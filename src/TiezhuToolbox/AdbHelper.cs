using System.Diagnostics;
using System.Text;

namespace TiezhuToolbox;

/// <summary>
/// ADB 设备信息。
/// </summary>
public readonly record struct AdbDeviceInfo(string Serial, string State, string Model)
{
    public override string ToString() => string.IsNullOrEmpty(Model)
        ? $"{Serial}  [{State}]"
        : $"{Serial}  [{State}  {Model}]";
}

/// <summary>
/// ADB 辅助类：定位 adb、枚举设备、连接模拟器、截图。
/// </summary>
public static class AdbHelper
{
    private static string? _cachedAdbPath;

    /// <summary>
    /// 查找 adb.exe，找到后缓存。找不到返回 null。
    /// </summary>
    public static string? FindAdbPath()
    {
        if (_cachedAdbPath != null && File.Exists(_cachedAdbPath))
            return _cachedAdbPath;

        foreach (var candidate in EnumerateCandidates())
        {
            if (File.Exists(candidate))
            {
                _cachedAdbPath = candidate;
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        // 1. 程序同目录，方便用户用自备版本覆盖
        var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
        var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        yield return Path.Combine(exeDir, "adb.exe");
        yield return Path.Combine(exeDir, "adb", "adb.exe");

        // 2. PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                yield return Path.Combine(dir.Trim(), "adb.exe");
        }

        // 3. Android SDK 环境变量
        foreach (var envName in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
        {
            var sdkDir = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrEmpty(sdkDir))
                yield return Path.Combine(sdkDir, "platform-tools", "adb.exe");
        }

        // 4. Android Studio 默认 SDK 位置
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");

        // 5. 随程序发布的精简版 platform-tools，作为系统未安装 ADB 时的兜底。
        // 单文件发布会将内容解压到 AppContext.BaseDirectory，它可能与 exeDir 不同。
        yield return Path.Combine(exeDir, "platform-tools", "adb.exe");

        var appBaseDir = AppContext.BaseDirectory;
        if (!string.Equals(
                Path.GetFullPath(appBaseDir),
                Path.GetFullPath(exeDir),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(appBaseDir, "platform-tools", "adb.exe");
        }
    }

    /// <summary>
    /// 获取 adb devices -l 列出的设备。
    /// </summary>
    public static List<AdbDeviceInfo> GetDevices()
    {
        var adb = FindAdbPath() ?? throw new FileNotFoundException("未找到 adb.exe");
        var output = RunAdb(adb, "devices -l", timeoutMs: 10_000);

        var devices = new List<AdbDeviceInfo>();
        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            var model = parts.Skip(2)
                .Where(p => p.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                .Select(p => p["model:".Length..])
                .FirstOrDefault() ?? string.Empty;

            devices.Add(new AdbDeviceInfo(parts[0], parts[1], model));
        }

        return devices;
    }

    /// <summary>
    /// adb connect 到指定地址（如 127.0.0.1:16384），返回 adb 的原文输出。
    /// </summary>
    public static string Connect(string address)
    {
        var adb = FindAdbPath() ?? throw new FileNotFoundException("未找到 adb.exe");
        return RunAdb(adb, $"connect {address}", timeoutMs: 10_000).Trim();
    }

    /// <summary>
    /// 通过 adb exec-out screencap 截取设备屏幕，返回 PNG 位图。
    /// 使用 exec-out 而非 shell，避免 adb 在 Windows 下的 CRLF 转换破坏 PNG 二进制。
    /// </summary>
    public static Bitmap ScreenshotPng(string serial)
    {
        var adb = FindAdbPath() ?? throw new FileNotFoundException("未找到 adb.exe");
        var pngBytes = RunAdbBinary(adb, $"-s {serial} exec-out screencap -p", timeoutMs: 20_000);

        if (pngBytes.Length == 0)
            throw new InvalidOperationException("adb 未返回任何截图数据，请确认设备连接正常。");

        // PNG 魔数：89 50 4E 47
        if (pngBytes.Length < 4 || pngBytes[0] != 0x89 || pngBytes[1] != 0x50 || pngBytes[2] != 0x4E || pngBytes[3] != 0x47)
        {
            var text = Encoding.UTF8.GetString(pngBytes).Trim();
            throw new InvalidOperationException($"adb 截图失败：{(text.Length > 0 ? text : "返回数据不是 PNG 图像")}");
        }

        return new Bitmap(new MemoryStream(pngBytes));
    }

    private static ProcessStartInfo CreateStartInfo(string adb, string arguments) => new()
    {
        FileName = adb,
        Arguments = arguments,
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    /// <summary>
    /// 运行 adb 并以文本形式返回 stdout（stderr 非空时合并）。
    /// </summary>
    private static string RunAdb(string adb, string arguments, int timeoutMs)
    {
        using var process = Process.Start(CreateStartInfo(adb, arguments))
            ?? throw new InvalidOperationException($"无法启动 adb：{adb}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"adb {arguments} 执行超时");
        }

        return string.IsNullOrEmpty(stdout) ? stderr : stdout;
    }

    /// <summary>
    /// 运行 adb 并以二进制形式返回 stdout（用于截图等二进制输出）。
    /// </summary>
    private static byte[] RunAdbBinary(string adb, string arguments, int timeoutMs)
    {
        using var process = Process.Start(CreateStartInfo(adb, arguments))
            ?? throw new InvalidOperationException($"无法启动 adb：{adb}");

        using var ms = new MemoryStream();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var copyTask = Task.Run(() => process.StandardOutput.BaseStream.CopyTo(ms));

        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"adb {arguments} 执行超时");
        }

        copyTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (ms.Length == 0 && !string.IsNullOrWhiteSpace(stderr))
            throw new InvalidOperationException($"adb 执行失败：{stderr.Trim()}");

        return ms.ToArray();
    }
}
