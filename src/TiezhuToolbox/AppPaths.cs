using System.Text;
using System.Text.Json;

namespace TiezhuToolbox;

/// <summary>当前 Windows 用户的可写配置与缓存目录。</summary>
internal static class AppPaths
{
    public static string UserRoot { get; } = ResolveUserRoot();

    public static string SettingsPath => Path.Combine(UserRoot, "settings.json");

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string ResolveUserRoot()
    {
        // 仅供自动化验证隔离真实用户配置；正常运行固定使用 LocalAppData。
        var testRoot = Environment.GetEnvironmentVariable("TIEZHU_TOOLBOX_USER_ROOT");
        return string.IsNullOrWhiteSpace(testRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TiezhuToolbox")
            : Path.GetFullPath(testRoot);
    }

    public static void WriteJsonAtomic<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        WriteTextAtomic(path, json);
    }

    public static void WriteTextAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("无法确定配置目录");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
        File.Move(temporaryPath, path, overwrite: true);
    }

    public static void PreserveBrokenFile(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            var brokenPath = path + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(path, brokenPath, overwrite: true);
        }
        catch
        {
            // 损坏文件无法改名时也要允许程序继续使用默认值。
        }

        try
        {
            var logDirectory = Path.Combine(UserRoot, "logs");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(Path.Combine(logDirectory, "config-errors.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 配置文件损坏，已回退默认：{path}{Environment.NewLine}");
        }
        catch
        {
            // 记录失败不影响回退。
        }
    }

}
