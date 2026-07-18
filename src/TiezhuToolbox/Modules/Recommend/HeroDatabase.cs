using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>
/// 官方战绩角色数据库（传说分段），数据来自 tools/HeroDataCollector 生成的 Assets/HeroData/heroes.json。
/// </summary>
public class HeroDatabase
{
    private static readonly Lazy<HeroDatabase> _instance = new(() => new HeroDatabase());

    public static HeroDatabase Instance => _instance.Value;

    /// <summary>是否成功加载 heroes.json。</summary>
    public bool IsLoaded { get; }

    /// <summary>数据对应的游戏赛季，如 pvp_rta_ss20。</summary>
    public string SeasonCode { get; private set; } = string.Empty;

    /// <summary>数据生成时间（采集工具写入）。</summary>
    public string UpdatedAt { get; private set; } = string.Empty;

    /// <summary>全部角色（传说分段）。</summary>
    public IReadOnlyList<HeroInfo> Heroes { get; private set; } = Array.Empty<HeroInfo>();

    /// <summary>套装代码 → 简体套装名（如 set_speed → 速度套装）。</summary>
    public IReadOnlyDictionary<string, string> SetNames { get; private set; }
        = new Dictionary<string, string>();

    /// <summary>简体套装名 → 套装代码（如 速度套装 → set_speed），用于匹配 OCR 的 SetName。</summary>
    public IReadOnlyDictionary<string, string> SetCodesByName { get; private set; }
        = new Dictionary<string, string>();

    private HeroDatabase()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", "heroes.json");
            if (!File.Exists(path))
                return;

            var doc = JsonSerializer.Deserialize<HeroDataDocument>(File.ReadAllText(path));
            if (doc?.Heroes == null)
                return;

            SeasonCode = doc.SeasonCode ?? string.Empty;
            UpdatedAt = doc.UpdatedAt ?? string.Empty;
            Heroes = doc.Heroes;
            var names = (doc.Sets ?? new List<HeroSetInfo>())
                .Where(s => !string.IsNullOrEmpty(s.Code) && !string.IsNullOrEmpty(s.Name))
                .ToDictionary(s => s.Code!, s => s.Name!);
            SetNames = names;
            SetCodesByName = names.ToDictionary(kv => kv.Value, kv => kv.Key);
            IsLoaded = true;
        }
        catch
        {
            // 数据文件损坏时视为未加载，不影响识别主流程
        }
    }

    /// <summary>按简体套装名（如“速度套装”）查套装代码，查不到返回 null。</summary>
    public string? FindSetCode(string setName)
        => !string.IsNullOrEmpty(setName) && SetCodesByName.TryGetValue(setName, out var code) ? code : null;

    /// <summary>角色头像文件路径（不存在时返回 null）。</summary>
    public static string? GetAvatarPath(string heroCode)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", "heroes", heroCode + ".png");
        return File.Exists(path) ? path : null;
    }

    private class HeroDataDocument
    {
        [JsonPropertyName("seasonCode")] public string? SeasonCode { get; set; }
        [JsonPropertyName("gradeCode")] public string? GradeCode { get; set; }
        [JsonPropertyName("updatedAt")] public string? UpdatedAt { get; set; }
        [JsonPropertyName("sets")] public List<HeroSetInfo>? Sets { get; set; }
        [JsonPropertyName("heroes")] public List<HeroInfo>? Heroes { get; set; }
    }
}

/// <summary>套装基础信息。</summary>
public class HeroSetInfo
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

/// <summary>角色信息（传说分段统计）。</summary>
public class HeroInfo
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    /// <summary>有用属性（简体中文，如“防御力”）。</summary>
    [JsonPropertyName("usefulStats")] public List<string> UsefulStats { get; set; } = new();

    /// <summary>主流套装搭配（已过滤低使用率）。</summary>
    [JsonPropertyName("setCombos")] public List<HeroSetCombo> SetCombos { get; set; } = new();
}

/// <summary>一套主流套装搭配。</summary>
public class HeroSetCombo
{
    [JsonPropertyName("sets")] public List<string> Sets { get; set; } = new();

    /// <summary>使用率（%）。</summary>
    [JsonPropertyName("rate")] public double Rate { get; set; }

    /// <summary>胜率（%）。</summary>
    [JsonPropertyName("winRate")] public double WinRate { get; set; }
}
