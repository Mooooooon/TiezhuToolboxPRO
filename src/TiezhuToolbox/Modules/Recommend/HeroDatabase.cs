using System.Text.Json;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>
/// 英雄数据库。数据优先级：用户覆盖配置 &gt; 用户更新的官方数据 &gt; 程序内置官方数据。
/// </summary>
public sealed class HeroDatabase
{
    private static readonly Lazy<HeroDatabase> LazyInstance = new(() => new HeroDatabase());
    private readonly object _sync = new();
    private HeroDataDocument _baseDocument = new();
    private HeroOverrideDocument _overrides = new();

    public static HeroDatabase Instance => LazyInstance.Value;
    public event EventHandler? Changed;

    public bool IsLoaded { get; private set; }
    public string SeasonCode { get; private set; } = string.Empty;
    public string UpdatedAt { get; private set; } = string.Empty;
    public bool UsesUserData { get; private set; }
    public IReadOnlyList<HeroInfo> Heroes { get; private set; } = Array.Empty<HeroInfo>();
    public IReadOnlyList<HeroProfile> Profiles { get; private set; } = Array.Empty<HeroProfile>();
    public IReadOnlyDictionary<string, string> SetNames { get; private set; }
        = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> SetCodesByName { get; private set; }
        = new Dictionary<string, string>();

    private HeroDatabase() => Reload(raiseEvent: false);

    public HeroDataDocument GetBaseDocumentSnapshot()
    {
        lock (_sync)
        {
            var json = JsonSerializer.Serialize(_baseDocument, AppPaths.JsonOptions);
            return JsonSerializer.Deserialize<HeroDataDocument>(json, AppPaths.JsonOptions) ?? new HeroDataDocument();
        }
    }

    public HeroProfile? GetProfile(string code)
    {
        lock (_sync)
            return Profiles.FirstOrDefault(h => h.Code == code)?.Clone();
    }

    public string? FindSetCode(string setName)
        => !string.IsNullOrEmpty(setName) && SetCodesByName.TryGetValue(setName, out var code) ? code : null;

    public void SaveOverride(HeroProfile profile)
    {
        lock (_sync)
        {
            if (!Profiles.Any(h => h.Code == profile.Code))
                return;

            _overrides.Heroes[profile.Code] = new HeroProfileOverride
            {
                IsExcluded = profile.IsExcluded,
                UsefulStats = Normalize(profile.UsefulStats, EquipmentRules.UsefulStats),
                AllowedSets = Normalize(profile.AllowedSets, SetNames.Keys),
                NecklaceMainStats = Normalize(profile.NecklaceMainStats, EquipmentRules.NecklaceMainStats),
                RingMainStats = Normalize(profile.RingMainStats, EquipmentRules.RingMainStats),
                BootsMainStats = Normalize(profile.BootsMainStats, EquipmentRules.BootsMainStats),
            };
            _overrides.Version = HeroOverrideDocument.CurrentVersion;
            AppPaths.WriteJsonAtomic(AppPaths.HeroOverridesPath, _overrides);
            RebuildProfiles();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>导出全部英雄自定义配置，包含装备需求和匹配屏蔽状态。</summary>
    public void ExportOverrides(string path)
    {
        lock (_sync)
            AppPaths.WriteJsonAtomic(path, _overrides);
    }

    /// <summary>从存档替换全部英雄自定义配置。</summary>
    public void ImportOverrides(string path)
    {
        HeroOverrideDocument imported;
        try
        {
            imported = JsonSerializer.Deserialize<HeroOverrideDocument>(
                           File.ReadAllText(path), AppPaths.JsonOptions)
                       ?? throw new InvalidDataException("配置存档内容为空");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("配置存档格式不正确", ex);
        }

        if (imported.Version <= 0 || imported.Version > HeroOverrideDocument.CurrentVersion)
            throw new InvalidDataException($"不支持的配置存档版本：{imported.Version}");

        imported.Heroes ??= new Dictionary<string, HeroProfileOverride>();
        imported.Heroes = imported.Heroes
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value != null)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        imported.Version = HeroOverrideDocument.CurrentVersion;

        lock (_sync)
        {
            _overrides = imported;
            AppPaths.WriteJsonAtomic(AppPaths.HeroOverridesPath, _overrides);
            RebuildProfiles();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ResetOverride(string heroCode)
    {
        var changed = false;
        lock (_sync)
        {
            changed = _overrides.Heroes.Remove(heroCode);
            if (changed)
            {
                AppPaths.WriteJsonAtomic(AppPaths.HeroOverridesPath, _overrides);
                RebuildProfiles();
            }
        }
        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ResetAllOverrides()
    {
        lock (_sync)
        {
            _overrides = new HeroOverrideDocument();
            AppPaths.WriteJsonAtomic(AppPaths.HeroOverridesPath, _overrides);
            RebuildProfiles();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reload() => Reload(raiseEvent: true);

    private void Reload(bool raiseEvent)
    {
        lock (_sync)
        {
            IsLoaded = false;
            var bundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", "heroes.json");
            var userDocument = LoadDocument(AppPaths.UserHeroDataPath);
            UsesUserData = userDocument?.Heroes.Count > 0;
            _baseDocument = UsesUserData ? userDocument! : LoadDocument(bundledPath) ?? new HeroDataDocument();
            if (_baseDocument.Heroes.Count == 0)
                return;

            _overrides = LoadOverrides();
            SeasonCode = _baseDocument.SeasonCode;
            UpdatedAt = _baseDocument.UpdatedAt;
            Heroes = _baseDocument.Heroes;
            SetNames = _baseDocument.Sets
                .Where(s => !string.IsNullOrWhiteSpace(s.Code) && !string.IsNullOrWhiteSpace(s.Name))
                .GroupBy(s => s.Code)
                .ToDictionary(g => g.Key, g => g.First().Name);
            SetCodesByName = SetNames.ToDictionary(kv => kv.Value, kv => kv.Key);
            RebuildProfiles();
            IsLoaded = true;
        }

        if (raiseEvent)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildProfiles()
    {
        Profiles = _baseDocument.Heroes
            .Where(h => !string.IsNullOrWhiteSpace(h.Code))
            .Select(CreateProfile)
            .OrderBy(h => h.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    private HeroProfile CreateProfile(HeroInfo hero)
    {
        var useful = Normalize(hero.UsefulStats, EquipmentRules.UsefulStats);
        var profile = new HeroProfile
        {
            Code = hero.Code,
            Name = string.IsNullOrWhiteSpace(hero.Name) ? hero.Code : hero.Name,
            Attribute = hero.Attribute,
            Job = hero.Job,
            Grade = hero.Grade,
            HasGradeData = hero.HasGradeData || hero.UsefulStats.Count > 0 || hero.SetCombos.Count > 0,
            SetCombos = hero.SetCombos,
            UsefulStats = useful,
            AllowedSets = hero.SetCombos.SelectMany(c => c.Sets).Where(SetNames.ContainsKey)
                .Distinct(StringComparer.Ordinal).OrderBy(s => s).ToList(),
            NecklaceMainStats = EquipmentRules.DeriveNecklaceMainStats(useful),
            RingMainStats = EquipmentRules.DeriveMainStats(useful, EquipmentRules.RingMainStats),
            BootsMainStats = EquipmentRules.DeriveBootsMainStats(useful),
        };

        if (!_overrides.Heroes.TryGetValue(hero.Code, out var custom))
            return profile;

        profile.IsExcluded = custom.IsExcluded;
        profile.UsefulStats = Normalize(custom.UsefulStats, EquipmentRules.UsefulStats);
        profile.AllowedSets = Normalize(custom.AllowedSets, SetNames.Keys);
        profile.NecklaceMainStats = Normalize(custom.NecklaceMainStats, EquipmentRules.NecklaceMainStats);
        profile.RingMainStats = Normalize(custom.RingMainStats, EquipmentRules.RingMainStats);
        profile.BootsMainStats = Normalize(custom.BootsMainStats, EquipmentRules.BootsMainStats);
        return profile;
    }

    private static List<string> Normalize(IEnumerable<string>? values, IEnumerable<string> allowed)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        return (values ?? Array.Empty<string>()).Where(allowedSet.Contains)
            .Distinct(StringComparer.Ordinal).ToList();
    }

    private static HeroDataDocument? LoadDocument(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<HeroDataDocument>(File.ReadAllText(path), AppPaths.JsonOptions);
        }
        catch
        {
            if (string.Equals(path, AppPaths.UserHeroDataPath, StringComparison.OrdinalIgnoreCase))
                AppPaths.PreserveBrokenFile(path);
            return null;
        }
    }

    private static HeroOverrideDocument LoadOverrides()
    {
        if (!File.Exists(AppPaths.HeroOverridesPath))
            return new HeroOverrideDocument();
        try
        {
            var document = JsonSerializer.Deserialize<HeroOverrideDocument>(
                               File.ReadAllText(AppPaths.HeroOverridesPath), AppPaths.JsonOptions)
                           ?? new HeroOverrideDocument();
            document.Heroes ??= new Dictionary<string, HeroProfileOverride>();
            document.Heroes = document.Heroes
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value != null)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            return document;
        }
        catch
        {
            AppPaths.PreserveBrokenFile(AppPaths.HeroOverridesPath);
            return new HeroOverrideDocument();
        }
    }

    public static string? GetAvatarPath(string heroCode)
        => ResolveAssetPath("heroes", heroCode + ".png");

    public static string? GetSetIconPath(string setCode)
        => ResolveAssetPath("sets", setCode + ".png");

    private static string? ResolveAssetPath(string folder, string fileName)
    {
        var userPath = Path.Combine(AppPaths.UserHeroDataDirectory, folder, fileName);
        if (File.Exists(userPath))
            return userPath;
        var bundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", folder, fileName);
        return File.Exists(bundledPath) ? bundledPath : null;
    }
}
