using OpenCvSharp;

var screenshotsDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\bin\Release\net9.0-windows\win-x64\publish\screenshots";
var outputDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\Assets\Templates";

Directory.CreateDirectory(Path.Combine(outputDir, "digits"));

// 从第一张截图提取 "88" 模板
var imagePath1 = Path.Combine(screenshotsDir, "MuMuNxDevice_20260717_030638.png");
using var mat1 = Cv2.ImRead(imagePath1);

if (mat1.Empty())
{
    Console.WriteLine("无法读取第一张截图");
    return;
}

// 装备等级"88"区域
var levelRect = new Rect(775, 319, 50, 40);
using var levelRegion = new Mat(mat1, levelRect);
Cv2.ImWrite(Path.Combine(outputDir, "debug_level.png"), levelRegion);
Console.WriteLine($"装备等级区域: {levelRect}");

// 预处理并保存整个区域作为 "88" 模板
var levelGray = Preprocess(levelRegion);
Cv2.ImWrite(Path.Combine(outputDir, "digits", "88.png"), levelGray);
Console.WriteLine("已保存数字模板 88.png");

// 从第二张截图提取 "+3" 模板
var imagePath2 = Path.Combine(screenshotsDir, "MuMuNxDevice_20260717_031029.png");
using var mat2 = Cv2.ImRead(imagePath2);

if (mat2.Empty())
{
    Console.WriteLine("无法读取第二张截图");
    return;
}

// 强化等级"+3"区域
var enhanceRect = new Rect(845, 295, 50, 40);
using var enhanceRegion = new Mat(mat2, enhanceRect);
Cv2.ImWrite(Path.Combine(outputDir, "debug_enhance.png"), enhanceRegion);
Console.WriteLine($"强化等级区域: {enhanceRect}");

// 预处理并保存整个区域作为 "+3" 模板
var enhanceGray = Preprocess(enhanceRegion);
Cv2.ImWrite(Path.Combine(outputDir, "digits", "+3.png"), enhanceGray);
Console.WriteLine("已保存数字模板 +3.png");

Console.WriteLine("模板提取完成");

static Mat Preprocess(Mat region)
{
    var gray = new Mat();
    Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
    Cv2.Resize(gray, gray, new OpenCvSharp.Size(region.Width * 4, region.Height * 4), interpolation: InterpolationFlags.Cubic);
    Cv2.Threshold(gray, gray, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
    Cv2.BitwiseNot(gray, gray);
    return gray;
}
