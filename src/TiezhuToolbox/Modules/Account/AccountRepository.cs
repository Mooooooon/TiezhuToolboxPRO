using System.Globalization;
using System.Text.Json;

namespace TiezhuToolbox.Modules.Account;

public sealed class AccountImportException(string message) : Exception(message);

public static class AccountImportService
{
    private static readonly Dictionary<string, GearSlot> Slots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Weapon"] = GearSlot.Weapon,
        ["Helmet"] = GearSlot.Helmet,
        ["Armor"] = GearSlot.Armor,
        ["Necklace"] = GearSlot.Necklace,
        ["Ring"] = GearSlot.Ring,
        ["Boots"] = GearSlot.Boots,
    };

    private static readonly Dictionary<string, GearStatType> Stats = Enum.GetValues<GearStatType>()
        .ToDictionary(value => value.ToString(), StringComparer.OrdinalIgnoreCase);

    public static AccountSnapshotV1 Parse(string gearText, string source, bool allowMissingOwners = false)
    {
        using var document = JsonDocument.Parse(gearText);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new AccountImportException("导入内容必须是 JSON 对象");
        if (!root.TryGetProperty("items", out var itemsNode) || itemsNode.ValueKind != JsonValueKind.Array)
            throw new AccountImportException("缺少 items 装备数组");
        if (!root.TryGetProperty("heroes", out var heroesNode) || heroesNode.ValueKind != JsonValueKind.Array)
            throw new AccountImportException("缺少 heroes 英雄数组");

        var snapshot = new AccountSnapshotV1 { Source = source, ImportedAt = DateTimeOffset.Now };
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in itemsNode.EnumerateArray())
        {
            var item = ParseItem(node);
            if (!itemIds.Add(item.Id))
                throw new AccountImportException($"装备 ID 重复：{item.Id}");
            snapshot.Items.Add(item);
        }

        var heroIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in heroesNode.EnumerateArray())
        {
            var hero = ParseHero(node, snapshot.Warnings);
            if (!heroIds.Add(hero.Id))
                throw new AccountImportException($"英雄 ID 重复：{hero.Id}");
            snapshot.Heroes.Add(hero);
        }

        foreach (var item in snapshot.Items.Where(item => !string.IsNullOrEmpty(item.EquippedHeroId)))
        {
            // 装备扫描按英雄过滤导入时，归属被过滤英雄的装备属预期，不再逐条提示。
            if (!heroIds.Contains(item.EquippedHeroId) && !allowMissingOwners)
                snapshot.Warnings.Add($"装备 {item.Id} 的持有英雄 {item.EquippedHeroId} 不在导入英雄列表中，已保留归属 ID");
        }
        return snapshot;
    }

    private static AccountGear ParseItem(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
            throw new AccountImportException("装备条目不是对象");
        var id = GetText(node, "ingameId") ?? GetText(node, "id")
            ?? throw new AccountImportException("装备缺少 ingameId");
        if (string.IsNullOrWhiteSpace(id))
            throw new AccountImportException("装备 ID 不能为空");
        var slotText = GetText(node, "gear") ?? string.Empty;
        if (!Slots.TryGetValue(slotText, out var slot))
            throw new AccountImportException($"装备 {id} 的部位无效：{slotText}");
        var set = GetText(node, "set") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(set))
            throw new AccountImportException($"装备 {id} 缺少套装");
        if (!node.TryGetProperty("main", out var mainNode))
            throw new AccountImportException($"装备 {id} 缺少主属性");

        var equippedHeroId = GetText(node, "ingameEquippedId") ?? string.Empty;
        if (equippedHeroId == "0")
            equippedHeroId = string.Empty;
        var item = new AccountGear
        {
            Id = id,
            Name = GetText(node, "name") ?? "Unknown",
            Slot = slot,
            Set = set,
            Rank = GetText(node, "rank") ?? string.Empty,
            Level = GetInt(node, "level"),
            Enhance = GetInt(node, "enhance"),
            Main = ParseStat(mainNode, id),
            EquippedHeroId = equippedHeroId,
            Storage = GetBool(node, "storage"),
        };
        if (item.Level is < 0 or > 100 || item.Enhance is < 0 or > 15)
            throw new AccountImportException($"装备 {id} 的等级或强化值越界");
        if (!IsValidMainStat(item.Slot, item.Main.Type))
            throw new AccountImportException($"装备 {id} 的 {item.Slot} 主属性无效：{item.Main.Type}");
        if (node.TryGetProperty("substats", out var substats) && substats.ValueKind == JsonValueKind.Array)
            item.Substats.AddRange(substats.EnumerateArray().Select(stat => ParseStat(stat, id)));
        if (item.Substats.Count > 4)
            throw new AccountImportException($"装备 {id} 的副属性超过四条");
        return item;
    }

    private static bool IsValidMainStat(GearSlot slot, GearStatType type) => slot switch
    {
        GearSlot.Weapon => type == GearStatType.Attack,
        GearSlot.Helmet => type == GearStatType.Health,
        GearSlot.Armor => type == GearStatType.Defense,
        GearSlot.Necklace => type is GearStatType.Attack or GearStatType.Health or GearStatType.Defense
            or GearStatType.AttackPercent or GearStatType.HealthPercent or GearStatType.DefensePercent
            or GearStatType.CriticalHitChancePercent or GearStatType.CriticalHitDamagePercent,
        GearSlot.Ring => type is GearStatType.Attack or GearStatType.Health or GearStatType.Defense
            or GearStatType.AttackPercent or GearStatType.HealthPercent or GearStatType.DefensePercent
            or GearStatType.EffectivenessPercent or GearStatType.EffectResistancePercent,
        GearSlot.Boots => type is GearStatType.Attack or GearStatType.Health or GearStatType.Defense
            or GearStatType.AttackPercent or GearStatType.HealthPercent or GearStatType.DefensePercent or GearStatType.Speed,
        _ => false,
    };

    private static AccountHero ParseHero(JsonElement node, List<string> warnings)
    {
        if (node.ValueKind != JsonValueKind.Object)
            throw new AccountImportException("英雄条目不是对象");
        var id = GetText(node, "id") ?? throw new AccountImportException("英雄缺少 id");
        if (string.IsNullOrWhiteSpace(id))
            throw new AccountImportException("英雄 ID 不能为空");
        var code = GetText(node, "code") ?? string.Empty;
        var name = GetText(node, "name") ?? code;
        if (string.IsNullOrWhiteSpace(code))
            warnings.Add($"英雄 {name}（{id}）缺少代码，无法计算面板");

        var level = GetFirstInt(node, "level", "lv");
        var stars = GetFirstInt(node, "stars", "g");
        var awaken = GetFirstInt(node, "awaken", "z");
        var fallback = level <= 0 || stars is < 1 or > 6 || awaken is < 0 or > 6;
        if (fallback)
        {
            level = 60;
            stars = 6;
            awaken = 6;
            warnings.Add($"英雄 {name}（{id}）养成字段不完整，已按 60 级六星满觉计算");
        }
        return new AccountHero
        {
            Id = id,
            Code = code,
            Name = name,
            Level = level,
            Stars = stars,
            Awaken = awaken,
            UsedFallbackProgression = fallback,
            ArtifactCode = GetText(node, "artifactCode") ?? GetText(node, "artifact"),
            ArtifactLevel = GetNullableInt(node, "artifactLevel"),
        };
    }

    private static GearStat ParseStat(JsonElement node, string itemId)
    {
        var typeText = GetText(node, "type") ?? string.Empty;
        if (!Stats.TryGetValue(typeText, out var type))
            throw new AccountImportException($"装备 {itemId} 包含不支持的属性：{typeText}");
        var value = GetDouble(node, "value");
        if (!double.IsFinite(value) || value < 0 || value > 100000)
            throw new AccountImportException($"装备 {itemId} 的 {typeText} 数值无效");
        return new GearStat(type, value, GetInt(node, "rolls"), GetBool(node, "modified"));
    }

    private static string? GetText(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int GetFirstInt(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetNullableInt(node, name);
            if (value.HasValue)
                return value.Value;
        }
        return 0;
    }

    private static int GetInt(JsonElement node, string name) => GetNullableInt(node, name) ?? 0;

    private static int? GetNullableInt(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static double GetDouble(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value))
            return 0;
        if (value.TryGetDouble(out var number))
            return number;
        return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : 0;
    }

    private static bool GetBool(JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

public sealed class AccountRepository
{
    public AccountSnapshotV1? LoadSnapshot() => Load<AccountSnapshotV1>(AppPaths.AccountSnapshotPath);

    public HeroPreferenceDocument LoadPreferences() =>
        Load<HeroPreferenceDocument>(AppPaths.HeroPreferencesPath) ?? new HeroPreferenceDocument();

    public void SaveSnapshot(AccountSnapshotV1 snapshot)
    {
        if (snapshot.SchemaVersion != AccountSnapshotV1.CurrentSchemaVersion)
            throw new InvalidDataException($"不支持的账号快照版本：{snapshot.SchemaVersion}");
        BackupAndWrite(AppPaths.AccountSnapshotPath, snapshot);
    }

    public void SavePreferences(HeroPreferenceDocument preferences)
    {
        NormalizePriorities(preferences.Active);
        BackupAndWrite(AppPaths.HeroPreferencesPath, preferences);
    }

    public HeroPreferenceDocument MergePreferences(AccountSnapshotV1 snapshot, HeroPreferenceDocument current)
    {
        var activeById = current.Active.ToDictionary(value => value.HeroId, StringComparer.Ordinal);
        var history = current.History.ToDictionary(value => value.HeroId, StringComparer.Ordinal);
        foreach (var removed in current.Active.Where(value => snapshot.Heroes.All(hero => hero.Id != value.HeroId)))
            history[removed.HeroId] = removed;

        var merged = new List<HeroPreference>();
        foreach (var hero in snapshot.Heroes)
        {
            if (!activeById.TryGetValue(hero.Id, out var preference) && !history.Remove(hero.Id, out preference))
            {
                preference = new HeroPreference
                {
                    HeroId = hero.Id,
                    HeroCode = hero.Code,
                    Priority = int.MaxValue,
                    ArtifactCode = hero.ArtifactCode,
                    ArtifactLevel = hero.ArtifactLevel ?? 30,
                };
            }
            preference.HeroCode = hero.Code;
            merged.Add(preference);
        }
        NormalizePriorities(merged);
        return new HeroPreferenceDocument { Active = merged, History = history.Values.ToList() };
    }

    public static void MovePriority(List<HeroPreference> preferences, string heroId, int requestedPriority)
    {
        NormalizePriorities(preferences);
        var current = preferences.FindIndex(value => value.HeroId == heroId);
        if (current < 0)
            return;
        var target = Math.Clamp(requestedPriority - 1, 0, preferences.Count - 1);
        var item = preferences[current];
        preferences.RemoveAt(current);
        preferences.Insert(target, item);
        for (var index = 0; index < preferences.Count; index++)
            preferences[index].Priority = index + 1;
    }

    private static void NormalizePriorities(List<HeroPreference> preferences)
    {
        var ordered = preferences.OrderBy(value => value.Priority <= 0 ? int.MaxValue : value.Priority)
            .ThenBy(value => value.HeroId, StringComparer.Ordinal)
            .ToList();
        preferences.Clear();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Priority = index + 1;
            preferences.Add(ordered[index]);
        }
    }

    private static T? Load<T>(string path)
    {
        if (!File.Exists(path))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), AppPaths.JsonOptions);
        }
        catch
        {
            AppPaths.PreserveBrokenFile(path);
            return default;
        }
    }

    private static void BackupAndWrite<T>(string path, T value)
    {
        if (File.Exists(path))
            File.Copy(path, path + ".bak", overwrite: true);
        AppPaths.WriteJsonAtomic(path, value);
    }
}
