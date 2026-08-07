using System.Collections.Concurrent;
using System.Diagnostics;
using TiezhuToolbox.Modules.Account;

namespace TiezhuToolbox.Modules.Optimizer;

public sealed class BuildOptimizer(HeroStatCalculator calculator)
{
    private static readonly GearSlot[] SlotOrder =
    [
        GearSlot.Necklace, GearSlot.Ring, GearSlot.Boots,
        GearSlot.Weapon, GearSlot.Helmet, GearSlot.Armor,
    ];

    public Task<OptimizationSearchResult> SearchAsync(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Search(request, progress, cancellationToken), CancellationToken.None);

    public OptimizationSearchResult Search(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!cancellationToken.CanBeCanceled)
            cancellationToken = request.CancellationToken;
        Validate(request);
        var stopwatch = Stopwatch.StartNew();
        var fixedPanel = calculator.Calculate(request.Hero, request.HeroPreference, []);
        if (fixedPanel.Warning?.Contains("静态目录未覆盖", StringComparison.Ordinal) == true)
            throw new InvalidOperationException(fixedPanel.Warning);

        var targetPriority = request.HeroPreference?.Priority ?? int.MaxValue;
        var priorities = request.HeroPriorities.ToDictionary(value => value.HeroId, value => value.Priority, StringComparer.Ordinal);
        var grouped = SlotOrder.Select(slot => request.Equipment
                .Where(item => item.Slot == slot)
                .Where(item => IsAllowedByOwner(item, request.Hero.Id, request.OccupationMode, targetPriority, priorities))
                .Where(item => IsAllowedMain(item, request.AllowedMainStats))
                .Select(item => new Candidate(item, Potential(item, fixedPanel.BaseStats),
                    ItemWeightedScore(item, fixedPanel.BaseStats, request.Weights)))
                .OrderByDescending(value => value.Score)
                .ToArray())
            .ToArray();
        if (grouped.Any(values => values.Length == 0))
            return Empty(stopwatch, complete: true);

        // 动态选择候选最少的部位作为根层，降低并行任务数量和首轮分支宽度。
        var ordered = grouped.OrderBy(values => values.Length).ToArray();
        var suffixMax = BuildSuffixMax(ordered);
        var suffixScore = BuildSuffixScore(ordered);
        var required = request.RequiredSets.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var top = new TopResults(Math.Clamp(request.ResultLimit, 1, 2000));
        long checkedCount = 0;
        long prunedCount = 0;
        long lastProgressTicks = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
            CancellationToken = cancellationToken,
        };
        try
        {
            Parallel.ForEach(ordered[0], options, first =>
            {
                var selected = new AccountGear[6];
                selected[0] = first.Item;
                var setCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    [first.Item.Set] = 1,
                };
                SearchDepth(1, first.Potential, first.Score, selected, setCounts);
            });
        }
        catch (OperationCanceledException)
        {
            // 已找到的结果仍然有价值，作为不完整结果返回。
        }

        var complete = !cancellationToken.IsCancellationRequested;
        var finalProgress = new OptimizationProgress(
            Interlocked.Read(ref checkedCount), Interlocked.Read(ref prunedCount), top.Threshold,
            stopwatch.Elapsed, !complete);
        progress?.Report(finalProgress);
        return new OptimizationSearchResult(top.Snapshot(), finalProgress, complete);

        void SearchDepth(int depth, HeroStats potential, double partialScore, AccountGear[] selected,
            Dictionary<string, int> setCounts)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            if (!CanReachRequiredSets(depth, ordered, required, setCounts))
            {
                Interlocked.Increment(ref prunedCount);
                return;
            }
            if (!CanReachMinimums(fixedPanel.RawStats, potential, suffixMax[depth], request.StatRanges))
            {
                Interlocked.Increment(ref prunedCount);
                return;
            }
            // 英雄固定加成与套装加成不在逐件分数中；使用宽松上界，宁可少剪枝也不能漏解。
            if (top.IsFull && partialScore + suffixScore[depth] + 1000 < top.Threshold)
            {
                Interlocked.Increment(ref prunedCount);
                return;
            }

            if (depth == ordered.Length)
            {
                var currentChecked = Interlocked.Increment(ref checkedCount);
                if ((currentChecked & 4095) == 0)
                    ReportIfDue();
                if (!HasRequiredSets(required, setCounts))
                    return;
                var equipment = selected.ToArray();
                var panel = calculator.Calculate(request.Hero, request.HeroPreference, equipment);
                if (!MatchesRanges(panel.RawStats, request.StatRanges))
                    return;
                var score = HeroStatCalculator.CalculateWeightedScore(panel.BaseStats, panel.RawStats, request.Weights);
                var conflicts = equipment.Select(item => item.EquippedHeroId)
                    .Where(id => !string.IsNullOrEmpty(id) && id != request.Hero.Id)
                    .Distinct(StringComparer.Ordinal).ToArray();
                top.Add(new OptimizationResult(equipment, panel, score, conflicts.Length, conflicts));
                ReportIfDue();
                return;
            }

            foreach (var candidate in ordered[depth])
            {
                selected[depth] = candidate.Item;
                setCounts.TryGetValue(candidate.Item.Set, out var oldCount);
                setCounts[candidate.Item.Set] = oldCount + 1;
                SearchDepth(depth + 1, potential + candidate.Potential, partialScore + candidate.Score,
                    selected, setCounts);
                if (oldCount == 0)
                    setCounts.Remove(candidate.Item.Set);
                else
                    setCounts[candidate.Item.Set] = oldCount;
            }
        }

        void ReportIfDue()
        {
            var now = stopwatch.ElapsedTicks;
            var previous = Interlocked.Read(ref lastProgressTicks);
            if (now - previous < Stopwatch.Frequency / 4 ||
                Interlocked.CompareExchange(ref lastProgressTicks, now, previous) != previous)
                return;
            progress?.Report(new OptimizationProgress(
                Interlocked.Read(ref checkedCount), Interlocked.Read(ref prunedCount), top.Threshold,
                stopwatch.Elapsed, false));
        }
    }

    private static void Validate(OptimizationRequest request)
    {
        if (request.Equipment.Count == 0)
            throw new ArgumentException("没有可搜索的装备", nameof(request));
        var weightSum = request.Weights.Attack + request.Weights.Health + request.Weights.Defense
                        + request.Weights.Speed + request.Weights.CriticalChance + request.Weights.CriticalDamage
                        + request.Weights.Effectiveness + request.Weights.Resistance;
        if (weightSum <= 0)
            throw new ArgumentException("至少设置一项大于零的权重", nameof(request));
        foreach (var range in request.StatRanges.Values)
            if (range.Minimum.HasValue && range.Maximum.HasValue && range.Minimum > range.Maximum)
                throw new ArgumentException("属性最小值不能大于最大值", nameof(request));
    }

    private static bool IsAllowedByOwner(AccountGear item, string heroId, EquipmentOccupationMode mode,
        int targetPriority, IReadOnlyDictionary<string, int> priorities)
    {
        if (mode == EquipmentOccupationMode.All || string.IsNullOrEmpty(item.EquippedHeroId) || item.EquippedHeroId == heroId)
            return true;
        if (mode == EquipmentOccupationMode.UnequippedOrTarget)
            return false;
        return !priorities.TryGetValue(item.EquippedHeroId, out var ownerPriority) || ownerPriority >= targetPriority;
    }

    private static bool IsAllowedMain(AccountGear item,
        IReadOnlyDictionary<GearSlot, HashSet<GearStatType>> allowed) =>
        !allowed.TryGetValue(item.Slot, out var values) || values.Count == 0 || values.Contains(item.Main.Type);

    private static HeroStats Potential(AccountGear item, HeroStats baseStats)
    {
        var result = default(HeroStats);
        foreach (var stat in item.Substats.Prepend(item.Main))
        {
            result += stat.Type switch
            {
                GearStatType.Attack => new HeroStats(stat.Value, 0, 0, 0, 0, 0, 0, 0),
                GearStatType.AttackPercent => new HeroStats(baseStats.Attack * stat.Value / 100, 0, 0, 0, 0, 0, 0, 0),
                GearStatType.Health => new HeroStats(0, stat.Value, 0, 0, 0, 0, 0, 0),
                GearStatType.HealthPercent => new HeroStats(0, baseStats.Health * stat.Value / 100, 0, 0, 0, 0, 0, 0),
                GearStatType.Defense => new HeroStats(0, 0, stat.Value, 0, 0, 0, 0, 0),
                GearStatType.DefensePercent => new HeroStats(0, 0, baseStats.Defense * stat.Value / 100, 0, 0, 0, 0, 0),
                GearStatType.Speed => new HeroStats(0, 0, 0, stat.Value, 0, 0, 0, 0),
                GearStatType.CriticalHitChancePercent => new HeroStats(0, 0, 0, 0, stat.Value, 0, 0, 0),
                GearStatType.CriticalHitDamagePercent => new HeroStats(0, 0, 0, 0, 0, stat.Value, 0, 0),
                GearStatType.EffectivenessPercent => new HeroStats(0, 0, 0, 0, 0, 0, stat.Value, 0),
                GearStatType.EffectResistancePercent => new HeroStats(0, 0, 0, 0, 0, 0, 0, stat.Value),
                _ => default,
            };
        }
        return result;
    }

    private static double ItemWeightedScore(AccountGear item, HeroStats baseStats, HeroStats weights)
    {
        var delta = Potential(item, baseStats);
        var pseudo = baseStats + delta;
        return HeroStatCalculator.CalculateWeightedScore(baseStats, pseudo, weights);
    }

    private static HeroStats[] BuildSuffixMax(Candidate[][] candidates)
    {
        var result = new HeroStats[candidates.Length + 1];
        // 套装上界故意放宽，保证剪枝不丢精确结果。
        var setAllowance = new HeroStats(10000, 50000, 5000, 50, 100, 100, 100, 100);
        result[^1] = setAllowance;
        for (var depth = candidates.Length - 1; depth >= 0; depth--)
        {
            var values = candidates[depth].Select(value => value.Potential).ToArray();
            result[depth] = result[depth + 1] + new HeroStats(
                values.Max(value => value.Attack), values.Max(value => value.Health),
                values.Max(value => value.Defense), values.Max(value => value.Speed),
                values.Max(value => value.CriticalChance), values.Max(value => value.CriticalDamage),
                values.Max(value => value.Effectiveness), values.Max(value => value.Resistance));
        }
        return result;
    }

    private static double[] BuildSuffixScore(Candidate[][] candidates)
    {
        var result = new double[candidates.Length + 1];
        for (var depth = candidates.Length - 1; depth >= 0; depth--)
            result[depth] = result[depth + 1] + candidates[depth].Max(value => value.Score);
        return result;
    }

    private static bool CanReachMinimums(HeroStats baseline, HeroStats selected, HeroStats remaining,
        IReadOnlyDictionary<GearStatType, StatRange> ranges)
    {
        foreach (var (type, range) in ranges)
        {
            if (!range.Minimum.HasValue)
                continue;
            var possible = GetStat(type, baseline) + GetStat(type, selected) + GetStat(type, remaining);
            if (possible < range.Minimum.Value)
                return false;
        }
        return true;
    }

    private static bool MatchesRanges(HeroStats stats, IReadOnlyDictionary<GearStatType, StatRange> ranges) =>
        ranges.All(pair => pair.Value.Contains(GetStat(pair.Key, stats)));

    private static double GetStat(GearStatType type, HeroStats stats) => type switch
    {
        GearStatType.Attack or GearStatType.AttackPercent => stats.Attack,
        GearStatType.Health or GearStatType.HealthPercent => stats.Health,
        GearStatType.Defense or GearStatType.DefensePercent => stats.Defense,
        GearStatType.Speed => stats.Speed,
        GearStatType.CriticalHitChancePercent => stats.CriticalChance,
        GearStatType.CriticalHitDamagePercent => stats.CriticalDamage,
        GearStatType.EffectivenessPercent => stats.Effectiveness,
        GearStatType.EffectResistancePercent => stats.Resistance,
        _ => 0,
    };

    private static bool CanReachRequiredSets(int depth, Candidate[][] candidates,
        IReadOnlyDictionary<string, int> required, IReadOnlyDictionary<string, int> counts)
    {
        foreach (var (set, activations) in required)
        {
            counts.TryGetValue(set, out var current);
            var possible = current;
            for (var index = depth; index < candidates.Length; index++)
                if (candidates[index].Any(value => value.Item.Set.Equals(set, StringComparison.OrdinalIgnoreCase)))
                    possible++;
            if (possible < EquipmentSetCatalog.RequiredPieces(set) * activations)
                return false;
        }
        return true;
    }

    private static bool HasRequiredSets(IReadOnlyDictionary<string, int> required,
        IReadOnlyDictionary<string, int> counts) => required.All(pair =>
        counts.TryGetValue(pair.Key, out var count) && count / EquipmentSetCatalog.RequiredPieces(pair.Key) >= pair.Value);

    private static OptimizationSearchResult Empty(Stopwatch stopwatch, bool complete)
    {
        var progress = new OptimizationProgress(0, 0, 0, stopwatch.Elapsed, !complete);
        return new OptimizationSearchResult([], progress, complete);
    }

    private sealed record Candidate(AccountGear Item, HeroStats Potential, double Score);

    private sealed class TopResults(int limit)
    {
        private readonly object _gate = new();
        private readonly PriorityQueue<OptimizationResult, ResultPriority> _queue = new(new ResultPriorityComparer());

        public bool IsFull { get { lock (_gate) return _queue.Count >= limit; } }
        public double Threshold { get { lock (_gate) return _queue.TryPeek(out _, out var priority) && _queue.Count >= limit ? priority.Score : double.NegativeInfinity; } }

        public void Add(OptimizationResult result)
        {
            var priority = new ResultPriority(result.Score, CreateKey(result));
            lock (_gate)
            {
                if (_queue.Count < limit)
                {
                    _queue.Enqueue(result, priority);
                    return;
                }
                _queue.TryPeek(out _, out var threshold);
                if (ResultPriorityComparer.Instance.Compare(priority, threshold) <= 0)
                    return;
                _queue.Dequeue();
                _queue.Enqueue(result, priority);
            }
        }

        public IReadOnlyList<OptimizationResult> Snapshot()
        {
            lock (_gate)
                return _queue.UnorderedItems.Select(value => value.Element).OrderByDescending(value => value.Score)
                    .ThenBy(CreateKey, StringComparer.Ordinal).ToArray();
        }

        private static string CreateKey(OptimizationResult result) =>
            string.Join(",", result.Equipment.Select(item => item.Id).OrderBy(id => id, StringComparer.Ordinal));

        private readonly record struct ResultPriority(double Score, string Key);

        private sealed class ResultPriorityComparer : IComparer<ResultPriority>
        {
            public static ResultPriorityComparer Instance { get; } = new();

            public int Compare(ResultPriority x, ResultPriority y)
            {
                var score = x.Score.CompareTo(y.Score);
                return score != 0 ? score : -StringComparer.Ordinal.Compare(x.Key, y.Key);
            }
        }
    }
}
