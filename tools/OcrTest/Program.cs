using TiezhuToolbox.Modules.Ocr;

var screenshotsDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\bin\Release\net9.0-windows\win-x64\publish\screenshots";
var templateDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\Assets\Templates";

// 新旧两种分辨率的截图
var imageNames = args.Length > 0
    ? args
    : new[] { "MuMuNxDevice_20260717_031029.png", "MuMuNxDevice_20260717_041111.png" };

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
    Console.WriteLine($"  装备名称: {info.Name}");
    Console.WriteLine($"  装备品质: {info.Quality}");
    Console.WriteLine($"  主属性: {info.MainStatName} {info.MainStatValue}");
    Console.WriteLine($"  副属性:");
    foreach (var sub in info.SubStats)
    {
        Console.WriteLine($"    - {sub.Name} {sub.Value}" + (string.IsNullOrEmpty(sub.EnhanceValue) ? "" : $" ({sub.EnhanceValue})"));
    }
    Console.WriteLine($"  套装: {info.SetName}");
    Console.WriteLine($"  装备分数: {info.Score}");
    Console.WriteLine();
    Console.WriteLine("原始文本:");
    Console.WriteLine(info.RawText);
    Console.WriteLine();
}
