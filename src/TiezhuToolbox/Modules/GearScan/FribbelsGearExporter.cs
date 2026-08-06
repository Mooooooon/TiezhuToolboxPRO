using System.Text.Json;
using System.Text.Json.Nodes;

namespace TiezhuToolbox.Modules.GearScan;

public sealed record GearScanResult(
    string GearText,
    int ItemCount,
    int HeroCount,
    int LevelZeroItemCount,
    int InferredLevelItemCount = 0);

public enum GearScanHeroFilter
{
    All,
    AtLeastFiveStarsFiveAwakened,
    SixStarsSixAwakened,
}

/// <summary>将游戏原始对象转换为 Fribbels 导入器可直接读取的 gear.txt。</summary>
public sealed class FribbelsGearExporter
{
    private static readonly string[] Ranks = ["Unknown", "Normal", "Good", "Rare", "Heroic", "Epic"];

    private static readonly IReadOnlyDictionary<string, string> StatTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["att_rate"] = "AttackPercent",
        ["max_hp_rate"] = "HealthPercent",
        ["def_rate"] = "DefensePercent",
        ["att"] = "Attack",
        ["max_hp"] = "Health",
        ["def"] = "Defense",
        ["speed"] = "Speed",
        ["res"] = "EffectResistancePercent",
        ["cri"] = "CriticalHitChancePercent",
        ["cri_dmg"] = "CriticalHitDamagePercent",
        ["acc"] = "EffectivenessPercent",
        ["coop"] = "DualAttackChancePercent",
    };

    private static readonly IReadOnlyDictionary<string, string> GearTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["weapon"] = "Weapon", ["helm"] = "Helmet", ["armor"] = "Armor",
        ["neck"] = "Necklace", ["ring"] = "Ring", ["boot"] = "Boots",
    };

    private static readonly IReadOnlyDictionary<char, string> GearLetters = new Dictionary<char, string>
    {
        ['w'] = "Weapon", ['h'] = "Helmet", ['a'] = "Armor",
        ['n'] = "Necklace", ['r'] = "Ring", ['b'] = "Boots",
    };

    private static readonly IReadOnlyDictionary<string, string> Sets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["set_acc"] = "HitSet", ["set_att"] = "AttackSet", ["set_coop"] = "UnitySet",
        ["set_counter"] = "CounterSet", ["set_cri_dmg"] = "DestructionSet", ["set_cri"] = "CriticalSet",
        ["set_def"] = "DefenseSet", ["set_immune"] = "ImmunitySet", ["set_max_hp"] = "HealthSet",
        ["set_penetrate"] = "PenetrationSet", ["set_rage"] = "RageSet", ["set_res"] = "ResistSet",
        ["set_revenge"] = "RevengeSet", ["set_scar"] = "InjurySet", ["set_speed"] = "SpeedSet",
        ["set_vampire"] = "LifestealSet", ["set_shield"] = "ProtectionSet", ["set_torrent"] = "TorrentSet",
        ["set_revenant"] = "ReversalSet", ["set_riposte"] = "RiposteSet", ["set_chase"] = "PursuitSet",
        ["set_opener"] = "WarfareSet", ["set_weak"] = "WeakeningSet", ["set_might"] = "FervorSet",
    };

    /// <summary>供离线回归验证原始解析结果到 gear.txt 的转换，不发起网络请求。</summary>
    public static GearScanResult ConvertParserResponse(
        string responseText,
        int minimumEnhance,
        GearScanHeroFilter heroFilter = GearScanHeroFilter.All)
    {
        var root = JsonNode.Parse(responseText) as JsonObject
            ?? throw new InvalidDataException("测试响应不是 JSON 对象");
        var items = new JsonArray();
        foreach (var node in root["data"] as JsonArray ?? [])
        {
            if (node is not JsonObject raw || string.IsNullOrWhiteSpace(GetString(raw, "f")))
                continue;
            var item = (JsonObject)raw.DeepClone();
            ConvertItem(item);
            if (GetInt(item, "enhance") >= minimumEnhance)
                items.Add(item);
        }
        var heroes = ConvertHeroes(root["units"] as JsonArray, heroFilter);
        var text = new JsonObject { ["items"] = items, ["heroes"] = heroes }
            .ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return new GearScanResult(text, items.Count, heroes.Count,
            items.Count(node => node is JsonObject item && GetInt(item, "level") == 0));
    }

    private static void ConvertItem(JsonObject item)
    {
        var gear = ResolveGear(item);
        if (gear != null)
            item["gear"] = gear;

        var grade = GetInt(item, "g");
        var rank = grade >= 0 && grade < Ranks.Length ? Ranks[grade] : "Unknown";
        item["rank"] = rank;

        var setCode = GetString(item, "f");
        if (setCode != null && Sets.TryGetValue(setCode, out var set))
            item["set"] = set;

        item["name"] = string.IsNullOrWhiteSpace(GetString(item, "name")) ? "Unknown" : GetString(item, "name");
        item["level"] = GetInt(item, "level");

        var operations = item["op"] as JsonArray ?? [];
        var countByRank = rank switch { "Normal" => 5, "Good" => 6, "Rare" => 7, "Heroic" => 8, "Epic" => 9, _ => 0 };
        var offsetByRank = rank switch { "Good" => 1, "Rare" => 2, "Heroic" => 3, "Epic" => 4, _ => 0 };
        var operationCount = Math.Min(Math.Max(operations.Count - 1, 0), countByRank);
        item["enhance"] = Math.Max((operationCount - offsetByRank) * 3, 0);
        item["main"] = ConvertMainStat(item, operations);
        item["substats"] = ConvertSubstats(operations);

        item["ingameId"] = item["id"]?.DeepClone();
        item["ingameEquippedId"] = item["p"]?.ToString() ?? string.Empty;
    }

    private static JsonObject ConvertMainStat(JsonObject item, JsonArray operations)
    {
        var operation = operations.FirstOrDefault() as JsonArray;
        var rawType = operation?[0]?.GetValue<string>();
        var type = rawType != null && StatTypes.TryGetValue(rawType, out var mapped) ? mapped : "Unknown";
        var rawValue = GetDouble(item, "mainStatValue");
        var value = rawType != null && IsFlat(rawType) ? rawValue : RoundTenth(rawValue * 100);
        return new JsonObject { ["type"] = type, ["value"] = value };
    }

    private static JsonArray ConvertSubstats(JsonArray operations)
    {
        var order = new List<string>();
        var stats = new Dictionary<string, StatAccumulator>(StringComparer.Ordinal);
        foreach (var node in operations.Skip(1))
        {
            if (node is not JsonArray operation || operation.Count < 2)
                continue;
            var rawType = operation[0]?.GetValue<string>();
            if (rawType == null || !StatTypes.TryGetValue(rawType, out var type))
                continue;
            var rawValue = GetNodeDouble(operation[1]);
            var value = IsFlat(rawType) ? rawValue : RoundTenth(rawValue * 100);
            var annotation = operation.Count > 2 ? operation[2]?.GetValue<string>() : null;
            if (!stats.TryGetValue(type, out var accumulator))
            {
                accumulator = new StatAccumulator(value, 1, false);
                stats.Add(type, accumulator);
                order.Add(type);
                continue;
            }

            accumulator.Value += value;
            if (annotation == "c")
                accumulator.Modified = true;
            else if (annotation != "u")
                accumulator.Rolls++;
        }

        var result = new JsonArray();
        foreach (var type in order)
        {
            var stat = stats[type];
            var node = new JsonObject { ["type"] = type, ["value"] = stat.Value, ["rolls"] = stat.Rolls };
            if (stat.Modified)
                node["modified"] = true;
            result.Add(node);
        }
        return result;
    }

    private static JsonArray ConvertHeroes(JsonArray? unitGroups, GearScanHeroFilter filter)
    {
        var longest = unitGroups?
            .OfType<JsonArray>()
            .OrderByDescending(group => group.Count)
            .FirstOrDefault();
        var heroes = new JsonArray();
        if (longest == null)
            return heroes;

        foreach (var node in longest)
        {
            if (node is not JsonObject raw || string.IsNullOrWhiteSpace(GetString(raw, "name")) || raw["id"] == null)
                continue;
            var stars = GetInt(raw, "g");
            var awaken = GetInt(raw, "z");
            var included = filter switch
            {
                GearScanHeroFilter.AtLeastFiveStarsFiveAwakened => stars >= 5 && awaken >= 5,
                GearScanHeroFilter.SixStarsSixAwakened => stars >= 6 && awaken >= 6,
                _ => true,
            };
            if (!included)
                continue;
            var hero = (JsonObject)raw.DeepClone();
            hero["stars"] = hero["g"]?.DeepClone();
            hero["awaken"] = hero["z"]?.DeepClone();
            heroes.Add(hero);
        }
        return heroes;
    }

    private static string? ResolveGear(JsonObject item)
    {
        var type = GetString(item, "type");
        if (type != null && GearTypes.TryGetValue(type, out var gear))
            return gear;
        var code = GetString(item, "code");
        if (string.IsNullOrEmpty(code))
            return null;
        foreach (var segment in code.Split('_').Reverse())
        {
            if (segment.Length == 1 && GearLetters.TryGetValue(segment[0], out gear))
                return gear;
        }
        var prefix = code.Split('_')[0];
        return GearLetters.TryGetValue(prefix[^1], out gear) ? gear : null;
    }

    private static bool IsFlat(string type) => type is "max_hp" or "speed" or "att" or "def";

    private static double RoundTenth(double value) => Math.Floor(value * 10 + 0.5) / 10;

    private static string? GetString(JsonObject value, string property) =>
        value[property] is JsonValue node && node.TryGetValue<string>(out var result) ? result : null;

    private static int GetInt(JsonObject value, string property)
    {
        if (value[property] is not JsonValue node)
            return 0;
        if (node.TryGetValue<int>(out var intValue))
            return intValue;
        return node.TryGetValue<double>(out var doubleValue) ? (int)doubleValue : 0;
    }

    private static double GetDouble(JsonObject value, string property) => GetNodeDouble(value[property]);

    private static double GetNodeDouble(JsonNode? node)
    {
        if (node is not JsonValue value)
            return 0;
        if (value.TryGetValue<double>(out var doubleValue))
            return doubleValue;
        return value.TryGetValue<int>(out var intValue) ? intValue : 0;
    }

    private sealed class StatAccumulator(double value, int rolls, bool modified)
    {
        public double Value { get; set; } = value;
        public int Rolls { get; set; } = rolls;
        public bool Modified { get; set; } = modified;
    }
}
