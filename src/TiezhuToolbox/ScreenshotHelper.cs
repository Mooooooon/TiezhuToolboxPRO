using System.Drawing.Imaging;

namespace TiezhuToolbox;

/// <summary>
/// 截图辅助类：截图文件保存。
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>
    /// 获取截图保存目录。
    /// 注意：单文件发布时 AppDomain.CurrentDomain.BaseDirectory 指向临时解压目录，
    /// 因此使用当前进程可执行文件所在目录。
    /// </summary>
    public static string GetScreenshotDirectory()
    {
        var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
        var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(exeDir, "screenshots");
    }

    /// <summary>
    /// 保存位图到 screenshots 目录，文件名包含时间戳。
    /// </summary>
    public static string SaveBitmap(Bitmap bitmap, string baseName)
    {
        var dir = GetScreenshotDirectory();
        Directory.CreateDirectory(dir);

        var fileName = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var fileNameSafe = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(dir, fileNameSafe);

        bitmap.Save(path, ImageFormat.Png);
        return path;
    }
}
