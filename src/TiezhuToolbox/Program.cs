namespace TiezhuToolbox;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 由程序按显示器 DPI 直接绘制，避免 Windows 将整个窗口位图拉伸后文字发糊。
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetDefaultFont(new Font("Microsoft YaHei UI", 9.75F));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
