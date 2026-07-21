using System.Text.Json.Serialization;

namespace TiezhuToolbox.Modules.Recommend;

public class HeroDataDocument
{
    [JsonPropertyName("seasonCode")] public string SeasonCode { get; set; } = string.Empty;
    [JsonPropertyName("gradeCode")] public string GradeCode { get; set; } = "emperor";
    [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; } = string.Empty;
    [JsonPropertyName("sets")] public List<HeroSetInfo> Sets { get; set; } = new();
    [JsonPropertyName("heroes")] public List<HeroInfo> Heroes { get; set; } = new();
}

/// <summary>套装基础信息。</summary>
public class HeroSetInfo
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

/// <summary>官方英雄基础数据与前排分段统计。</summary>
public class HeroInfo
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("attribute")] public string Attribute { get; set; } = string.Empty;
    [JsonPropertyName("job")] public string Job { get; set; } = string.Empty;
    [JsonPropertyName("grade")] public int Grade { get; set; }
    [JsonPropertyName("hasGradeData")] public bool HasGradeData { get; set; }
    [JsonPropertyName("usefulStats")] public List<string> UsefulStats { get; set; } = new();
    [JsonPropertyName("setCombos")] public List<HeroSetCombo> SetCombos { get; set; } = new();
}

/// <summary>一套官方主流套装搭配。</summary>
public class HeroSetCombo
{
    [JsonPropertyName("sets")] public List<string> Sets { get; set; } = new();
    [JsonPropertyName("rate")] public double Rate { get; set; }
    [JsonPropertyName("winRate")] public double WinRate { get; set; }
}

/// <summary>推荐算法最终使用的英雄配置。</summary>
public class HeroProfile
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Attribute { get; init; } = string.Empty;
    public string Job { get; init; } = string.Empty;
    public int Grade { get; init; }
    public bool HasGradeData { get; init; }
    /// <summary>用户是否将该英雄排除在装备匹配之外。</summary>
    public bool IsExcluded { get; set; }
    public IReadOnlyList<HeroSetCombo> SetCombos { get; init; } = Array.Empty<HeroSetCombo>();
    public List<string> UsefulStats { get; set; } = new();
    public List<string> AllowedSets { get; set; } = new();
    public List<string> NecklaceMainStats { get; set; } = new();
    public List<string> RingMainStats { get; set; } = new();
    public List<string> BootsMainStats { get; set; } = new();

    public HeroProfile Clone() => new()
    {
        Code = Code,
        Name = Name,
        Attribute = Attribute,
        Job = Job,
        Grade = Grade,
        HasGradeData = HasGradeData,
        IsExcluded = IsExcluded,
        SetCombos = SetCombos,
        UsefulStats = UsefulStats.ToList(),
        AllowedSets = AllowedSets.ToList(),
        NecklaceMainStats = NecklaceMainStats.ToList(),
        RingMainStats = RingMainStats.ToList(),
        BootsMainStats = BootsMainStats.ToList(),
    };
}

public class HeroOverrideDocument
{
    public const int CurrentVersion = 2;
    public int Version { get; set; } = CurrentVersion;
    public Dictionary<string, HeroProfileOverride> Heroes { get; set; } = new();
}

public class HeroProfileOverride
{
    public bool IsExcluded { get; set; }
    public List<string> UsefulStats { get; set; } = new();
    public List<string> AllowedSets { get; set; } = new();
    public List<string> NecklaceMainStats { get; set; } = new();
    public List<string> RingMainStats { get; set; } = new();
    public List<string> BootsMainStats { get; set; } = new();
}
