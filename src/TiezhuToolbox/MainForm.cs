using System.Diagnostics;

namespace TiezhuToolbox;

public partial class MainForm : Form
{
    private string? _lastScreenshotPath;

    public MainForm()
    {
        InitializeComponent();
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        RefreshWindowList();
    }

    private void RefreshWindowList()
    {
        comboWindows.DataSource = null;
        var windows = WindowHelper.GetWindows(this.Handle);
        comboWindows.DataSource = windows.ToList();
        comboWindows.DisplayMember = nameof(WindowInfo.Title);
        comboWindows.ValueMember = nameof(WindowInfo.Handle);

        if (comboWindows.Items.Count > 0)
            comboWindows.SelectedIndex = 0;

        UpdateStatus($"已加载 {windows.Count} 个窗口");
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        RefreshWindowList();
    }

    private void btnCapture_Click(object sender, EventArgs e)
    {
        if (comboWindows.SelectedItem is not WindowInfo window)
        {
            UpdateStatus("请先选择一个窗口");
            return;
        }

        UpdateStatus("正在截图...");
        Application.DoEvents();

        var bitmap = WindowHelper.CaptureWindow(window.Handle, clientOnly: false);
        if (bitmap == null)
        {
            UpdateStatus("截图失败，尝试切换截图模式...");
            bitmap = WindowHelper.CaptureWindow(window.Handle, clientOnly: true);
        }

        if (bitmap == null)
        {
            UpdateStatus($"无法截取窗口：{window.Title}");
            return;
        }

        pictureBox.Image?.Dispose();
        pictureBox.Image = bitmap;

        // 保存截图
        try
        {
            _lastScreenshotPath = WindowHelper.SaveBitmap(bitmap, window.ProcessName);
            UpdateStatus($"已保存：{_lastScreenshotPath}  ({bitmap.Width}x{bitmap.Height})");
        }
        catch (Exception ex)
        {
            UpdateStatus($"截图已显示，但保存失败：{ex.Message}");
        }
    }

    private void btnOpenFolder_Click(object sender, EventArgs e)
    {
        var dir = WindowHelper.GetScreenshotDirectory();
        Directory.CreateDirectory(dir);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            UpdateStatus($"无法打开截图目录：{ex.Message}");
        }
    }

    private async void btnRecognize_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_lastScreenshotPath) || !File.Exists(_lastScreenshotPath))
        {
            UpdateStatus("请先截图");
            return;
        }

        UpdateStatus("正在识别...");
        btnRecognize.Enabled = false;
        Application.DoEvents();

        try
        {
            var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates");
            using var engine = new Modules.Ocr.OcrEngine(templateDir);
            var info = await engine.RecognizeAsync(_lastScreenshotPath);

            var message = $"识别结果：等级 {info.Level}，强化 +{info.EnhanceLevel}，{info.Name}";
            if (!string.IsNullOrEmpty(info.MainStatName))
                message += $"，{info.MainStatName} {info.MainStatValue}";
            if (info.SubStats.Count > 0)
                message += $"，{info.SubStats.Count} 条副属性";

            UpdateStatus(message);

            // 显示详细信息
            var detail = $"装备等级: {info.Level}\n" +
                         $"强化等级: +{info.EnhanceLevel}\n" +
                         $"装备名称: {info.Name}\n" +
                         $"装备品质: {info.Quality}\n" +
                         $"主属性: {info.MainStatName} {info.MainStatValue}\n" +
                         $"副属性:\n";
            foreach (var sub in info.SubStats)
            {
                detail += $"  - {sub.Name} {sub.Value}";
                if (!string.IsNullOrEmpty(sub.EnhanceValue))
                    detail += $" ({sub.EnhanceValue})";
                detail += "\n";
            }
            detail += $"套装: {info.SetName}\n" +
                      $"装备分数: {info.Score}\n\n" +
                      $"原始文本:\n{info.RawText}\n\n" +
                      $"截图路径: {_lastScreenshotPath}\n" +
                      $"截图尺寸: {new Bitmap(_lastScreenshotPath).Width}x{new Bitmap(_lastScreenshotPath).Height}";

            MessageBox.Show(detail, "识别结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            UpdateStatus($"识别失败：{ex.Message}");
            MessageBox.Show($"识别失败：{ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnRecognize.Enabled = true;
        }
    }

    private void UpdateStatus(string message)
    {
        toolStripStatusLabel.Text = message;
    }
}
