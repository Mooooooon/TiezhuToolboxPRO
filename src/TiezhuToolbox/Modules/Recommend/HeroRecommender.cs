using TiezhuToolbox.Modules.Ocr;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>
/// 装备 → 适用角色推荐算法。
/// 主属性与套装是硬门槛：不符合该角色的有用属性/主流搭配就直接不推荐；
/// 通过门槛后，匹配度按副属性的装备分数加权：
/// 有用属性的得分之和 ÷ 装备副属性总分 × 100（民间算法权重，见 <see cref="EquipmentScoreCalculator"/>）。
/// 因此强化全跳到无用属性上的装备，匹配度会显著降低。
/// 数据为官方战绩传说分段（见 <see cref="HeroDatabase"/>）。
/// </summary>
public static class HeroRecommender
{
    /// <summary>左三件（武器/头盔/铠甲）的固定主属性，对推荐没有区分度。</summary>
    private static readonly HashSet<string> FixedLeftMainStats = new() { "攻击力", "防御力", "生命值" };

    /// <summary>
    /// 计算装备对各角色的匹配度，返回降序前 <paramref name="top"/> 名（匹配度 ≤ 0 的不返回）。
    /// 数据文件缺失或损坏时返回空列表。
    /// </summary>
    public static IReadOnlyList<HeroRecommendation> Recommend(EquipmentInfo info, int top = 5)
    {
        var db = HeroDatabase.Instance;
        if (!db.IsLoaded)
            return Array.Empty<HeroRecommendation>();

        var gearSetCode = db.FindSetCode(info.SetName);
        // 每条副属性的分数权重（未识别/不参算的属性权重为 0）
        var scored = info.SubStats.Select(s => (s.Name, Score: EquipmentScoreCalculator.Calculate(s))).ToList();
        var totalScore = scored.Sum(s => s.Score);
        if (totalScore <= 0)
            return Array.Empty<HeroRecommendation>();

        // 主属性没识别出来 → 不过滤；固定值攻击/防御/生命 → 左三件，主属性固定无信息量 → 不过滤
        var mainStatInformative = !string.IsNullOrEmpty(info.MainStatName)
                                  && (info.MainStatValue.Contains('%') || !FixedLeftMainStats.Contains(info.MainStatName));

        var results = new List<HeroRecommendation>();
        foreach (var hero in db.Heroes)
        {
            // 硬门槛一：主属性必须属于该角色的有用属性（如暴击项链对 c5154 直接淘汰）
            if (mainStatInformative && !hero.UsefulStats.Contains(info.MainStatName))
                continue;

            // 硬门槛二：装备套装必须出现在该角色的主流搭配中（套装没识别出来时不过滤）
            if (gearSetCode != null && !hero.SetCombos.Any(c => c.Sets.Contains(gearSetCode)))
                continue;

            // 匹配度 = 有用属性得分占比（分数权重即民间算法，跳得多的属性权重更大）
            var usefulScore = scored.Where(s => hero.UsefulStats.Contains(s.Name)).Sum(s => s.Score);
            if (usefulScore <= 0)
                continue;

            results.Add(new HeroRecommendation
            {
                Code = hero.Code,
                Name = hero.Name,
                Score = Math.Round(100.0 * usefulScore / totalScore, 1),
                MatchedStats = scored.Where(s => s.Score > 0 && hero.UsefulStats.Contains(s.Name))
                    .Select(s => s.Name).ToList(),
                SetMatched = true,
                AvatarPath = HeroDatabase.GetAvatarPath(hero.Code),
            });
        }

        return results.OrderByDescending(r => r.Score).ThenBy(r => r.Name).Take(top).ToList();
    }
}
