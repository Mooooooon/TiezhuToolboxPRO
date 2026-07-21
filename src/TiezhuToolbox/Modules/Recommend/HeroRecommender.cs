using TiezhuToolbox.Modules.Ocr;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>
/// 装备 → 适用角色推荐算法。
/// 主属性、套装与速度需求是硬门槛：不符合该角色的有用属性/主流搭配，
/// 或角色需要速度但装备没有速度时直接不推荐；
/// 通过门槛后，匹配度同时考虑可用属性覆盖率和强化分配质量：
/// 角色需求少于四种，或右三件主属性占用一种需求时，只要求覆盖实际可出现在副属性中的需求；
/// 再按装备分数权重检查有用属性是否吃到足够强化，因此强化全跳到无用属性仍会显著降分。
/// 数据为官方战绩前排分段（见 <see cref="HeroDatabase"/>）。
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

        return Recommend(info, db.Profiles, db.SetCodesByName, top);
    }

    /// <summary>使用指定英雄配置计算推荐，供配置预览与回归工具使用。</summary>
    public static IReadOnlyList<HeroRecommendation> Recommend(
        EquipmentInfo info,
        IReadOnlyList<HeroProfile> profiles,
        IReadOnlyDictionary<string, string> setCodesByName,
        int top = 5)
    {
        if (profiles.Count == 0)
            return Array.Empty<HeroRecommendation>();

        var gearSetCode = !string.IsNullOrWhiteSpace(info.SetName)
                          && setCodesByName.TryGetValue(info.SetName, out var resolvedSetCode)
            ? resolvedSetCode
            : null;
        // 每条副属性的分数权重（未识别/不参算的属性权重为 0）
        var scored = info.SubStats
            .Select(s => (Stat: s, s.Name, Score: EquipmentScoreCalculator.Calculate(s)))
            .Where(s => s.Score > 0)
            .ToList();
        var subStatSlotCount = info.SubStats.Count(s => !string.IsNullOrWhiteSpace(s.Name));
        var totalScore = scored.Sum(s => s.Score);
        if (totalScore <= 0 || subStatSlotCount == 0)
            return Array.Empty<HeroRecommendation>();

        // 主属性没识别出来 → 不过滤；固定值攻击/防御/生命 → 左三件，主属性固定无信息量 → 不过滤
        var mainStatInformative = !string.IsNullOrEmpty(info.MainStatName)
                                  && (info.MainStatValue.Contains('%') || !FixedLeftMainStats.Contains(info.MainStatName));

        var results = new List<HeroRecommendation>();
        var part = EquipmentRules.DetectPart(info.Quality);
        var normalizedMainStat = EquipmentRules.NormalizeMainStat(info);
        var gearHasSpeed = normalizedMainStat == "速度"
                           || info.SubStats.Any(s => s.Name == "速度");

        foreach (var hero in profiles)
        {
            if (hero.IsExcluded)
                continue;

            // 硬门槛一：右三件按部位配置检查主属性；未知部位保持旧版的宽松回退逻辑。
            if (!MainStatMatches(hero, part, normalizedMainStat, info, mainStatInformative))
                continue;

            // 硬门槛二：装备套装必须出现在该角色的主流搭配中（套装没识别出来时不过滤）
            if (gearSetCode != null && !hero.AllowedSets.Contains(gearSetCode))
                continue;

            // 硬门槛三：需要速度的角色，装备主/副属性中必须实际带有速度。
            if (hero.UsefulStats.Contains("速度") && !gearHasSpeed)
                continue;

            var matchableStats = GetMatchableUsefulStats(hero, part, normalizedMainStat);
            var matched = scored.Where(s => matchableStats.Contains(s.Name)).ToList();
            var matchedStatCount = matched.Select(s => s.Name).Distinct().Count();
            var targetStatCount = Math.Min(subStatSlotCount, matchableStats.Count);
            if (matchedStatCount == 0 || targetStatCount == 0)
                continue;

            // 属性覆盖率只要求命中该部位实际可能出现的角色需求。
            // 无用词条的初始分数属于必然填充，不纳入惩罚；一旦强化跳到无用词条，
            // 则按该词条的强化次数估算强化分数并降低分配质量。
            var usefulScore = matched.Sum(s => s.Score);
            var coverage = (double)matchedStatCount / targetStatCount;
            var totalRollCount = info.SubStats.Sum(s => Math.Clamp(s.RollCount, 0, 5));
            double allocationQuality;
            if (totalRollCount > 0)
            {
                var wastedEnhancementScore = scored
                    .Where(s => !matchableStats.Contains(s.Name))
                    .Sum(s => EstimateEnhancementScore(s.Score, s.Stat.RollCount));
                allocationQuality = usefulScore / (usefulScore + wastedEnhancementScore);
            }
            else if (info.EnhanceLevel > 0)
            {
                // 强化等级已识别但词条次数缺失时，退回按分数占比校正，避免错误给出满匹配。
                var neutralUsefulShare = (double)matchedStatCount / scored.Count;
                var actualUsefulShare = usefulScore / totalScore;
                allocationQuality = Math.Min(1.0, actualUsefulShare / neutralUsefulShare);
            }
            else
            {
                allocationQuality = 1.0;
            }

            results.Add(new HeroRecommendation
            {
                Code = hero.Code,
                Name = hero.Name,
                Score = Math.Round(100.0 * coverage * allocationQuality, 1),
                MatchedStats = matched.Select(s => s.Name).Distinct().ToList(),
                SetMatched = true,
                AvatarPath = HeroDatabase.GetAvatarPath(hero.Code),
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.MatchedStats.Count)
            .ThenBy(r => r.Name)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// 根据词条总分和强化次数估算由强化产生的分数。
    /// 缺少逐跳数值时，将当前总分按“初始一次 + 强化次数”平均拆分。
    /// </summary>
    private static double EstimateEnhancementScore(double totalScore, int rollCount)
    {
        var rolls = Math.Clamp(rollCount, 0, 5);
        return rolls == 0 ? 0 : totalScore * rolls / (rolls + 1.0);
    }

    /// <summary>
    /// 返回当前部位实际可能出现在副属性中的角色需求。
    /// 右三件的主属性不会同时成为副属性，因此需要从匹配目标中移除。
    /// </summary>
    private static HashSet<string> GetMatchableUsefulStats(
        HeroProfile hero,
        EquipmentPart part,
        string? normalizedMainStat)
    {
        var matchable = hero.UsefulStats.ToHashSet(StringComparer.Ordinal);
        if (part is EquipmentPart.Necklace or EquipmentPart.Ring or EquipmentPart.Boots
            && normalizedMainStat != null)
        {
            matchable.Remove(normalizedMainStat.TrimEnd('%'));
        }

        return matchable;
    }

    private static bool MainStatMatches(
        HeroProfile hero,
        EquipmentPart part,
        string? normalizedMainStat,
        EquipmentInfo info,
        bool mainStatInformative)
    {
        if (part is EquipmentPart.Weapon or EquipmentPart.Helm or EquipmentPart.Armor)
            return true;

        if (part is EquipmentPart.Necklace or EquipmentPart.Ring or EquipmentPart.Boots)
        {
            // 主属性完全未识别时不过滤；识别到固定值或未知右侧主属性时不匹配任何角色。
            if (string.IsNullOrWhiteSpace(info.MainStatName))
                return true;
            if (normalizedMainStat == null)
                return false;

            return part switch
            {
                EquipmentPart.Necklace => hero.NecklaceMainStats.Contains(normalizedMainStat),
                EquipmentPart.Ring => hero.RingMainStats.Contains(normalizedMainStat),
                EquipmentPart.Boots => hero.BootsMainStats.Contains(normalizedMainStat),
                _ => true,
            };
        }

        return !mainStatInformative || hero.UsefulStats.Contains(info.MainStatName);
    }
}
