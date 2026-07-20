namespace TiezhuToolbox.Modules.Recommend;

/// <summary>根据官方英雄属性直方图推导玩家普遍堆叠的有效属性。</summary>
public static class HeroUsefulStatAnalyzer
{
    private const double LowBucketThreshold = 0.15;
    private const int PeakBucketThreshold = 4;

    private static readonly IReadOnlyDictionary<string, string> StatNames = new Dictionary<string, string>
    {
        ["att"] = "攻击力", ["def"] = "防御力", ["max_hp"] = "生命值", ["speed"] = "速度",
        ["cri"] = "暴击率", ["cri_dmg"] = "暴击伤害", ["acc"] = "效果命中", ["res"] = "效果抗性",
    };

    private static readonly string[] CoreStats = ["att", "def", "max_hp", "speed", "cri", "cri_dmg"];

    public static List<string> InferUsefulStats(IReadOnlyDictionary<string, double[]> histograms)
    {
        var referenceTotal = CoreStats
            .Where(histograms.ContainsKey)
            .Select(code => histograms[code].Sum())
            .DefaultIfEmpty(0)
            .Max();
        var unavailableSamples = EstimateUnavailableSamples(histograms, referenceTotal);
        var result = new List<string>();

        foreach (var (code, statName) in StatNames)
        {
            if (!histograms.TryGetValue(code, out var source) || source.Length == 0)
                continue;

            var buckets = source.ToArray();
            var originalTotal = buckets.Sum();
            if (originalTotal <= 0)
                continue;
            var originalLowRatio =
                (buckets.ElementAtOrDefault(0) + buckets.ElementAtOrDefault(1)) / originalTotal;

            // 接口会把属性不可见的记录按英雄裸属性落入前两档；这批记录在六项核心属性中
            // 形成数量近似相同的共同尖峰。只对样本总数完整的直方图扣除，避免误伤会省略零值的命中/抗性。
            if (unavailableSamples > 0 && NearlyEqual(originalTotal, referenceTotal))
            {
                var baselineBucket = Array.FindIndex(buckets, 0, Math.Min(2, buckets.Length),
                    value => value >= unavailableSamples);
                if (baselineBucket >= 0)
                    buckets[baselineBucket] -= unavailableSamples;
            }

            var total = buckets.Sum();
            if (total <= 0)
                continue;
            var peak = Array.IndexOf(buckets, buckets.Max());
            // 低档占比继续使用原始直方图，避免把穿装后的自然属性增幅误判为主动堆叠；
            // 扣除不可见样本只用于恢复被异常低档尖峰遮住的真实最高柱。
            if (originalLowRatio < LowBucketThreshold || peak >= PeakBucketThreshold)
                result.Add(statName);
        }

        return result;
    }

    /// <summary>
    /// 用主流套装补足直方图可能漏掉的有效属性。
    /// 速度套进入使用率 ≥10% 的主流组合，说明玩家明确需要速度，应将速度视为有效属性。
    /// </summary>
    public static void ApplySetImplications(List<string> usefulStats, IEnumerable<HeroSetCombo> setCombos)
    {
        if (setCombos.Any(combo => combo.Sets.Contains("set_speed", StringComparer.Ordinal))
            && !usefulStats.Contains("速度", StringComparer.Ordinal))
        {
            usefulStats.Add("速度");
        }
    }

    public static double EstimateUnavailableSamples(IReadOnlyDictionary<string, double[]> histograms)
    {
        var referenceTotal = CoreStats
            .Where(histograms.ContainsKey)
            .Select(code => histograms[code].Sum())
            .DefaultIfEmpty(0)
            .Max();
        return EstimateUnavailableSamples(histograms, referenceTotal);
    }

    private static double EstimateUnavailableSamples(
        IReadOnlyDictionary<string, double[]> histograms,
        double referenceTotal)
    {
        if (referenceTotal <= 0)
            return 0;

        return CoreStats
            .Where(histograms.ContainsKey)
            .Select(code => histograms[code])
            .Where(buckets => buckets.Length >= 2 && NearlyEqual(buckets.Sum(), referenceTotal))
            .Select(buckets => Math.Max(buckets[0], buckets[1]))
            .DefaultIfEmpty(0)
            .Min();
    }

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.5;
}
