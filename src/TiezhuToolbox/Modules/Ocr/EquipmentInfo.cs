namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// 装备信息识别结果。
/// </summary>
public class EquipmentInfo
{
    /// <summary>装备等级，如 88。</summary>
    public int Level { get; set; }

    /// <summary>强化等级，如 3 表示 +3。</summary>
    public int EnhanceLevel { get; set; }

    /// <summary>装备名称，如“荣耀斗士战斧”。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>装备品质，如“传说武器”。</summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>主属性名称，如“攻击力”。</summary>
    public string MainStatName { get; set; } = string.Empty;

    /// <summary>主属性值，如 164。</summary>
    public string MainStatValue { get; set; } = string.Empty;

    /// <summary>副属性列表。</summary>
    public List<SubStat> SubStats { get; set; } = new();

    /// <summary>套装名称，如“命中套装”。</summary>
    public string SetName { get; set; } = string.Empty;

    /// <summary>装备分数，如 50(+11)。</summary>
    public string Score { get; set; } = string.Empty;

    /// <summary>识别原始文本，用于调试。</summary>
    public string RawText { get; set; } = string.Empty;
}

/// <summary>
/// 副属性。
/// </summary>
public class SubStat
{
    /// <summary>属性名称，如“生命值”。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>属性值，如“8%”或“5”。</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>强化增加值，如“+8%”。</summary>
    public string? EnhanceValue { get; set; }
}
