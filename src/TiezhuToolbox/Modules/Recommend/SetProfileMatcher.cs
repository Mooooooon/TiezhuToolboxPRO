using TiezhuToolbox.Modules.Ocr;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>装备 → 当前套装属性子类匹配算法。</summary>
public static class SetProfileMatcher
{
    private const double ReplacedInitialStatPenalty = 0.20;

    private sealed record ScoreComponent(
        string Name,
        double Value,
        double InitialValue,
        double EnhancedValue,
        bool IsMain);
    private sealed record ScoreResult(double Score, List<string> MatchedStats, double MainEffectiveValue);

    /// <summary>按匹配度返回当前套装最适合的属性子类。</summary>
    public static IReadOnlyList<SetProfileRecommendation> Match(
        EquipmentInfo info,
        int top = 5,
        IReadOnlySet<string>? disabledProfileKeys = null)
    {
        var database = DemandDatabase.Instance;
        if (!database.IsLoaded)
            return Array.Empty<SetProfileRecommendation>();

        var set = database.FindSet(info.SetName);
        return set == null
            ? Array.Empty<SetProfileRecommendation>()
            : Match(info, set, top, disabledProfileKeys);
    }

    /// <summary>使用指定套装数据匹配，供回归测试使用。</summary>
    public static IReadOnlyList<SetProfileRecommendation> Match(
        EquipmentInfo info,
        DemandSet set,
        int top = 5,
        IReadOnlySet<string>? disabledProfileKeys = null)
    {
        if (set.Profiles.Count == 0 || top <= 0)
            return Array.Empty<SetProfileRecommendation>();

        var part = EquipmentRules.DetectPart(info.Quality);
        if (part == EquipmentPart.Unknown)
            return Array.Empty<SetProfileRecommendation>();

        var components = BuildComponents(info, part, out var mainStatText);
        if (components == null || components.Count == 0)
            return Array.Empty<SetProfileRecommendation>();

        var slotCount = info.SubStats.Count(stat => !string.IsNullOrWhiteSpace(stat.Name));
        if (part is EquipmentPart.Necklace or EquipmentPart.Ring or EquipmentPart.Boots)
            slotCount++;
        if (slotCount <= 0)
            return Array.Empty<SetProfileRecommendation>();

        return set.Profiles
            .Where(profile => disabledProfileKeys == null
                              || !disabledProfileKeys.Contains(CreateProfileKey(set.Code, profile.Id)))
            .Select(profile =>
            {
                var aggregate = Calculate(profile.Stats, profile.Weights, components, slotCount);
                var heroes = profile.Heroes.Select(hero =>
                    {
                        var result = Calculate(profile.Stats, hero.Weights, components, slotCount);
                        return new HeroBuildRecommendation
                        {
                            Code = hero.Code,
                            Name = hero.Name,
                            ComboName = hero.ComboName,
                            Score = result.Score,
                            SampleShare = hero.SampleShare,
                            DemandContribution = hero.DemandContribution,
                            MatchedStats = result.MatchedStats,
                            AvatarPath = DemandDatabase.GetAvatarPath(hero.Code),
                        };
                    })
                    .OrderByDescending(hero => hero.Score)
                    .ThenByDescending(hero => hero.DemandContribution)
                    .ThenBy(hero => hero.Name, StringComparer.CurrentCulture)
                    .ThenBy(hero => hero.ComboName, StringComparer.CurrentCulture)
                    .ToList();

                var mainContribution = components.FirstOrDefault(component => component.IsMain) is not null
                    ? $"{mainStatText}；对子类有效价值 {aggregate.MainEffectiveValue:0.##}"
                    : string.Empty;
                return new SetProfileRecommendation
                {
                    SetCode = set.Code,
                    SetName = set.Name,
                    ProfileId = profile.Id,
                    ProfileName = profile.Name,
                    Score = aggregate.Score,
                    DemandWeight = profile.DemandWeight,
                    MatchedStats = aggregate.MatchedStats,
                    MainStatContribution = mainContribution,
                    Heroes = heroes,
                };
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.DemandWeight)
            .ThenBy(result => result.ProfileName, StringComparer.CurrentCulture)
            .Take(top)
            .ToList();
    }

    /// <summary>生成可持久化的套装子类唯一键。</summary>
    public static string CreateProfileKey(string setCode, string profileId)
        => $"{setCode}/{profileId}";

    private static List<ScoreComponent>? BuildComponents(
        EquipmentInfo info,
        EquipmentPart part,
        out string mainStatText)
    {
        mainStatText = string.Empty;
        var components = info.SubStats
            .Select(BuildSubStatComponent)
            .Where(component => component.Value > 0)
            .ToList();

        if (part is EquipmentPart.Weapon or EquipmentPart.Helm or EquipmentPart.Armor)
            return components;

        if (part is not (EquipmentPart.Necklace or EquipmentPart.Ring or EquipmentPart.Boots)
            || info.Level is not (85 or 88 or 90)
            || !TryGetMainStatComponent(info, out var main, out mainStatText))
        {
            return null;
        }

        components.Add(main);
        return components;
    }

    private static ScoreComponent BuildSubStatComponent(SubStat stat)
    {
        var totalValue = EquipmentScoreCalculator.Calculate(stat);
        var enhancedValue = 0d;
        if (!string.IsNullOrWhiteSpace(stat.EnhanceValue))
        {
            enhancedValue = EquipmentScoreCalculator.Calculate(new SubStat
            {
                Name = stat.Name,
                Value = stat.EnhanceValue,
            });
        }
        else if (stat.RollCount > 0)
        {
            // 强化增量文本偶发漏识别时，以“一次初始词条 + RollCount 次强化”的比例估算。
            enhancedValue = totalValue * stat.RollCount / (stat.RollCount + 1d);
        }

        enhancedValue = Math.Clamp(enhancedValue, 0, totalValue);
        return new ScoreComponent(
            stat.Name,
            totalValue,
            totalValue - enhancedValue,
            enhancedValue,
            IsMain: false);
    }

    /// <summary>取得85→90预估及88/90满强化右三主属性价值。</summary>
    private static bool TryGetMainStatComponent(
        EquipmentInfo info,
        out ScoreComponent component,
        out string display)
    {
        component = new ScoreComponent(string.Empty, 0, 0, 0, IsMain: true);
        display = string.Empty;
        var normalized = EquipmentRules.NormalizeMainStat(info);
        if (normalized == null)
            return false;

        var statName = normalized.TrimEnd('%');
        var fullValue = normalized switch
        {
            "攻击力%" or "防御力%" or "生命值%" => 65,
            "效果命中" or "效果抗性" => 65,
            "暴击率" => 60,
            "暴击伤害" => 70,
            "速度" => 45,
            _ => 0,
        };
        if (fullValue == 0)
            return false;

        var scoreValue = EquipmentScoreCalculator.Calculate(new SubStat
        {
            Name = statName,
            Value = normalized.EndsWith('%') || statName != "速度"
                ? $"{fullValue}%"
                : fullValue.ToString(),
        });
        if (scoreValue <= 0)
            return false;

        // 右三主属性是装备用途的主动选择，错主属性必须全额处罚，不能享受初始词条容忍。
        component = new ScoreComponent(statName, scoreValue, 0, scoreValue, IsMain: true);
        display = $"{statName}满值 {fullValue}{(statName == "速度" ? string.Empty : "%")}，价值 {scoreValue:0.##}";
        return true;
    }

    private static ScoreResult Calculate(
        IReadOnlyCollection<string> explicitStats,
        IReadOnlyDictionary<string, double> rawWeights,
        IReadOnlyList<ScoreComponent> components,
        int slotCount)
    {
        var desired = explicitStats
            .Where(stat => rawWeights.GetValueOrDefault(stat) > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (desired.Count == 0)
            return new ScoreResult(0, new List<string>(), 0);

        var present = components.Select(component => component.Name)
            .Where(desired.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var targetCount = Math.Min(slotCount, desired.Count);
        var coverageDenominator = desired
            .Select(stat => rawWeights.GetValueOrDefault(stat))
            .OrderByDescending(value => value)
            .Take(targetCount)
            .Sum();
        var coverage = coverageDenominator <= 0
            ? 0
            : Math.Clamp(
                present.Sum(stat => rawWeights.GetValueOrDefault(stat)) / coverageDenominator,
                0,
                1);

        var usefulValue = components
            .Where(component => desired.Contains(component.Name))
            .Sum(component => component.Value);
        if (usefulValue <= 0)
            return new ScoreResult(0, present, 0);

        var offProfilePenalty = CalculateOffProfilePenalty(desired, components);
        var effectiveness = Math.Clamp(
            usefulValue / (usefulValue + offProfilePenalty),
            0,
            1);
        var allocationQuality = CalculateDistributionAlignment(
            desired,
            rawWeights,
            components);
        var mainEffectiveValue = components
            .Where(component => component.IsMain && desired.Contains(component.Name))
            .Sum(component => component.Value);

        return new ScoreResult(
            Math.Round(100 * effectiveness * coverage * allocationQuality, 1),
            present,
            mainEffectiveValue);
    }

    /// <summary>
    /// 装备只能修改一条副属性：选择初始价值最高的一条歪副属性作为修改目标，
    /// 其强化增量全额损失、初始值按20%损失；其他歪属性及错误主属性全额处罚。
    /// </summary>
    private static double CalculateOffProfilePenalty(
        IReadOnlyCollection<string> desired,
        IReadOnlyList<ScoreComponent> components)
    {
        var offProfile = components
            .Where(component => !desired.Contains(component.Name))
            .ToList();
        var fullPenalty = offProfile.Sum(component => component.Value);
        var bestReplacementSaving = offProfile
            .Where(component => !component.IsMain)
            .Select(component => component.InitialValue * (1 - ReplacedInitialStatPenalty))
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(0, fullPenalty - bestReplacementSaving);
    }

    /// <summary>
    /// 使用归一化 Jensen-Shannon 散度比较“需求权重分布”和“实际有效分分布”。
    /// 缺失属性已由覆盖率处罚，因此这里只比较已经命中的有效属性，避免重复扣分。
    /// </summary>
    private static double CalculateDistributionAlignment(
        IReadOnlyCollection<string> desired,
        IReadOnlyDictionary<string, double> rawWeights,
        IReadOnlyList<ScoreComponent> components)
    {
        var values = components
            .Where(component => desired.Contains(component.Name))
            .GroupBy(component => component.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(component => component.Value),
                StringComparer.Ordinal);
        if (values.Count == 0)
            return 0;

        var weightSum = values.Keys.Sum(stat => rawWeights.GetValueOrDefault(stat));
        var valueSum = values.Values.Sum();
        if (weightSum <= 0 || valueSum <= 0)
            return 0;

        var divergence = 0d;
        foreach (var (stat, value) in values)
        {
            var targetShare = rawWeights.GetValueOrDefault(stat) / weightSum;
            var actualShare = value / valueSum;
            var middle = (targetShare + actualShare) / 2;
            divergence += 0.5 * targetShare * Math.Log2(targetShare / middle);
            divergence += 0.5 * actualShare * Math.Log2(actualShare / middle);
        }

        return Math.Clamp(1 - divergence, 0, 1);
    }
}
