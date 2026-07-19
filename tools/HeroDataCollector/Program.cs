using System.Text.Json;
using TiezhuToolbox.Modules.Recommend;

// 第七史诗官方英雄数据采集工具：官方英雄元数据 + 当前赛季传说分段统计。
// 用法：dotnet run [--out <输出目录>]

var outputDirectory = GetOutputDirectory(args);
HeroDataDocument? existing = null;
var existingPath = Path.Combine(outputDirectory, "heroes.json");
if (File.Exists(existingPath))
{
    try
    {
        existing = JsonSerializer.Deserialize<HeroDataDocument>(File.ReadAllText(existingPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch
    {
        Console.WriteLine("现有 heroes.json 无法读取，将重新生成");
    }
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var lastStage = string.Empty;
var progress = new Progress<HeroDataUpdateProgress>(value =>
{
    if (value.Stage != lastStage || value.Current == value.Total || value.Current % 10 == 0)
        Console.WriteLine($"[{value.Stage}] {value.Current}/{value.Total} {value.Message}");
    lastStage = value.Stage;
});

try
{
    using var service = new HeroDataUpdateService();
    var result = await service.WritePackageAsync(outputDirectory, existing, progress, cancellation.Token);
    foreach (var warning in result.Warnings)
        Console.WriteLine($"警告：{warning}");
    Console.WriteLine($"完成：{outputDirectory}");
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("已取消");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"采集失败：{ex.Message}");
    return 1;
}

static string GetOutputDirectory(string[] args)
{
    var index = Array.IndexOf(args, "--out");
    if (index >= 0 && index + 1 < args.Length)
        return Path.GetFullPath(args[index + 1]);

    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "src", "TiezhuToolbox", "TiezhuToolbox.csproj")))
            return Path.Combine(directory.FullName, "src", "TiezhuToolbox", "Assets", "HeroData");
        directory = directory.Parent;
    }
    throw new InvalidOperationException("未找到仓库根目录，请用 --out 指定输出目录");
}
