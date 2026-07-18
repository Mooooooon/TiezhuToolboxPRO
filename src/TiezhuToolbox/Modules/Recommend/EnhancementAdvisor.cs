using TiezhuToolbox.Modules.Ocr;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>强化建议结论。</summary>
public enum EnhanceAdvice
{
    /// <summary>无法判断（未识别出装备部位）。</summary>
    None,

    /// <summary>分数达到当前强化档位要求，继续强化。</summary>
    Continue,

    /// <summary>分数不足，但副属性速度达标，可继续赌速度。</summary>
    GambleSpeed,

    /// <summary>+15 且分数/速度达标，建议重铸。</summary>
    Reforge,

    /// <summary>分数与速度均不达标，建议放弃。</summary>
    GiveUp,

    /// <summary>右三件固定攻击/防御/生命主属性，建议放弃。</summary>
    GiveUpFixedMain,
}

/// <summary>强化建议结果：结论 + 展示文本 + 判定依据。</summary>
public record EnhanceAdviceResult(EnhanceAdvice Advice, string Text, string Detail);

/// <summary>
/// 强化建议（算法参考社区打铁助手脚本）：
/// 分数阶梯 —— +3 前分数 ≥ 阈值，之后每 3 级要求 +6 分，+15 时 ≥ 阈值+30 建议重铸；
/// 分数不达标时赌速度 —— 副属性速度 ≥ 3/6/9/12/12（对应 +3/+6/+9/+12/+15 前）可继续，+15 时速度 ≥ 15 建议重铸。
/// 左三件（武器/头盔/铠甲）直接走上述流程；项链/戒指即使是固定值主属性，只要速度达标也可作为速度散件继续赌；
/// 其余右三件固定值主属性直接淘汰。只有项链/戒指分数不足时可赌速度，鞋子分数不足直接放弃。
/// </summary>
public static class EnhancementAdvisor
{
    /// <summary>装备部位。</summary>
    private enum Part
    {
        Unknown,
        Weapon,
        Helm,
        Armor,
        Necklace,
        Ring,
        Boots,
    }

    /// <summary>分数阶梯：强化档位上限（不含）→ 相对阈值的加分。</summary>
    private static readonly (int LevelCap, double Offset)[] ScoreSteps =
        { (3, 0), (6, 6), (9, 12), (12, 18), (15, 24) };

    /// <summary>赌速度阶梯：强化档位上限（不含）→ 速度要求。</summary>
    private static readonly (int LevelCap, int Speed)[] SpeedSteps =
        { (3, 3), (6, 6), (9, 9), (12, 12), (15, 12) };

    /// <summary>
    /// 分析装备是否值得继续强化。
    /// </summary>
    /// <param name="info">识别出的装备信息（部位取自 <see cref="EquipmentInfo.Quality"/>，如"传说武器"）。</param>
    /// <param name="leftThreshold">左三件分数阈值。</param>
    /// <param name="rightThreshold">右三件分数阈值。</param>
    public static EnhanceAdviceResult Analyze(EquipmentInfo info, double leftThreshold, double rightThreshold)
    {
        var part = DetectPart(info.Quality);
        if (part == Part.Unknown)
            return new EnhanceAdviceResult(EnhanceAdvice.None, "无法判断", "未从品质文本中识别出装备部位");

        // 分数用民间算法由副属性现算，不依赖调用方是否已填 info.Score
        var score = EquipmentScoreCalculator.Calculate(info.SubStats);
        var enhance = info.EnhanceLevel;

        if (part is Part.Weapon or Part.Helm or Part.Armor)
        {
            return ScoreLadder(score, enhance, leftThreshold)
                   ?? SpeedLadder(GetSpeed(info), enhance, GiveUpDetail(score));
        }

        // 项链/戒指的固定值主属性通常应放弃，但速度达标时仍可作为速度散件继续赌。
        if (IsFixedMainStat(info.MainStatName, info.MainStatValue))
        {
            if (part is Part.Necklace or Part.Ring)
            {
                var speedOffPiece = SpeedOffPieceLadder(GetSpeed(info), enhance);
                if (speedOffPiece != null)
                    return speedOffPiece;
            }

            return new EnhanceAdviceResult(EnhanceAdvice.GiveUpFixedMain,
                "固定值主属性，建议放弃", $"右三件主属性为固定{info.MainStatName}，收益过低");
        }

        var byScore = ScoreLadder(score, enhance, rightThreshold);
        if (byScore != null)
            return byScore;

        if (part is Part.Necklace or Part.Ring)
            return SpeedLadder(GetSpeed(info), enhance, GiveUpDetail(score));

        return new EnhanceAdviceResult(EnhanceAdvice.GiveUp, "分数过低，建议放弃", GiveUpDetail(score));
    }

    /// <summary>分数阶梯：达标返回继续强化/建议重铸，当前档位不达标返回 null。</summary>
    private static EnhanceAdviceResult? ScoreLadder(double score, int enhance, double threshold)
    {
        foreach (var (cap, offset) in ScoreSteps)
        {
            if (enhance < cap)
            {
                var required = threshold + offset;
                return score >= required
                    ? new EnhanceAdviceResult(EnhanceAdvice.Continue,
                        "继续强化", $"分数 {score:0.##} ≥ {required:0.##}（强化 +{cap} 前要求）")
                    : null;
            }
        }

        return enhance == 15 && score >= threshold + 30
            ? new EnhanceAdviceResult(EnhanceAdvice.Reforge,
                "建议重铸", $"分数 {score:0.##} ≥ {threshold + 30:0.##}（重铸要求）")
            : null;
    }

    /// <summary>赌速度阶梯：速度达标返回继续赌速度/建议重铸，否则建议放弃。</summary>
    private static EnhanceAdviceResult SpeedLadder(int speed, int enhance, string giveUpDetail)
    {
        foreach (var (cap, required) in SpeedSteps)
        {
            if (enhance < cap)
            {
                return speed >= required
                    ? new EnhanceAdviceResult(EnhanceAdvice.GambleSpeed,
                        "继续赌速度", $"分数不足，但速度 {speed} ≥ {required}（强化 +{cap} 前要求）")
                    : new EnhanceAdviceResult(EnhanceAdvice.GiveUp, "分数过低，建议放弃", giveUpDetail);
            }
        }

        return enhance == 15 && speed >= 15
            ? new EnhanceAdviceResult(EnhanceAdvice.Reforge,
                "建议重铸", $"分数不足，但速度 {speed} ≥ 15，值得重铸")
            : new EnhanceAdviceResult(EnhanceAdvice.GiveUp, "分数过低，建议放弃", giveUpDetail);
    }

    /// <summary>固定值主属性项链/戒指的速度散件例外：速度达标返回建议，否则仍按固定主属性淘汰。</summary>
    private static EnhanceAdviceResult? SpeedOffPieceLadder(int speed, int enhance)
    {
        foreach (var (cap, required) in SpeedSteps)
        {
            if (enhance < cap)
            {
                return speed >= required
                    ? new EnhanceAdviceResult(EnhanceAdvice.GambleSpeed,
                        "继续赌速度", $"固定值主属性仅作速度散件，速度 {speed} ≥ {required}（强化 +{cap} 前要求）")
                    : null;
            }
        }

        return enhance == 15 && speed >= 15
            ? new EnhanceAdviceResult(EnhanceAdvice.Reforge,
                "建议重铸", $"固定值主属性仅作速度散件，速度 {speed} ≥ 15，值得重铸")
            : null;
    }

    /// <summary>从品质文本（如"传说武器"）识别装备部位。</summary>
    private static Part DetectPart(string quality)
    {
        if (quality.Contains("武器")) return Part.Weapon;
        if (quality.Contains("头盔")) return Part.Helm;
        if (quality.Contains("铠甲") || quality.Contains("护甲")) return Part.Armor;
        if (quality.Contains("项链")) return Part.Necklace;
        if (quality.Contains("戒指")) return Part.Ring;
        if (quality.Contains("鞋子") || quality.Contains("靴")) return Part.Boots;
        return Part.Unknown;
    }

    /// <summary>右三件固定攻击/防御/生命主属性判定（百分比主属性不算固定值）。</summary>
    private static bool IsFixedMainStat(string name, string value)
        => name is "攻击力" or "防御力" or "生命值" && !value.Contains('%');

    /// <summary>副属性中的速度值（取前导数字，取不到为 0）。</summary>
    private static int GetSpeed(EquipmentInfo info)
    {
        var speedSub = info.SubStats.FirstOrDefault(s => s.Name == "速度");
        if (speedSub == null)
            return 0;

        var digits = new string(speedSub.Value.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var speed) ? speed : 0;
    }

    private static string GiveUpDetail(double score)
        => $"分数 {score:0.##} 未达当前强化档位要求，速度也未达标";
}
