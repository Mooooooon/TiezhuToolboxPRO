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

    /// <summary>+15 且预计重铸分数或速度达标，建议重铸。</summary>
    Reforge,

    /// <summary>88 级 +15 装备达到最终要求，建议保留。</summary>
    Keep,

    /// <summary>分数与速度均不达标，建议放弃。</summary>
    GiveUp,

    /// <summary>右三件固定攻击/防御/生命主属性，建议放弃。</summary>
    GiveUpFixedMain,
}

/// <summary>强化建议结果：结论 + 展示文本 + 判定依据。</summary>
public record EnhanceAdviceResult(EnhanceAdvice Advice, string Text, string Detail);

/// <summary>
/// 强化建议（算法参考社区打铁助手脚本）：
/// 85 级分数阶梯 —— +3 前分数 ≥ 阈值，之后每 3 级要求 +6 分，+15 时模拟游戏增量且重铸后分数 ≥ 65；
/// 88 级分数阶梯 —— 默认 28 分起步，之后每 3 级要求 +7 分，+15 达到最终要求时建议保留且不建议重铸；
/// 分数不达标时赌速度 —— 副属性速度 ≥ 3/6/9/12/12（对应 +3/+6/+9/+12/+15 前）可继续；
/// +15 时速度 ≥ 15，85 级建议重铸，88 级建议保留。
/// 分数达标时仍会检查用途：没有速度潜质且最高角色匹配度低于 70% 时建议放弃；
/// 左三件（武器/头盔/铠甲）直接走上述流程；项链/戒指即使是固定值主属性，只要速度达标也可作为速度散件继续赌；
/// 其余右三件固定值主属性直接淘汰。只有项链/戒指分数不足时可赌速度，鞋子分数不足直接放弃。
/// </summary>
public static class EnhancementAdvisor
{
    private const double ReforgeScoreThreshold = 65;
    private const double MinimumHeroMatchScore = 70;

    /// <summary>分数阶梯：强化档位上限（不含）→ 相对阈值的加分。</summary>
    private static readonly (int LevelCap, double Offset)[] ScoreSteps =
        { (3, 0), (6, 6), (9, 12), (12, 18), (15, 24) };

    private static readonly (int LevelCap, double Offset)[] Level88ScoreSteps =
        { (3, 0), (6, 7), (9, 14), (12, 21), (15, 28) };

    /// <summary>赌速度阶梯：强化档位上限（不含）→ 速度要求。</summary>
    private static readonly (int LevelCap, int Speed)[] SpeedSteps =
        { (3, 3), (6, 6), (9, 9), (12, 12), (15, 12) };

    /// <summary>
    /// 分析装备是否值得继续强化。
    /// </summary>
    /// <param name="info">识别出的装备信息（部位取自 <see cref="EquipmentInfo.Quality"/>，如"传说武器"）。</param>
    /// <param name="leftThreshold">左三件分数阈值。</param>
    /// <param name="rightThreshold">右三件分数阈值。</param>
    /// <param name="level88Threshold">88 级装备的统一起步阈值。</param>
    public static EnhanceAdviceResult Analyze(
        EquipmentInfo info, double leftThreshold, double rightThreshold, double level88Threshold = 28)
    {
        var part = EquipmentRules.DetectPart(info.Quality);
        if (part == EquipmentPart.Unknown)
            return new EnhanceAdviceResult(EnhanceAdvice.None, "无法判断", "未从品质文本中识别出装备部位");

        // 分数用民间算法由副属性现算，不依赖调用方是否已填 info.Score
        var score = EquipmentScoreCalculator.Calculate(info.SubStats);
        var reforgedScore = EquipmentScoreCalculator.CalculateReforged(info.SubStats);
        var enhance = info.EnhanceLevel;
        var isLevel88 = info.Level == 88;

        // 游戏只允许 85 级 +15 装备重铸到 90 级；等级未识别时仍继续判断，避免 OCR 偶发漏读导致无建议。
        if (enhance == 15 && info.Level == 90)
            return new EnhanceAdviceResult(EnhanceAdvice.None, "已完成重铸", "装备等级已是 90");
        if (enhance == 15 && info.Level > 0 && info.Level is not (85 or 88))
            return new EnhanceAdviceResult(EnhanceAdvice.None, "不支持重铸", $"装备等级 {info.Level} 不能重铸为 90 级");

        var threshold = isLevel88
            ? level88Threshold
            : part is EquipmentPart.Weapon or EquipmentPart.Helm or EquipmentPart.Armor
                ? leftThreshold
                : rightThreshold;

        if (part is EquipmentPart.Weapon or EquipmentPart.Helm or EquipmentPart.Armor)
        {
            var speed = GetSpeed(info);
            var leftScoreAdvice = ScoreLadder(score, reforgedScore, enhance, threshold, isLevel88);
            if (leftScoreAdvice != null)
                return ApplyHeroMatchGate(info, leftScoreAdvice, speed, enhance);

            return SpeedLadder(speed, enhance, isLevel88,
                GiveUpDetail(score, reforgedScore, enhance, threshold, isLevel88));
        }

        // 项链/戒指的固定值主属性通常应放弃，但速度达标时仍可作为速度散件继续赌。
        if (IsFixedMainStat(info.MainStatName, info.MainStatValue))
        {
            if (part is EquipmentPart.Necklace or EquipmentPart.Ring)
            {
                var speedOffPiece = SpeedOffPieceLadder(GetSpeed(info), enhance, isLevel88);
                if (speedOffPiece != null)
                    return speedOffPiece;
            }

            return new EnhanceAdviceResult(EnhanceAdvice.GiveUpFixedMain,
                "固定值主属性，建议放弃", $"右三件主属性为固定{info.MainStatName}，收益过低");
        }

        var byScore = ScoreLadder(score, reforgedScore, enhance, threshold, isLevel88);
        if (byScore != null)
            return ApplyHeroMatchGate(info, byScore, GetSpeed(info), enhance);

        if (part is EquipmentPart.Necklace or EquipmentPart.Ring)
            return SpeedLadder(GetSpeed(info), enhance, isLevel88,
                GiveUpDetail(score, reforgedScore, enhance, threshold, isLevel88));

        return new EnhanceAdviceResult(EnhanceAdvice.GiveUp, "分数过低，建议放弃",
            GiveUpDetail(score, reforgedScore, enhance, threshold, isLevel88));
    }

    /// <summary>分数阶梯：达标返回继续强化/建议重铸，当前档位不达标返回 null。</summary>
    private static EnhanceAdviceResult? ScoreLadder(
        double score, double reforgedScore, int enhance, double threshold, bool isLevel88)
    {
        var steps = isLevel88 ? Level88ScoreSteps : ScoreSteps;
        foreach (var (cap, offset) in steps)
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

        if (enhance == 15 && isLevel88)
            return score >= threshold + 35
                ? new EnhanceAdviceResult(EnhanceAdvice.Keep,
                    "建议保留", $"88级装备不可重铸，最终分数 {score:0.##} ≥ {threshold + 35:0.##}")
                : null;

        return enhance == 15 && reforgedScore >= ReforgeScoreThreshold
            ? new EnhanceAdviceResult(EnhanceAdvice.Reforge,
                "建议重铸", $"预计重铸分数 {reforgedScore:0.##} ≥ {ReforgeScoreThreshold:0.##}")
            : null;
    }

    /// <summary>
    /// 分数达标后再检查装备用途：没有速度潜质且所有角色匹配度都低于 70% 时放弃。
    /// 角色数据未加载时跳过此门槛，避免数据文件异常导致误判。
    /// </summary>
    private static EnhanceAdviceResult ApplyHeroMatchGate(
        EquipmentInfo info, EnhanceAdviceResult scoreAdvice, int speed, int enhance)
    {
        if (HasSpeedPotential(speed, enhance) || !HeroDatabase.Instance.IsLoaded)
            return scoreAdvice;

        var bestMatch = HeroRecommender.Recommend(info, top: 1).FirstOrDefault();
        if (bestMatch?.Score >= MinimumHeroMatchScore)
            return scoreAdvice;

        var matchDetail = bestMatch == null
            ? "没有匹配到适用角色"
            : $"最高匹配度 {bestMatch.Score:0.#}% < {MinimumHeroMatchScore:0.#}%";
        return new EnhanceAdviceResult(EnhanceAdvice.GiveUp,
            "匹配度过低，建议放弃", $"{matchDetail}，且速度 {speed} 未达当前强化档位要求");
    }

    /// <summary>副属性速度是否达到当前强化档位的速度潜质要求。</summary>
    private static bool HasSpeedPotential(int speed, int enhance)
    {
        foreach (var (cap, required) in SpeedSteps)
        {
            if (enhance < cap)
                return speed >= required;
        }

        return enhance == 15 && speed >= 15;
    }

    /// <summary>赌速度阶梯：速度达标返回继续赌速度/建议重铸，否则建议放弃。</summary>
    private static EnhanceAdviceResult SpeedLadder(int speed, int enhance, bool isLevel88, string giveUpDetail)
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

        if (enhance == 15 && isLevel88 && speed >= 15)
            return new EnhanceAdviceResult(EnhanceAdvice.Keep,
                "建议保留", $"88级装备不可重铸，分数不足但速度 {speed} ≥ 15");

        return enhance == 15 && speed >= 15
            ? new EnhanceAdviceResult(EnhanceAdvice.Reforge,
                "建议重铸", $"分数不足，但速度 {speed} ≥ 15，值得重铸")
            : new EnhanceAdviceResult(EnhanceAdvice.GiveUp, "分数过低，建议放弃", giveUpDetail);
    }

    /// <summary>固定值主属性项链/戒指的速度散件例外：速度达标返回建议，否则仍按固定主属性淘汰。</summary>
    private static EnhanceAdviceResult? SpeedOffPieceLadder(int speed, int enhance, bool isLevel88)
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

        if (enhance == 15 && isLevel88 && speed >= 15)
            return new EnhanceAdviceResult(EnhanceAdvice.Keep,
                "建议保留", $"88级固定值主属性仅作速度散件，速度 {speed} ≥ 15");

        return enhance == 15 && speed >= 15
            ? new EnhanceAdviceResult(EnhanceAdvice.Reforge,
                "建议重铸", $"固定值主属性仅作速度散件，速度 {speed} ≥ 15，值得重铸")
            : null;
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

    private static string GiveUpDetail(
        double score, double reforgedScore, int enhance, double threshold, bool isLevel88)
        => enhance == 15 && isLevel88
            ? $"88级最终分数 {score:0.##} < {threshold + 35:0.##}，速度也未达标"
            : enhance == 15
            ? $"预计重铸分数 {reforgedScore:0.##} < {ReforgeScoreThreshold:0.##}，速度也未达标"
            : $"分数 {score:0.##} 未达当前强化档位要求，速度也未达标";
}
