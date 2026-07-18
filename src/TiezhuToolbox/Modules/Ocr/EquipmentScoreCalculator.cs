namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// 装备分数计算（民间算法，只统计副属性，不含主属性）。
/// 百分比属性按数值计分，固定值属性按等效百分比换算。
/// </summary>
public static class EquipmentScoreCalculator
{
    /// <summary>
    /// 由副属性列表计算装备分数，结果保留两位小数。
    /// </summary>
    public static double Calculate(IEnumerable<SubStat> subStats)
    {
        double score = 0;
        foreach (var sub in subStats)
        {
            var isPercent = sub.Value.Contains('%');
            if (!double.TryParse(sub.Value.Replace("%", ""), out var value))
                continue;

            score += sub.Name switch
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
        return Math.Round(score, 2);
    }
}
