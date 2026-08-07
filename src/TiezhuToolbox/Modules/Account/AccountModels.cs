namespace TiezhuToolbox.Modules.Account;

public enum GearSlot
{
    Weapon,
    Helmet,
    Armor,
    Necklace,
    Ring,
    Boots,
}

public enum GearStatType
{
    Attack,
    AttackPercent,
    Health,
    HealthPercent,
    Defense,
    DefensePercent,
    Speed,
    CriticalHitChancePercent,
    CriticalHitDamagePercent,
    EffectivenessPercent,
    EffectResistancePercent,
}

public sealed record GearStat(GearStatType Type, double Value, int Rolls = 0, bool Modified = false);

public sealed class AccountGear
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Unknown";
    public GearSlot Slot { get; set; }
    public string Set { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Enhance { get; set; }
    public GearStat Main { get; set; } = new(GearStatType.Attack, 0);
    public List<GearStat> Substats { get; set; } = [];
    public string EquippedHeroId { get; set; } = string.Empty;
    public bool Storage { get; set; }
}

public sealed class AccountHero
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; } = 60;
    public int Stars { get; set; } = 6;
    public int Awaken { get; set; } = 6;
    public bool UsedFallbackProgression { get; set; }
    public string? ArtifactCode { get; set; }
    public int? ArtifactLevel { get; set; }
}

public sealed class AccountSnapshotV1
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.Now;
    public string Source { get; set; } = string.Empty;
    public List<AccountGear> Items { get; set; } = [];
    public List<AccountHero> Heroes { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class HeroPreference
{
    public string HeroId { get; set; } = string.Empty;
    public string HeroCode { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? ImprintGrade { get; set; }
    public int ExclusiveEquipmentIndex { get; set; } = -1;
    public string? ArtifactCode { get; set; }
    public int ArtifactLevel { get; set; } = 30;
    public bool MaxSpecialtyTree { get; set; } = true;
}

public sealed class HeroPreferenceDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<HeroPreference> Active { get; set; } = [];
    public List<HeroPreference> History { get; set; } = [];
}

public readonly record struct HeroStats(
    double Attack,
    double Health,
    double Defense,
    double Speed,
    double CriticalChance,
    double CriticalDamage,
    double Effectiveness,
    double Resistance)
{
    public static HeroStats operator +(HeroStats a, HeroStats b) => new(
        a.Attack + b.Attack,
        a.Health + b.Health,
        a.Defense + b.Defense,
        a.Speed + b.Speed,
        a.CriticalChance + b.CriticalChance,
        a.CriticalDamage + b.CriticalDamage,
        a.Effectiveness + b.Effectiveness,
        a.Resistance + b.Resistance);
}

public sealed record HeroPanelResult(
    HeroStats BaseStats,
    HeroStats RawStats,
    double CriticalChanceOverflow,
    double CriticalDamageOverflow,
    IReadOnlyList<string> ActiveSets,
    string? Warning);

