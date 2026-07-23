namespace TiezhuToolbox.Modules.Recommend;

/// <summary>一件装备对某个套装属性子类的匹配结果。</summary>
public sealed class SetProfileRecommendation
{
    public string SetCode { get; init; } = string.Empty;
    public string SetName { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public string ProfileName { get; init; } = string.Empty;
    public double Score { get; init; }
    public double DemandWeight { get; init; }
    public List<string> MatchedStats { get; init; } = new();
    public string MainStatContribution { get; init; } = string.Empty;
    public List<HeroBuildRecommendation> Heroes { get; init; } = new();
}

/// <summary>属性子类下的一条英雄完整套装组合匹配结果。</summary>
public sealed class HeroBuildRecommendation
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ComboName { get; init; } = string.Empty;
    public double Score { get; init; }
    public double SampleShare { get; init; }
    public double DemandContribution { get; init; }
    public List<string> MatchedStats { get; init; } = new();
    public string? AvatarPath { get; init; }
}

