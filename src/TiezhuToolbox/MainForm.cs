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
        RefreshDeviceList();
    }

    private void RefreshDeviceList()
    {
        if (AdbHelper.FindAdbPath() == null)
        {
            comboDevices.DataSource = null;
            UpdateStatus("未找到 adb.exe，请将 platform-tools 的 adb.exe 放到程序目录或加入 PATH");
            return;
        }

        try
        {
            var devices = AdbHelper.GetDevices();
            comboDevices.DataSource = null;
            comboDevices.DataSource = devices;

            if (comboDevices.Items.Count > 0)
                comboDevices.SelectedIndex = 0;

            UpdateStatus(devices.Count > 0
                ? $"已加载 {devices.Count} 个设备"
                : "未发现设备，请确认模拟器已开启 ADB 调试（MuMu：设置中心→其他→ADB 调试），或输入地址后点击连接");
        }
        catch (Exception ex)
        {
            UpdateStatus($"获取设备列表失败：{ex.Message}");
        }
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        RefreshDeviceList();
    }

    private void btnConnect_Click(object sender, EventArgs e)
    {
        var address = txtAddress.Text.Trim();
        if (string.IsNullOrEmpty(address))
        {
            UpdateStatus("请输入模拟器 ADB 地址（如 127.0.0.1:16384）");
            return;
        }

        if (AdbHelper.FindAdbPath() == null)
        {
            UpdateStatus("未找到 adb.exe，请将 platform-tools 的 adb.exe 放到程序目录或加入 PATH");
            return;
        }

        UpdateStatus($"正在连接 {address} ...");
        Application.DoEvents();

        try
        {
            var result = AdbHelper.Connect(address);
            UpdateStatus(string.IsNullOrEmpty(result) ? "连接命令已执行" : result);
        }
        catch (Exception ex)
        {
            UpdateStatus($"连接失败：{ex.Message}");
            return;
        }

        RefreshDeviceList();
    }

    private void btnCapture_Click(object sender, EventArgs e)
    {
        if (comboDevices.SelectedItem is not AdbDeviceInfo device)
        {
            UpdateStatus("请先选择一个设备");
            return;
        }

        UpdateStatus("正在截图...");
        Application.DoEvents();

        Bitmap? bitmap = null;
        try
        {
            bitmap = AdbHelper.ScreenshotPng(device.Serial);
        }
        catch (Exception ex)
        {
            UpdateStatus($"无法截取设备：{ex.Message}");
            return;
        }

        pictureBox.Image?.Dispose();
        pictureBox.Image = bitmap;

        // 保存截图
        try
        {
            var baseName = "adb_" + string.Join("_", device.Serial.Split(Path.GetInvalidFileNameChars()));
            _lastScreenshotPath = ScreenshotHelper.SaveBitmap(bitmap, baseName);
            UpdateStatus($"已保存：{_lastScreenshotPath}  ({bitmap.Width}x{bitmap.Height})");
        }
        catch (Exception ex)
        {
            UpdateStatus($"截图已显示，但保存失败：{ex.Message}");
        }
    }

    private void btnOpenFolder_Click(object sender, EventArgs e)
    {
        var dir = ScreenshotHelper.GetScreenshotDirectory();
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
