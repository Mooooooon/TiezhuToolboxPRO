using System.Text.Json.Serialization;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>用户手动添加的套装属性子类。</summary>
public sealed class CustomDemandProfile
{
    public string Id { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Stats { get; set; } = new();
    public Dictionary<string, double> Weights { get; set; } = new();
    public bool Enabled { get; set; } = true;

    [JsonIgnore]
    public string ProfileKey => SetProfileMatcher.CreateProfileKey(SetCode, Id);

    public DemandProfile ToDemandProfile()
    {
        var weights = EquipmentRules.UsefulStats.ToDictionary(
            stat => stat,
            stat => Stats.Contains(stat, StringComparer.Ordinal)
                ? Weights.GetValueOrDefault(stat)
                : 0D,
            StringComparer.Ordinal);
        return new DemandProfile
        {
            Id = Id,
            Name = Name,
            Stats = Stats.ToList(),
            Weights = weights,
            // 手动子类不使用需求量排序；同分时与其他零权重条目按名称稳定排序。
            DemandWeight = 0,
        };
    }
}

internal sealed class CustomDemandProfileDocument
{
    public int SchemaVersion { get; set; } = CustomDemandProfileStore.CurrentSchemaVersion;
    public List<CustomDemandProfile> Profiles { get; set; } = new();
}

/// <summary>独立加载和保存用户手动属性子类，不修改内置 demand-profiles.json。</summary>
public sealed class CustomDemandProfileStore
{
    public const int CurrentSchemaVersion = 1;
    private static readonly Lazy<CustomDemandProfileStore> LazyInstance =
        new(() => new CustomDemandProfileStore());
    private readonly List<CustomDemandProfile> _profiles = new();

    public static CustomDemandProfileStore Instance => LazyInstance.Value;
    public IReadOnlyList<CustomDemandProfile> Profiles => _profiles.Select(Clone).ToList();
    public string ErrorMessage { get; private set; } = string.Empty;

    private CustomDemandProfileStore() => Load();

    public IReadOnlyList<CustomDemandProfile> GetProfiles(string setCode) => _profiles
        .Where(profile => string.Equals(profile.SetCode, setCode, StringComparison.Ordinal))
        .Select(Clone)
        .ToList();

    public CustomDemandProfile? Find(string setCode, string profileId)
        => FindStored(setCode, profileId) is { } profile ? Clone(profile) : null;

    public void Upsert(CustomDemandProfile profile)
    {
        NormalizeAndValidate(profile);
        if (string.IsNullOrWhiteSpace(profile.Id))
            profile.Id = $"custom-{Guid.NewGuid():N}";
        else if (!profile.Id.StartsWith("custom-", StringComparison.Ordinal))
            throw new InvalidDataException("手动子类标识格式无效");

        var existingIndex = _profiles.FindIndex(item =>
            string.Equals(item.Id, profile.Id, StringComparison.Ordinal));
        var stored = Clone(profile);
        CustomDemandProfile? previous = null;
        if (existingIndex >= 0)
        {
            previous = _profiles[existingIndex];
            _profiles[existingIndex] = stored;
        }
        else
            _profiles.Add(stored);
        try
        {
            Save();
        }
        catch
        {
            if (existingIndex >= 0)
                _profiles[existingIndex] = previous!;
            else
                _profiles.Remove(stored);
            throw;
        }
    }

    public void SetEnabled(string setCode, string profileId, bool enabled)
    {
        var profile = FindStored(setCode, profileId)
                      ?? throw new InvalidOperationException("手动属性子类不存在");
        if (profile.Enabled == enabled)
            return;
        var previous = profile.Enabled;
        profile.Enabled = enabled;
        try
        {
            Save();
        }
        catch
        {
            profile.Enabled = previous;
            throw;
        }
    }

    public void Remove(string setCode, string profileId)
    {
        var index = _profiles.FindIndex(profile =>
            string.Equals(profile.SetCode, setCode, StringComparison.Ordinal)
            && string.Equals(profile.Id, profileId, StringComparison.Ordinal));
        if (index < 0)
            return;
        var removed = _profiles[index];
        _profiles.RemoveAt(index);
        try
        {
            Save();
        }
        catch
        {
            _profiles.Insert(index, removed);
            throw;
        }
    }

    private void Load()
    {
        _profiles.Clear();
        ErrorMessage = string.Empty;
        if (!File.Exists(AppPaths.CustomDemandProfilesPath))
            return;
        if (!DemandDatabase.Instance.IsLoaded)
        {
            ErrorMessage = "内置需求数据未加载，暂时无法读取手动属性子类";
            return;
        }

        try
        {
            var document = System.Text.Json.JsonSerializer.Deserialize<CustomDemandProfileDocument>(
                               File.ReadAllText(AppPaths.CustomDemandProfilesPath), AppPaths.JsonOptions)
                           ?? throw new InvalidDataException("手动需求数据内容为空");
            if (document.SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException($"不支持的手动需求数据版本 {document.SchemaVersion}");
            document.Profiles ??= new List<CustomDemandProfile>();
            var duplicateIds = document.Profiles
                .GroupBy(profile => profile.Id, StringComparer.Ordinal)
                .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
            if (duplicateIds != null)
                throw new InvalidDataException("手动属性子类标识为空或重复");

            foreach (var profile in document.Profiles)
            {
                NormalizeAndValidate(profile);
                if (!profile.Id.StartsWith("custom-", StringComparison.Ordinal))
                    throw new InvalidDataException("手动子类标识格式无效");
                _profiles.Add(Clone(profile));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _profiles.Clear();
            AppPaths.PreserveBrokenFile(AppPaths.CustomDemandProfilesPath);
        }
    }

    private void Save()
    {
        var document = new CustomDemandProfileDocument
        {
            Profiles = _profiles.Select(Clone).ToList(),
        };
        AppPaths.WriteJsonAtomic(AppPaths.CustomDemandProfilesPath, document);
        ErrorMessage = string.Empty;
    }

    private CustomDemandProfile? FindStored(string setCode, string profileId) => _profiles.FirstOrDefault(
        profile => string.Equals(profile.SetCode, setCode, StringComparison.Ordinal)
                   && string.Equals(profile.Id, profileId, StringComparison.Ordinal));

    private static void NormalizeAndValidate(CustomDemandProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.SetCode = profile.SetCode?.Trim() ?? string.Empty;
        profile.Name = profile.Name?.Trim() ?? string.Empty;
        profile.Stats ??= new List<string>();
        profile.Weights ??= new Dictionary<string, double>();
        profile.Stats = profile.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat))
            .Select(stat => stat.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!DemandDatabase.Instance.SetsByCode.ContainsKey(profile.SetCode))
            throw new InvalidDataException("手动属性子类引用了未知套装");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 80)
            throw new InvalidDataException("名称不能为空且不能超过 80 个字符");
        if (profile.Stats.Count == 0
            || profile.Stats.Any(stat => !EquipmentRules.UsefulStats.Contains(stat, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("至少选择一种有效属性");
        }

        var normalizedWeights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var stat in profile.Stats)
        {
            var value = profile.Weights.GetValueOrDefault(stat);
            if (!double.IsFinite(value) || value <= 0 || value > 10)
                throw new InvalidDataException($"{stat}的权重必须大于 0 且不超过 10");
            normalizedWeights[stat] = value;
        }
        profile.Weights = normalizedWeights;
    }

    private static CustomDemandProfile Clone(CustomDemandProfile profile) => new()
    {
        Id = profile.Id,
        SetCode = profile.SetCode,
        Name = profile.Name,
        Stats = profile.Stats.ToList(),
        Weights = new Dictionary<string, double>(profile.Weights, StringComparer.Ordinal),
        Enabled = profile.Enabled,
    };
}
