namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// 装备分数计算（民间算法，只统计副属性，不含主属性）。
/// 百分比属性按数值计分，固定值属性按等效百分比换算。
/// </summary>
public static class EquipmentScoreCalculator
{
    private static readonly int[] SpeedReforgeBonus = { 0, 1, 2, 3, 4, 4 };
    private static readonly int[] PercentReforgeBonus = { 1, 3, 4, 5, 7, 8 };
    private static readonly int[] CritChanceReforgeBonus = { 1, 2, 3, 4, 5, 6 };
    private static readonly int[] CritDamageReforgeBonus = { 1, 2, 3, 4, 6, 7 };

    /// <summary>
    /// 由副属性列表计算装备分数，结果保留两位小数。
    /// </summary>
    public static double Calculate(IEnumerable<SubStat> subStats)
    {
        return Math.Round(subStats.Sum(Calculate), 2);
    }

    /// <summary>
    /// 计算单条副属性的分数贡献（未识别的属性计 0 分）。
    /// </summary>
    public static double Calculate(SubStat sub)
    {
        var isPercent = sub.Value.Contains('%');
        if (!double.TryParse(sub.Value.Replace("%", ""), out var value))
            return 0;

        return sub.Name switch
        {
            "攻击力" => isPercent ? value : value * 3.46 / 39,
            "防御力" => isPercent ? value : value * 4.99 / 31,
            "生命值" => isPercent ? value : value * 3.09 / 174,
            "效果命中" or "效果抗性" => value,
            "速度" => value * 2,
            "暴击伤害" => value * 1.125,
            "暴击率" => value * 1.5,
            _ => 0,
        };
    }

    /// <summary>
    /// 模拟 85 级装备重铸到 90 级后的副属性总分。
    /// 重铸增量由每条副属性的强化次数决定；固定攻击/防御/生命按游戏规则不计增量。
    /// </summary>
    public static double CalculateReforged(IEnumerable<SubStat> subStats)
    {
        return Math.Round(subStats.Sum(sub => Calculate(sub) + CalculateReforgeBonus(sub)), 2);
    }

    /// <summary>计算单条副属性重铸后新增的分数。</summary>
    public static double CalculateReforgeBonus(SubStat sub)
    {
        var rolls = Math.Clamp(sub.RollCount, 0, 5);
        var isPercent = sub.Value.Contains('%');
        var valueBonus = sub.Name switch
        {
            "速度" => SpeedReforgeBonus[rolls],
            "攻击力" or "防御力" or "生命值" when isPercent => PercentReforgeBonus[rolls],
            "效果命中" or "效果抗性" => PercentReforgeBonus[rolls],
            "暴击率" => CritChanceReforgeBonus[rolls],
            "暴击伤害" => CritDamageReforgeBonus[rolls],
            _ => 0,
        };

        if (valueBonus == 0)
            return 0;

        return Calculate(new SubStat
        {
            Name = sub.Name,
            Value = isPercent ? $"{valueBonus}%" : valueBonus.ToString(),
        });
    }
}
