using TiezhuToolbox.Modules.Ocr;
using TiezhuToolbox.Modules.Recommend;

var screenshotsDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\bin\Release\net9.0-windows\win-x64\publish\screenshots";
var templateDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\Assets\Templates";

// 新旧两种分辨率的截图
var imageNames = args.Length > 0
    ? args
    : new[] { "MuMuNxDevice_20260717_031029.png", "MuMuNxDevice_20260717_041111.png" };

// 合成样例自检（无需截图）：
// 样例一：速度套 + 速度主属性鞋 + 副属性{防御,生命,速度,命中} → 调香师维波里丝(c5154) 应为 100%
// 样例二：暴击套 + 暴击率主属性项链 + 同样副属性 → c5154 主属性/套装均不符，不得出现
// 样例三：速度套速度鞋，副属性{生命,防御,速度,暴击率}但强化全跳暴击率 → c5154 应出现但匹配度大降（<50%）
if (args.Contains("--synthetic"))
{
    void Print(string title, EquipmentInfo info)
    {
        Console.WriteLine($"===== 合成样例: {title} =====");
        var recs = HeroRecommender.Recommend(info);
        foreach (var rec in recs)
            Console.WriteLine($"  {rec.Name}({rec.Code}) 匹配度 {rec.Score}%");
        Console.WriteLine(recs.Any(r => r.Code == "c5154") ? "  → 含 c5154" : "  → 不含 c5154");
        Console.WriteLine();
    }

    Print("速度套速度鞋 副属性{防御,生命,速度,命中}", new EquipmentInfo
    {
        Level = 88,
        SetName = "速度套装",
        MainStatName = "速度",
        MainStatValue = "45",
        SubStats =
        {
            new SubStat { Name = "防御力", Value = "10%" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "8" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    });

    Print("暴击套暴击项链 副属性{防御,生命,速度,命中}", new EquipmentInfo
    {
        Level = 88,
        SetName = "暴击套装",
        MainStatName = "暴击率",
        MainStatValue = "55%",
        SubStats =
        {
            new SubStat { Name = "防御力", Value = "10%" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "8" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    });

    Print("速度套速度鞋 副属性{生命,防御,速度,暴击率}强化全跳暴击", new EquipmentInfo
    {
        Level = 88,
        SetName = "速度套装",
        MainStatName = "速度",
        MainStatValue = "45",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "6%" },
            new SubStat { Name = "防御力", Value = "6%" },
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "暴击率", Value = "20%" },
        },
    });

    // 强化建议自检（阈值 24/24）
    void PrintAdvice(string title, EquipmentInfo info)
    {
        var r = EnhancementAdvisor.Analyze(info, 24, 24);
        Console.WriteLine($"  [强化建议] {title} → {r.Text}（{r.Detail}）");
    }

    Console.WriteLine("===== 强化建议样例（阈值 24/24） =====");
    PrintAdvice("传说武器 +0 高分（应：继续强化）", new EquipmentInfo
    {
        Quality = "传说武器",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "8" },
            new SubStat { Name = "暴击率", Value = "8%" },
            new SubStat { Name = "攻击力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    });
    PrintAdvice("传说戒指 固定防御主属性速度4（应：作为速度散件继续赌）", new EquipmentInfo
    {
        Quality = "传说戒指",
        MainStatName = "防御力",
        MainStatValue = "60",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "防御力", Value = "8%" },
            new SubStat { Name = "攻击力", Value = "38" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "4" },
        },
    });
    PrintAdvice("传说武器 +0 低分带速度3（应：继续赌速度）", new EquipmentInfo
    {
        Quality = "传说武器",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "3" },
            new SubStat { Name = "生命值", Value = "4%" },
            new SubStat { Name = "防御力", Value = "4%" },
            new SubStat { Name = "效果命中", Value = "4%" },
        },
    });
    PrintAdvice("传说戒指 固定攻击主属性（应：固定值主属性，建议放弃）", new EquipmentInfo
    {
        Quality = "传说戒指",
        MainStatName = "攻击力",
        MainStatValue = "500",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "2" },
            new SubStat { Name = "暴击率", Value = "8%" },
            new SubStat { Name = "攻击力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    });
    PrintAdvice("传说戒指 百分比主属性低分无速度（应：分数过低，建议放弃）", new EquipmentInfo
    {
        Quality = "传说戒指",
        MainStatName = "攻击力",
        MainStatValue = "60%",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "4%" },
            new SubStat { Name = "防御力", Value = "4%" },
            new SubStat { Name = "效果命中", Value = "4%" },
            new SubStat { Name = "效果抗性", Value = "4%" },
        },
    });
    PrintAdvice("传说武器 +15 预计重铸恰好 65 分（应：建议重铸）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "效果命中", Value = "7%", RollCount = 0 },
            new SubStat { Name = "攻击力", Value = "15%", RollCount = 1 },
            new SubStat { Name = "暴击率", Value = "4%", RollCount = 0 },
            new SubStat { Name = "暴击伤害", Value = "22%", RollCount = 4 },
        },
    });
    PrintAdvice("传说武器 +15 预计重铸 64 分（应：分数过低，建议放弃）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "效果命中", Value = "6%", RollCount = 0 },
            new SubStat { Name = "攻击力", Value = "15%", RollCount = 1 },
            new SubStat { Name = "暴击率", Value = "4%", RollCount = 0 },
            new SubStat { Name = "暴击伤害", Value = "22%", RollCount = 4 },
        },
    });
    PrintAdvice("传说武器 +15 预计重铸低于 65 但速度 15（应：建议重铸）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "15", RollCount = 5 },
            new SubStat { Name = "生命值", Value = "4%", RollCount = 0 },
            new SubStat { Name = "防御力", Value = "4%", RollCount = 0 },
            new SubStat { Name = "效果命中", Value = "4%", RollCount = 0 },
        },
    });
    PrintAdvice("传说武器 90 级 +15（应：已完成重铸）", new EquipmentInfo
    {
        Level = 90,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "16", RollCount = 5 },
            new SubStat { Name = "生命值", Value = "18%", RollCount = 0 },
            new SubStat { Name = "防御力", Value = "18%", RollCount = 0 },
            new SubStat { Name = "效果命中", Value = "18%", RollCount = 0 },
        },
    });
    PrintAdvice("速度鞋 低分（应：分数过低，建议放弃，鞋子不赌速度）", new EquipmentInfo
    {
        Quality = "传说鞋子",
        MainStatName = "速度",
        MainStatValue = "45",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "4%" },
            new SubStat { Name = "防御力", Value = "4%" },
            new SubStat { Name = "效果命中", Value = "4%" },
            new SubStat { Name = "效果抗性", Value = "4%" },
        },
    });
    return;
}

using var engine = new OcrEngine(templateDir);

foreach (var name in imageNames)
{
    var imagePath = Path.Combine(screenshotsDir, name);
    if (!File.Exists(imagePath))
    {
        Console.WriteLine($"截图不存在: {imagePath}");
        continue;
    }

    Console.WriteLine($"===== 测试图片: {name} =====");

    var info = await engine.RecognizeAsync(imagePath);

    Console.WriteLine("识别结果:");
    Console.WriteLine($"  装备等级: {info.Level}");
    Console.WriteLine($"  强化等级: +{info.EnhanceLevel}");
    Console.WriteLine($"  装备品质: {info.Quality}");
    Console.WriteLine($"  主属性: {info.MainStatName} {info.MainStatValue}");
    Console.WriteLine($"  副属性:");
    foreach (var sub in info.SubStats)
    {
        var rollText = sub.RollCount > 0 ? $"({sub.RollCount})" : string.Empty;
        Console.WriteLine($"    - {sub.Name}{rollText} {sub.Value}" + (string.IsNullOrEmpty(sub.EnhanceValue) ? "" : $" ({sub.EnhanceValue})"));
    }
    Console.WriteLine($"  套装: {info.SetName}");
    Console.WriteLine($"  装备分数: {info.Score}");

    // 强化建议（阈值 24/24）
    var advice = EnhancementAdvisor.Analyze(info, 24, 24);
    Console.WriteLine($"  强化建议: {advice.Text}（{advice.Detail}）");

    // 装备 → 适用角色推荐（官方战绩传说分段数据）
    var recommendations = HeroRecommender.Recommend(info);
    Console.WriteLine("  适用角色:");
    foreach (var rec in recommendations)
        Console.WriteLine($"    - {rec.Name}({rec.Code}) 匹配度 {rec.Score}%  命中副属性=[{string.Join(",", rec.MatchedStats)}] 套装命中={rec.SetMatched}");
    if (recommendations.Count == 0)
        Console.WriteLine("    （无匹配或 heroes.json 缺失）");

    Console.WriteLine();
    Console.WriteLine("原始文本:");
    Console.WriteLine(info.RawText);
    Console.WriteLine();
}
