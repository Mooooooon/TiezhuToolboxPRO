using TiezhuToolbox.Modules.Account;

namespace TiezhuToolbox.Modules.Optimizer;

public enum EquipmentOccupationMode
{
    All,
    ProtectHigherPriority,
    UnequippedOrTarget,
}

public readonly record struct StatRange(double? Minimum, double? Maximum)
{
    public bool Contains(double value) =>
        (!Minimum.HasValue || value >= Minimum.Value) && (!Maximum.HasValue || value <= Maximum.Value);
}

public sealed class OptimizationRequest
{
    public required AccountHero Hero { get; init; }
    public HeroPreference? HeroPreference { get; init; }
    public required IReadOnlyList<AccountGear> Equipment { get; init; }
    public required IReadOnlyList<HeroPreference> HeroPriorities { get; init; }
    public EquipmentOccupationMode OccupationMode { get; init; } = EquipmentOccupationMode.All;
    public Dictionary<GearStatType, StatRange> StatRanges { get; init; } = [];
    public HeroStats Weights { get; init; } = new(1, 1, 1, 1, 1, 1, 1, 1);
    public List<string> RequiredSets { get; init; } = [];
    public Dictionary<GearSlot, HashSet<GearStatType>> AllowedMainStats { get; init; } = [];
    public int ResultLimit { get; init; } = 200;
    public CancellationToken CancellationToken { get; init; }
}

public sealed record OptimizationResult(
    IReadOnlyList<AccountGear> Equipment,
    HeroPanelResult Panel,
    double Score,
    int ConflictCount,
    IReadOnlyList<string> ConflictHeroIds);

public sealed record OptimizationProgress(
    long CheckedCombinations,
    long PrunedBranches,
    double CurrentThreshold,
    TimeSpan Elapsed,
    bool IsIncomplete);

public sealed record OptimizationSearchResult(
    IReadOnlyList<OptimizationResult> Results,
    OptimizationProgress Progress,
    bool IsComplete);

public sealed class OptimizerPresetDocument
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<OptimizerPreset> Presets { get; set; } = [];
}

public sealed class OptimizerPreset
{
    public string Name { get; set; } = string.Empty;
    public string HeroId { get; set; } = string.Empty;
    public EquipmentOccupationMode OccupationMode { get; set; }
    public Dictionary<GearStatType, StatRange> StatRanges { get; set; } = [];
    public HeroStats Weights { get; set; }
    public List<string> RequiredSets { get; set; } = [];
    public Dictionary<GearSlot, HashSet<GearStatType>> AllowedMainStats { get; set; } = [];
}
