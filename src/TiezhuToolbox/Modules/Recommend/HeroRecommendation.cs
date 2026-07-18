namespace TiezhuToolbox.Modules.Recommend;

/// <summary>
/// 装备对某个角色的匹配结果。
/// </summary>
public class HeroRecommendation
{
    /// <summary>角色代码，如 c5154。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>角色名（简体中文）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>匹配度（0~100，保留 1 位小数）。</summary>
    public double Score { get; set; }

    /// <summary>匹配上的副属性（简体中文）。</summary>
    public List<string> MatchedStats { get; set; } = new();

    /// <summary>装备套装是否命中该角色的主流搭配。</summary>
    public bool SetMatched { get; set; }

    /// <summary>头像文件路径，无头像数据时为 null。</summary>
    public string? AvatarPath { get; set; }
}
