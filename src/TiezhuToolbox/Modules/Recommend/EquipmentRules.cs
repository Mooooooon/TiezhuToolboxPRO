using TiezhuToolbox.Modules.Ocr;

namespace TiezhuToolbox.Modules.Recommend;

public enum EquipmentPart
{
    Unknown,
    Weapon,
    Helm,
    Armor,
    Necklace,
    Ring,
    Boots,
}

public static class EquipmentRules
{
    public static readonly string[] UsefulStats =
        { "攻击力", "防御力", "生命值", "速度", "暴击率", "暴击伤害", "效果命中", "效果抗性" };

    public static readonly string[] NecklaceMainStats =
        { "攻击力%", "防御力%", "生命值%", "暴击率", "暴击伤害" };

    public static readonly string[] RingMainStats =
        { "攻击力%", "防御力%", "生命值%", "效果命中", "效果抗性" };

    public static readonly string[] BootsMainStats =
        { "攻击力%", "防御力%", "生命值%", "速度" };

    public static EquipmentPart DetectPart(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality)) return EquipmentPart.Unknown;
        if (quality.Contains("武器")) return EquipmentPart.Weapon;
        if (quality.Contains("头盔")) return EquipmentPart.Helm;
        if (quality.Contains("铠甲") || quality.Contains("护甲")) return EquipmentPart.Armor;
        if (quality.Contains("项链")) return EquipmentPart.Necklace;
        if (quality.Contains("戒指")) return EquipmentPart.Ring;
        if (quality.Contains("鞋子") || quality.Contains("靴")) return EquipmentPart.Boots;
        return EquipmentPart.Unknown;
    }

    public static string? NormalizeMainStat(EquipmentInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.MainStatName))
            return null;

        return info.MainStatName switch
        {
            "攻击力" when info.MainStatValue.Contains('%') => "攻击力%",
            "防御力" when info.MainStatValue.Contains('%') => "防御力%",
            "生命值" when info.MainStatValue.Contains('%') => "生命值%",
            "速度" => "速度",
            "暴击率" => "暴击率",
            "暴击伤害" => "暴击伤害",
            "效果命中" => "效果命中",
            "效果抗性" => "效果抗性",
            _ => null,
        };
    }

    public static List<string> DeriveMainStats(IEnumerable<string> usefulStats, IEnumerable<string> candidates)
    {
        var useful = usefulStats.ToHashSet(StringComparer.Ordinal);
        return candidates.Where(candidate => useful.Contains(candidate.TrimEnd('%'))).ToList();
    }

    public static List<string> DeriveNecklaceMainStats(IEnumerable<string> usefulStats)
    {
        var useful = usefulStats.ToHashSet(StringComparer.Ordinal);
        var criticalStats = new[] { "暴击率", "暴击伤害" }.Where(useful.Contains).ToList();
        return criticalStats.Count > 0
            ? criticalStats
            : DeriveMainStats(useful, NecklaceMainStats);
    }

    public static List<string> DeriveBootsMainStats(IEnumerable<string> usefulStats)
    {
        var useful = usefulStats.ToHashSet(StringComparer.Ordinal);
        return useful.Contains("速度")
            ? new List<string> { "速度" }
            : DeriveMainStats(useful, BootsMainStats);
    }
}
