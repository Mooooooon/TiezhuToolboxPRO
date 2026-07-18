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

            ShowEquipmentInfo(info);

            WriteDebugLog($"识别成功\n截图路径: {_lastScreenshotPath}\n原始文本:\n{info.RawText}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"识别失败：{ex.Message}");
            WriteDebugLog($"识别失败：{ex}");
        }
        finally
        {
            btnRecognize.Enabled = true;
        }
    }

    private void ShowEquipmentInfo(Modules.Ocr.EquipmentInfo info)
    {
        lblLevel.Text = $"等级 {info.Level}  强化 +{info.EnhanceLevel}";
        lblName.Text = $"装备名称：{info.Name}";
        lblQuality.Text = $"装备品质：{info.Quality}";
        lblMainStat.Text = $"主属性：{info.MainStatName} {info.MainStatValue}";

        listSubStats.Items.Clear();
        foreach (var sub in info.SubStats)
        {
            var text = $"{sub.Name} {sub.Value}";
            if (!string.IsNullOrEmpty(sub.EnhanceValue))
                text += $" ({sub.EnhanceValue})";
            listSubStats.Items.Add(text);
        }

        lblSet.Text = $"套装：{info.SetName}";
        lblScore.Text = $"装备分数：{info.Score}";
    }

    /// <summary>
    /// 把调试信息（原始识别文本、异常堆栈等）追加写入程序目录 logs/debug.log，不在界面显示。
    /// </summary>
    private void WriteDebugLog(string message)
    {
        try
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "debug.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // 日志写入失败不影响主流程
        }
    }

    private void UpdateStatus(string message)
    {
        toolStripStatusLabel.Text = message;
    }
}
