using System.Text.Json;

namespace TiezhuToolbox.Modules.Account;

public sealed class HeroCatalogEntry
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Attribute { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Rarity { get; set; }
    public HeroStats Level50FiveStar { get; set; }
    public HeroStats Level60SixStar { get; set; }
    public string? ImprintType { get; set; }
    public Dictionary<string, double> ImprintGrades { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ExclusiveEquipmentStat> ExclusiveEquipment { get; set; } = [];
    public HeroStats SpecialtyTreeBonus { get; set; }
    public bool SpecialtyTreeDataAvailable { get; set; }
}

public sealed record ExclusiveEquipmentStat(string Type, double Value);

public sealed class ArtifactCatalogEntry
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Rarity { get; set; }
    public double BaseAttack { get; set; }
    public double BaseHealth { get; set; }
    public double BaseDefense { get; set; }

    public HeroStats GetStats(int level)
    {
        level = Math.Clamp(level, 1, 30);
        var multiplier = 1 + 12 * (level / 30D);
        return new HeroStats(
            Math.Round(BaseAttack * multiplier, 1),
            Math.Round(BaseHealth * multiplier, 1),
            Math.Round(BaseDefense * multiplier, 1),
            0, 0, 0, 0, 0);
    }
}

public sealed class StaticGameData
{
    private readonly Dictionary<string, HeroCatalogEntry> _heroes;
    private readonly Dictionary<string, ArtifactCatalogEntry> _artifacts;
    private readonly Dictionary<string, string> _heroNamesZh;

    public StaticGameData(string? assetRoot = null)
    {
        var root = assetRoot ?? Path.Combine(AppContext.BaseDirectory, "Assets", "OptimizerData");
        _heroes = Load<List<HeroCatalogEntry>>(Path.Combine(root, "hero-catalog.json"))
            .Where(value => !string.IsNullOrWhiteSpace(value.Code))
            .GroupBy(value => value.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _artifacts = Load<List<ArtifactCatalogEntry>>(Path.Combine(root, "artifact-catalog.json"))
            .Where(value => !string.IsNullOrWhiteSpace(value.Code))
            .GroupBy(value => value.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _heroNamesZh = new Dictionary<string, string>(
            Load<Dictionary<string, string>>(Path.Combine(root, "hero-names-zh.json")),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<HeroCatalogEntry> Heroes => _heroes.Values;
    public IReadOnlyCollection<ArtifactCatalogEntry> Artifacts => _artifacts.Values;

    public bool TryGetHero(string code, out HeroCatalogEntry hero) => _heroes.TryGetValue(code, out hero!);

    // 英雄中文显示名；无翻译时回退原名（英文）。
    public string DisplayHeroName(string code, string? fallback = null) =>
        _heroNamesZh.TryGetValue(code, out var name) ? name : (fallback ?? code);
    public bool TryGetArtifact(string? code, out ArtifactCatalogEntry artifact)
    {
        if (code != null && _artifacts.TryGetValue(code, out artifact!))
            return true;
        artifact = null!;
        return false;
    }

    private static T Load<T>(string path) where T : new()
    {
        if (!File.Exists(path))
            return new T();
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), AppPaths.JsonOptions) ?? new T();
    }
}

public sealed record SetBonusDefinition(string Set, string DisplayName, int Pieces, HeroStats Bonus,
    double AttackPercent = 0, double HealthPercent = 0, double DefensePercent = 0, double SpeedPercent = 0);

public static class EquipmentSetCatalog
{
    public static readonly IReadOnlyDictionary<string, SetBonusDefinition> All =
        new Dictionary<string, SetBonusDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["AttackSet"] = new("AttackSet", "攻击", 4, default, AttackPercent: 45),
            ["HealthSet"] = new("HealthSet", "生命值", 2, default, HealthPercent: 20),
            ["DefenseSet"] = new("DefenseSet", "守护", 2, default, DefensePercent: 20),
            ["SpeedSet"] = new("SpeedSet", "速度", 4, default, SpeedPercent: 25),
            ["CriticalSet"] = new("CriticalSet", "暴击", 2, new HeroStats(0, 0, 0, 0, 12, 0, 0, 0)),
            ["HitSet"] = new("HitSet", "命中", 2, new HeroStats(0, 0, 0, 0, 0, 0, 20, 0)),
            ["ResistSet"] = new("ResistSet", "抵抗", 2, new HeroStats(0, 0, 0, 0, 0, 0, 0, 20)),
            ["DestructionSet"] = new("DestructionSet", "破灭", 4, new HeroStats(0, 0, 0, 0, 0, 60, 0, 0)),
            ["LifestealSet"] = new("LifestealSet", "吸血", 4, default),
            ["CounterSet"] = new("CounterSet", "反击", 4, default),
            ["UnitySet"] = new("UnitySet", "夹攻", 2, default),
            ["RageSet"] = new("RageSet", "愤怒", 4, default),
            ["ImmunitySet"] = new("ImmunitySet", "免疫", 2, default),
            ["PenetrationSet"] = new("PenetrationSet", "穿透", 2, default),
            ["RevengeSet"] = new("RevengeSet", "复仇", 4, default, SpeedPercent: 12),
            ["InjurySet"] = new("InjurySet", "创伤", 4, default),
            ["ProtectionSet"] = new("ProtectionSet", "防护", 4, default),
            ["TorrentSet"] = new("TorrentSet", "激流", 2, default, HealthPercent: -10),
            ["ReversalSet"] = new("ReversalSet", "逆袭", 4, default, SpeedPercent: 15),
            ["RiposteSet"] = new("RiposteSet", "回击", 4, default),
            ["WarfareSet"] = new("WarfareSet", "开战", 4, default, HealthPercent: 20),
            ["PursuitSet"] = new("PursuitSet", "追击", 2, default),
            ["WeakeningSet"] = new("WeakeningSet", "弱化", 4, default, SpeedPercent: 15),
            ["FervorSet"] = new("FervorSet", "全力", 2, default),
        };

    public static string DisplayName(string set) => All.TryGetValue(set, out var value) ? value.DisplayName : set;

    public static int RequiredPieces(string set) => All.TryGetValue(set, out var value) ? value.Pieces : 2;
}

// 英雄属性（元素）中文显示；数据里自然属性写作 wind，earth 作兼容别名。
public static class HeroAttributeCatalog
{
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Options =
        new KeyValuePair<string, string>[]
        {
            new("fire", "火焰"),
            new("ice", "寒气"),
            new("wind", "自然"),
            new("light", "光明"),
            new("dark", "黑暗"),
        };

    private static readonly IReadOnlyDictionary<string, string> All =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fire"] = "火焰",
            ["ice"] = "寒气",
            ["wind"] = "自然",
            ["earth"] = "自然",
            ["light"] = "光明",
            ["dark"] = "黑暗",
        };

    public static string DisplayName(string attribute) => All.TryGetValue(attribute, out var value) ? value : attribute;
}

// 英雄职业中文显示；manauser 即精灵师。
public static class HeroRoleCatalog
{
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Options =
        new KeyValuePair<string, string>[]
        {
            new("warrior", "战士"),
            new("knight", "骑士"),
            new("assassin", "盗贼"),
            new("ranger", "游侠"),
            new("mage", "魔导师"),
            new("manauser", "精灵师"),
        };

    private static readonly IReadOnlyDictionary<string, string> All =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["warrior"] = "战士",
            ["knight"] = "骑士",
            ["assassin"] = "盗贼",
            ["ranger"] = "游侠",
            ["mage"] = "魔导师",
            ["manauser"] = "精灵师",
        };

    public static string DisplayName(string role) => All.TryGetValue(role, out var value) ? value : role;
}
