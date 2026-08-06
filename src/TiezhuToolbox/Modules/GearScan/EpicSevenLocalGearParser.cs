using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TiezhuToolbox.Modules.GearScan;

/// <summary>完全在本机解码游戏响应并生成 Fribbels 可导入数据。</summary>
public sealed class EpicSevenLocalGearParser
{
    private static readonly double[] MainStatMultipliers = [1, 1.6, 2.2, 2.8, 3.6, 5];

    private readonly IReadOnlyDictionary<string, string> _heroNames;
    private readonly IReadOnlyDictionary<string, int> _equipmentLevels;
    private readonly IReadOnlyDictionary<string, string> _equipmentTypes;

    public EpicSevenLocalGearParser(string? assetRoot = null)
    {
        var root = assetRoot ?? Path.Combine(AppContext.BaseDirectory, "Assets", "GearScan");
        _heroNames = LoadDictionary<string>(Path.Combine(root, "hero-names.json"));
        _equipmentLevels = LoadDictionary<int>(Path.Combine(root, "equipment-levels.json"));
        _equipmentTypes = LoadDictionary<string>(Path.Combine(root, "equipment-types.json"));
    }

    public GearScanResult Parse(
        string pcapngPath,
        int minimumEnhance,
        GearScanHeroFilter heroFilter = GearScanHeroFilter.All)
    {
        if (minimumEnhance is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(minimumEnhance));

        Dictionary<string, object?>? accountData = null;
        var storageItems = new List<Dictionary<string, object?>>();
        foreach (var payload in EpicSevenTransportDecoder.DecodeServerPayloads(pcapngPath))
        {
            var unpacked = Lz4BlockDecoder.DecodeGamePayload(payload);
            if (new MessagePackReader(unpacked).ReadDocument() is not Dictionary<string, object?> response)
                continue;
            if (response.TryGetValue("account_data", out var accountValue)
                && accountValue is Dictionary<string, object?> account)
                accountData = account;
            if (response.TryGetValue("equip_storage", out var storageValue)
                && storageValue is Dictionary<string, object?> storage)
                storageItems.AddRange(storage.Values.OfType<Dictionary<string, object?>>());
        }

        if (accountData == null)
            throw new InvalidDataException("本地解析未找到登录账号数据；请从游戏完全关闭状态开始扫描，并等待进入大厅后再停止");
        if (!accountData.TryGetValue("equips", out var equipsValue)
            || equipsValue is not Dictionary<string, object?> equips)
            throw new InvalidDataException("本地解析未找到装备数据");
        if (!accountData.TryGetValue("units", out var unitsValue)
            || unitsValue is not Dictionary<string, object?> units)
            throw new InvalidDataException("本地解析未找到英雄数据");

        var items = new JsonArray();
        var equipmentIds = new HashSet<string>(StringComparer.Ordinal);
        var inferredLevelItemCount = 0;
        foreach (var raw in equips.Values.OfType<Dictionary<string, object?>>())
            AddEquipment(items, equipmentIds, raw, storage: false, minimumEnhance, ref inferredLevelItemCount);
        foreach (var raw in storageItems)
            AddEquipment(items, equipmentIds, raw, storage: true, minimumEnhance, ref inferredLevelItemCount);

        var heroes = new JsonArray();
        foreach (var raw in units.Values.OfType<Dictionary<string, object?>>())
        {
            var code = GetString(raw, "code");
            if (code == null || !_heroNames.TryGetValue(code, out var name))
                continue;
            var hero = ConvertMap(raw);
            hero["name"] = name;
            heroes.Add(hero);
        }

        var responseRoot = new JsonObject
        {
            ["status"] = "SUCCESS",
            ["data"] = items,
            ["units"] = new JsonArray(heroes),
        };
        var result = FribbelsGearExporter.ConvertParserResponse(
            responseRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
            minimumEnhance,
            heroFilter);
        if (result.ItemCount == 0)
            throw new InvalidDataException($"解析成功，但没有找到强化等级不低于 +{minimumEnhance} 的装备");
        return result with { InferredLevelItemCount = inferredLevelItemCount };
    }

    private void AddEquipment(
        JsonArray output,
        HashSet<string> equipmentIds,
        Dictionary<string, object?> raw,
        bool storage,
        int minimumEnhance,
        ref int inferredLevelItemCount)
    {
        if (string.IsNullOrWhiteSpace(GetString(raw, "f")))
            return;
        var id = raw.TryGetValue("id", out var idValue)
            ? Convert.ToString(idValue, CultureInfo.InvariantCulture)
            : null;
        if (string.IsNullOrEmpty(id) || !equipmentIds.Add(id))
            return;
        var item = ConvertMap(raw);
        var code = GetString(raw, "code");
        var (level, inferred) = ResolveLevel(code);
        if (inferred && ResolveEnhance(raw) >= minimumEnhance)
            inferredLevelItemCount++;
        item["level"] = level;
        if (code != null && _equipmentTypes.TryGetValue(code, out var type))
            item["type"] = type;
        item["name"] = "Unknown";
        item["mainStatValue"] = ResolveMainStatValue(raw, level);
        if (storage)
            item["storage"] = true;
        output.Add(item);
    }

    private (int Level, bool Inferred) ResolveLevel(string? code)
    {
        if (code != null && _equipmentLevels.TryGetValue(code, out var known) && known > 0)
            return (known, false);
        return (88, true);
    }

    private static double ResolveMainStatValue(Dictionary<string, object?> raw, int level)
    {
        var baseValue = level == 88 ? GetLevel88MainBase(GetMainType(raw)) : GetMainBase(raw);
        if (!raw.TryGetValue("op", out var operationsValue) || operationsValue is not List<object?> operations)
            return baseValue;
        var milestone = ResolveEnhance(raw) / 3;
        return baseValue * MainStatMultipliers[milestone];
    }

    private static int ResolveEnhance(Dictionary<string, object?> raw)
    {
        if (!raw.TryGetValue("op", out var operationsValue) || operationsValue is not List<object?> operations)
            return 0;
        var grade = GetInt(raw, "g");
        var rankOffset = grade switch { 2 => 1, 3 => 2, 4 => 3, 5 => 4, _ => 0 };
        var rankLimit = grade switch { 1 => 5, 2 => 6, 3 => 7, 4 => 8, 5 => 9, _ => 0 };
        var operationCount = Math.Min(Math.Max(operations.Count - 1, 0), rankLimit);
        return Math.Clamp(operationCount - rankOffset, 0, 5) * 3;
    }

    private static double GetLevel88MainBase(string? type) => type switch
    {
        "att" => 103,
        "max_hp" => 553,
        "def" => 62,
        "speed" => 9,
        "cri" => 0.12,
        "cri_dmg" => 0.14,
        "att_rate" or "max_hp_rate" or "def_rate" or "acc" or "res" => 0.13,
        _ => 0,
    };

    private static string? GetMainType(Dictionary<string, object?> raw) =>
        raw.TryGetValue("op", out var value)
        && value is List<object?> { Count: > 0 } operations
        && operations[0] is List<object?> { Count: > 0 } main
            ? main[0] as string
            : null;

    private static double GetMainBase(Dictionary<string, object?> raw) =>
        raw.TryGetValue("op", out var value)
        && value is List<object?> { Count: > 0 } operations
        && operations[0] is List<object?> { Count: > 1 } main
            ? ToDouble(main[1])
            : 0;

    private static int GetInt(Dictionary<string, object?> value, string key) =>
        value.TryGetValue(key, out var raw) ? checked((int)ToDouble(raw)) : 0;

    private static string? GetString(Dictionary<string, object?> value, string key) =>
        value.TryGetValue(key, out var raw) ? raw as string : null;

    private static double ToDouble(object? value) => value switch
    {
        byte number => number,
        short number => number,
        int number => number,
        long number => number,
        ushort number => number,
        uint number => number,
        ulong number => number,
        float number => number,
        double number => number,
        decimal number => (double)number,
        _ => 0,
    };

    private static JsonObject ConvertMap(Dictionary<string, object?> source)
    {
        var result = new JsonObject();
        foreach (var (key, value) in source)
            result[key] = ConvertValue(value);
        return result;
    }

    private static JsonNode? ConvertValue(object? value) => value switch
    {
        null => null,
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        byte[] bytes => JsonValue.Create(Convert.ToBase64String(bytes)),
        long number => JsonValue.Create(number),
        ulong number => JsonValue.Create(number),
        float number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        Dictionary<string, object?> map => ConvertMap(map),
        List<object?> list => new JsonArray(list.Select(ConvertValue).ToArray()),
        MessagePackExtension extension => JsonValue.Create(Convert.ToBase64String(extension.Data)),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    private static IReadOnlyDictionary<string, TValue> LoadDictionary<TValue>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("本地装备解析数据缺失", path);
        return JsonSerializer.Deserialize<Dictionary<string, TValue>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("本地装备解析数据格式无效：" + path);
    }
}
