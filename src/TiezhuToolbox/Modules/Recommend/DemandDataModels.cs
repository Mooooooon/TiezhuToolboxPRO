using System.Text.Json.Serialization;

namespace TiezhuToolbox.Modules.Recommend;

/// <summary>人工维护的套装需求数据。</summary>
public sealed class DemandDataDocument
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; } = string.Empty;
    [JsonPropertyName("sets")] public List<DemandSet> Sets { get; set; } = new();
}

/// <summary>一个游戏套装及其全部属性子类。</summary>
public sealed class DemandSet
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("profiles")] public List<DemandProfile> Profiles { get; set; } = new();
}

/// <summary>套装下的一种显式属性组合。</summary>
public sealed class DemandProfile
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("stats")] public List<string> Stats { get; set; } = new();
    [JsonPropertyName("weights")] public Dictionary<string, double> Weights { get; set; } = new();
    [JsonPropertyName("demandWeight")] public double DemandWeight { get; set; }
    [JsonPropertyName("heroes")] public List<DemandHeroBuild> Heroes { get; set; } = new();
}

/// <summary>某名英雄的一条完整套装组合需求；同一英雄可以保留多条组合。</summary>
public sealed class DemandHeroBuild
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("comboName")] public string ComboName { get; set; } = string.Empty;
    [JsonPropertyName("sampleShare")] public double SampleShare { get; set; }
    [JsonPropertyName("demandContribution")] public double DemandContribution { get; set; }
    [JsonPropertyName("weights")] public Dictionary<string, double> Weights { get; set; } = new();
}

