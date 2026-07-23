using System.Text.Json;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>只读套装需求数据库。运行时只读取程序内置 JSON，不联网、不使用用户覆盖。</summary>
public sealed class DemandDatabase
{
    public const int CurrentSchemaVersion = 1;
    private static readonly Lazy<DemandDatabase> LazyInstance = new(() => new DemandDatabase());

    public static DemandDatabase Instance => LazyInstance.Value;

    public bool IsLoaded { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public string UpdatedAt { get; private set; } = string.Empty;
    public IReadOnlyList<DemandSet> Sets { get; private set; } = Array.Empty<DemandSet>();
    public IReadOnlyDictionary<string, DemandSet> SetsByCode { get; private set; }
        = new Dictionary<string, DemandSet>();
    public IReadOnlyDictionary<string, string> SetCodesByName { get; private set; }
        = new Dictionary<string, string>();

    private DemandDatabase()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", "demand-profiles.json");
        Load(path);
    }

    public DemandSet? FindSet(string? setName)
    {
        if (string.IsNullOrWhiteSpace(setName)
            || !SetCodesByName.TryGetValue(setName, out var code)
            || !SetsByCode.TryGetValue(code, out var set))
        {
            return null;
        }

        return set;
    }

    private void Load(string path)
    {
        IsLoaded = false;
        ErrorMessage = string.Empty;
        try
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("缺少 demand-profiles.json", path);

            var document = JsonSerializer.Deserialize<DemandDataDocument>(
                               File.ReadAllText(path), AppPaths.JsonOptions)
                           ?? throw new InvalidDataException("需求数据内容为空");
            var errors = Validate(document);
            if (errors.Count > 0)
                throw new InvalidDataException(string.Join("；", errors.Take(5)));

            UpdatedAt = document.UpdatedAt;
            Sets = document.Sets;
            SetsByCode = document.Sets.ToDictionary(set => set.Code, StringComparer.Ordinal);
            SetCodesByName = document.Sets.ToDictionary(set => set.Name, set => set.Code, StringComparer.Ordinal);
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            UpdatedAt = string.Empty;
            Sets = Array.Empty<DemandSet>();
            SetsByCode = new Dictionary<string, DemandSet>();
            SetCodesByName = new Dictionary<string, string>();
        }
    }

    /// <summary>验证静态数据结构，供运行时加载与回归工具共用。</summary>
    public static IReadOnlyList<string> Validate(DemandDataDocument document)
    {
        var errors = new List<string>();
        var allowedStats = EquipmentRules.UsefulStats.ToHashSet(StringComparer.Ordinal);

        if (document.SchemaVersion != CurrentSchemaVersion)
            errors.Add($"不支持的数据版本 {document.SchemaVersion}");
        if (string.IsNullOrWhiteSpace(document.UpdatedAt))
            errors.Add("缺少 updatedAt");
        if (document.Sets.Count == 0)
            errors.Add("没有套装数据");

        var duplicateSetCodes = document.Sets
            .Where(set => !string.IsNullOrWhiteSpace(set.Code))
            .GroupBy(set => set.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var code in duplicateSetCodes)
            errors.Add($"套装代码重复：{code}");

        foreach (var set in document.Sets)
        {
            if (string.IsNullOrWhiteSpace(set.Code) || string.IsNullOrWhiteSpace(set.Name))
                errors.Add("套装代码或名称为空");

            var duplicateProfileIds = set.Profiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
                .GroupBy(profile => profile.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);
            foreach (var id in duplicateProfileIds)
                errors.Add($"{set.Code} 子类代码重复：{id}");

            foreach (var profile in set.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Name))
                    errors.Add($"{set.Code} 存在空子类代码或名称");
                if (profile.Stats.Count == 0 || profile.Stats.Any(stat => !allowedStats.Contains(stat)))
                    errors.Add($"{set.Code}/{profile.Id} 包含无效显式属性");
                if (profile.Stats.Count != profile.Stats.Distinct(StringComparer.Ordinal).Count())
                    errors.Add($"{set.Code}/{profile.Id} 显式属性重复");
                ValidateWeights(profile.Weights, $"{set.Code}/{profile.Id}", allowedStats, errors);
                if (profile.DemandWeight < 0)
                    errors.Add($"{set.Code}/{profile.Id} 需求权重为负数");

                foreach (var hero in profile.Heroes)
                {
                    if (string.IsNullOrWhiteSpace(hero.Code)
                        || string.IsNullOrWhiteSpace(hero.Name)
                        || string.IsNullOrWhiteSpace(hero.ComboName))
                    {
                        errors.Add($"{set.Code}/{profile.Id} 存在英雄代码、名称或组合为空");
                    }
                    if (hero.SampleShare < 0 || hero.SampleShare > 1)
                        errors.Add($"{set.Code}/{profile.Id}/{hero.Code} 样本占比越界");
                    if (hero.DemandContribution < 0)
                        errors.Add($"{set.Code}/{profile.Id}/{hero.Code} 需求贡献为负数");
                    ValidateWeights(
                        hero.Weights, $"{set.Code}/{profile.Id}/{hero.Code}", allowedStats, errors);
                }
            }
        }

        return errors;
    }

    private static void ValidateWeights(
        IReadOnlyDictionary<string, double> weights,
        string owner,
        IReadOnlySet<string> allowedStats,
        ICollection<string> errors)
    {
        if (weights.Count != allowedStats.Count
            || weights.Keys.Any(stat => !allowedStats.Contains(stat))
            || allowedStats.Any(stat => !weights.ContainsKey(stat)))
        {
            errors.Add($"{owner} 权重必须完整包含八种属性");
            return;
        }

        if (weights.Values.Any(value => !double.IsFinite(value) || value < 0 || value > 10))
            errors.Add($"{owner} 权重必须位于 0~10");
    }

    public static string? GetAvatarPath(string heroCode)
        => ResolveAssetPath("heroes", heroCode + ".png");

    public static string? GetSetIconPath(string setCode)
        => ResolveAssetPath("sets", setCode + ".png");

    private static string? ResolveAssetPath(string folder, string fileName)
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", folder, fileName);
        return File.Exists(path) ? path : null;
    }
}

