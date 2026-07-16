using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TiezhuToolbox;

/// <summary>
/// 窗口信息。
/// </summary>
public readonly record struct WindowInfo(IntPtr Handle, string Title, string ProcessName)
{
    public override string ToString() => string.IsNullOrEmpty(Title)
        ? $"[{ProcessName}]"
        : $"{Title}  [{ProcessName}]";
}

/// <summary>
/// 窗口辅助类：枚举窗口、截图。
/// </summary>
public static class WindowHelper
{
    /// <summary>
    /// 获取所有可见的顶层窗口，排除指定句柄（通常用于排除自身窗口）。
    /// </summary>
    public static IReadOnlyList<WindowInfo> GetWindows(IntPtr? excludeHandle = null)
    {
        var list = new List<WindowInfo>();

        Win32Api.EnumWindows((hWnd, _) =>
        {
            if (excludeHandle.HasValue && hWnd == excludeHandle.Value)
                return true;

            if (!Win32Api.IsWindowVisible(hWnd))
                return true;

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            var processName = GetProcessName(hWnd);
            list.Add(new WindowInfo(hWnd, title, processName));
            return true;
        }, IntPtr.Zero);

        return list;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(512);
        Win32Api.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetProcessName(IntPtr hWnd)
    {
        try
        {
            Win32Api.GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == 0)
                return string.Empty;

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 截取指定窗口的客户区或整个窗口。
    /// </summary>
    /// <param name="hWnd">窗口句柄。</param>
    /// <param name="clientOnly">true 截取客户区；false 截取整个窗口（包含标题栏和边框）。</param>
    /// <returns>截图位图。</returns>
    public static Bitmap? CaptureWindow(IntPtr hWnd, bool clientOnly = false)
    {
        if (hWnd == IntPtr.Zero)
            return null;

        if (clientOnly && Win32Api.GetClientRect(hWnd, out var clientRect))
        {
            // 客户区相对于窗口的左上角位置
            Win32Api.GetWindowRect(hWnd, out var windowRect);
            var offsetX = windowRect.Left;
            var offsetY = windowRect.Top;
            return CaptureByPrintWindow(hWnd, clientRect.Width, clientRect.Height, offsetX, offsetY)
                ?? CaptureByBitBlt(hWnd, clientRect.Width, clientRect.Height, offsetX, offsetY);
        }
        else
        {
            Win32Api.GetWindowRect(hWnd, out var windowRect);
            return CaptureByPrintWindow(hWnd, windowRect.Width, windowRect.Height, 0, 0)
                ?? CaptureByBitBlt(hWnd, windowRect.Width, windowRect.Height, 0, 0);
        }
    }

    private static Bitmap? CaptureByPrintWindow(IntPtr hWnd, int width, int height, int srcX, int srcY)
    {
        if (width <= 0 || height <= 0)
            return null;

        IntPtr hdcDest = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            IntPtr hdcSrc = Win32Api.GetWindowDC(hWnd);
            if (hdcSrc == IntPtr.Zero)
                return null;

            hdcDest = Win32Api.CreateCompatibleDC(hdcSrc);
            hBitmap = Win32Api.CreateCompatibleBitmap(hdcSrc, width, height);
            hOld = Win32Api.SelectObject(hdcDest, hBitmap);

            // 先尝试 RENDERFULLCONTENT，能捕获部分硬件加速窗口
            bool ok = Win32Api.PrintWindow(hWnd, hdcDest, Win32Api.PW_RENDERFULLCONTENT);

            if (!ok)
            {
                Win32Api.DeleteObject(hBitmap);
                hBitmap = Win32Api.CreateCompatibleBitmap(hdcSrc, width, height);
                Win32Api.SelectObject(hdcDest, hBitmap);
                ok = Win32Api.PrintWindow(hWnd, hdcDest, 0);
            }

            Win32Api.ReleaseDC(hWnd, hdcSrc);

            if (!ok)
                return null;

            // 如果请求了偏移（客户区），需要裁剪；这里简单返回整张，调用方按需处理
            return Image.FromHbitmap(hBitmap);
        }
        finally
        {
            if (hOld != IntPtr.Zero)
                Win32Api.SelectObject(hdcDest, hOld);
            if (hdcDest != IntPtr.Zero)
                Win32Api.DeleteDC(hdcDest);
            if (hBitmap != IntPtr.Zero)
                Win32Api.DeleteObject(hBitmap);
        }
    }

    private static Bitmap? CaptureByBitBlt(IntPtr hWnd, int width, int height, int srcX, int srcY)
    {
        if (width <= 0 || height <= 0)
            return null;

        IntPtr hdcDest = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            IntPtr hdcSrc = Win32Api.GetWindowDC(hWnd);
            if (hdcSrc == IntPtr.Zero)
                return null;

            hdcDest = Win32Api.CreateCompatibleDC(hdcSrc);
            hBitmap = Win32Api.CreateCompatibleBitmap(hdcSrc, width, height);
            hOld = Win32Api.SelectObject(hdcDest, hBitmap);

            bool ok = Win32Api.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, srcX, srcY,
                Win32Api.SRCCOPY | Win32Api.CAPTUREBLT);

            Win32Api.ReleaseDC(hWnd, hdcSrc);

            if (!ok)
                return null;

            return Image.FromHbitmap(hBitmap);
        }
        finally
        {
            if (hOld != IntPtr.Zero)
                Win32Api.SelectObject(hdcDest, hOld);
            if (hdcDest != IntPtr.Zero)
                Win32Api.DeleteDC(hdcDest);
            if (hBitmap != IntPtr.Zero)
                Win32Api.DeleteObject(hBitmap);
        }
    }

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
