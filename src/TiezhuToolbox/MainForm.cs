using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TiezhuToolbox;

public partial class MainForm : Form
{
    private const int WmHotKey = 0x0312;
    private const int RecognitionHotKeyId = 0x547A;

    private static readonly Color AccentColor = Color.FromArgb(26, 115, 232);
    private static readonly Color TextDarkColor = Color.FromArgb(32, 33, 36);
    private static readonly Font HeroNameFont = new("Microsoft YaHei UI", 9.75F, FontStyle.Bold);
    private static readonly Font HeroScoreFont = new("Microsoft YaHei UI", 9F);

    // 强化建议徽章配色：继续强化=绿，赌速度=橙，重铸=紫，放弃=红，无法判断=灰
    private static readonly Color AdviceContinueColor = Color.FromArgb(52, 168, 83);
    private static readonly Color AdviceGambleColor = Color.FromArgb(245, 124, 0);
    private static readonly Color AdviceReforgeColor = Color.FromArgb(142, 36, 170);
    private static readonly Color AdviceGiveUpColor = Color.FromArgb(217, 48, 37);
    private static readonly Color AdviceNoneColor = Color.FromArgb(95, 99, 104);

    private string? _lastScreenshotPath;
    private Modules.Ocr.EquipmentInfo? _lastInfo;
    private Modules.Ocr.OcrEngine? _ocrEngine;
    private Keys _registeredRecognitionHotKey = Keys.None;
    private bool _isRecognizing;
    private bool _isUpdatingHotKeySelection;
    private Icon? _applicationIcon;
    // AntdUI.Select 不支持 DataSource 绑定，设备列表单独保存，SelectedIndex 对应下标。
    private List<AdbDeviceInfo> _devices = new();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainForm()
    {
        InitializeComponent();
        using var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("TiezhuToolbox.AppIcon.ico");
        if (iconStream is not null)
        {
            using var embeddedIcon = new Icon(iconStream);
            _applicationIcon = (Icon)embeddedIcon.Clone();
            Icon = _applicationIcon;
        }

        InitializeTabsAndSettings();
        DoubleBuffered = true;
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        RefreshDeviceList();
        ApplyRecognitionAvailability(showHotKeySuccess: false);
    }

    private void RefreshDeviceList()
    {
        if (AdbHelper.FindAdbPath() == null)
        {
            _devices = new List<AdbDeviceInfo>();
            comboDevices.Items.Clear();
            UpdateStatus("未找到 adb.exe，请将 platform-tools 的 adb.exe 放到程序目录或加入 PATH");
            return;
        }

        try
        {
            var devices = AdbHelper.GetDevices();
            _devices = devices;
            comboDevices.Items.Clear();
            foreach (var device in devices)
                comboDevices.Items.Add(device.ToString());

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

    /// <summary>
    /// 截图 + 识别一步完成：截取当前设备画面并保存，随后 OCR 识别并刷新装备信息与角色推荐。
    /// </summary>
    private async void btnCaptureRecognize_Click(object sender, EventArgs e)
    {
        await CaptureAndRecognizeAsync();
    }

    private async Task CaptureAndRecognizeAsync()
    {
        if (_isRecognizing)
            return;

        var deviceIndex = comboDevices.SelectedIndex;
        if (deviceIndex < 0 || deviceIndex >= _devices.Count)
        {
            if (chkContinuousRecognition.Checked)
                chkContinuousRecognition.Checked = false;

            UpdateStatus("请先选择一个设备");
            return;
        }

        var device = _devices[deviceIndex];

        _isRecognizing = true;
        var isContinuous = chkContinuousRecognition.Checked;
        if (!isContinuous)
            btnCaptureRecognize.Enabled = false;

        Bitmap? capturedBitmap = null;
        try
        {
            if (!isContinuous)
                UpdateStatus("正在截图...");

            capturedBitmap = await Task.Run(() => AdbHelper.ScreenshotPng(device.Serial));

            var baseName = "adb_" + string.Join("_", device.Serial.Split(Path.GetInvalidFileNameChars()));
            _lastScreenshotPath = ScreenshotHelper.SaveBitmap(capturedBitmap, baseName);

            if (!isContinuous)
                UpdateStatus("正在识别...");

            var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates");
            _ocrEngine ??= new Modules.Ocr.OcrEngine(templateDir);
            var info = await _ocrEngine.RecognizeAsync(_lastScreenshotPath);

            // 强化动画期间面板会短暂消失或只显示部分字段，持续模式下保留上一份有效结果。
            if (isContinuous && !IsValidEquipmentResult(info))
                return;

            var resultChanged = !HasSameEquipmentResult(_lastInfo, info);

            if (resultChanged)
            {
                pictureBox.Image?.Dispose();
                pictureBox.Image = capturedBitmap;
                capturedBitmap = null;

                ShowEquipmentInfo(info);
                UpdateStatus($"识别完成：等级 {info.Level}，民间分数 {info.Score:0.##}");
            }
            else if (!isContinuous)
            {
                UpdateStatus($"识别完成：结果未变化，民间分数 {info.Score:0.##}");
            }

            WriteDebugLog($"识别成功\n截图路径: {_lastScreenshotPath}\n原始文本:\n{info.RawText}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"操作失败：{ex.Message}");
            WriteDebugLog($"截图识别失败：{ex}");
        }
        finally
        {
            capturedBitmap?.Dispose();
            _isRecognizing = false;
            if (!isContinuous)
                btnCaptureRecognize.Enabled = true;
        }
    }

    private void comboRecognitionHotKey_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
    {
        if (_isLoadingSettings || _isUpdatingHotKeySelection)
            return;
        SaveSettingsFromControls();
        if (IsHandleCreated && IsEquipmentTabActive)
            RegisterSelectedRecognitionHotKey(showSuccess: true);
    }

    private void RegisterSelectedRecognitionHotKey(bool showSuccess)
    {
        if (!IsEquipmentTabActive)
            return;
        // AntdUI.Select 的选中项通过 SelectedValue 读取（Items 里存的就是 "F1"~"F12" 字符串）。
        var selectedText = comboRecognitionHotKey.SelectedValue as string ?? comboRecognitionHotKey.Text;
        if (!Enum.TryParse<Keys>(selectedText, out var selectedKey))
            return;

        var previousKey = _registeredRecognitionHotKey;
        if (previousKey != Keys.None)
            UnregisterHotKey(Handle, RecognitionHotKeyId);

        if (RegisterHotKey(Handle, RecognitionHotKeyId, 0, (uint)selectedKey))
        {
            _registeredRecognitionHotKey = selectedKey;
            if (showSuccess)
                UpdateStatus($"识别快捷键已设置为 {selectedKey}");
            return;
        }

        _registeredRecognitionHotKey = Keys.None;
        if (previousKey != Keys.None && RegisterHotKey(Handle, RecognitionHotKeyId, 0, (uint)previousKey))
            _registeredRecognitionHotKey = previousKey;

        _isUpdatingHotKeySelection = true;
        comboRecognitionHotKey.SelectedValue = _registeredRecognitionHotKey == Keys.None
            ? "F2"
            : _registeredRecognitionHotKey.ToString();
        _isUpdatingHotKeySelection = false;

        SaveSettingsFromControls();

        UpdateStatus($"无法注册快捷键 {selectedKey}，可能已被其他程序占用");
    }

    private void chkContinuousRecognition_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
    {
        if (_isLoadingSettings)
            return;
        continuousRecognitionTimer.Enabled = IsEquipmentTabActive && chkContinuousRecognition.Checked;
        SaveSettingsFromControls();
        UpdateStatus(chkContinuousRecognition.Checked
            ? $"持续识别已开启，最短间隔 {numRecognitionInterval.Value:0.0} 秒"
            : "持续识别已关闭");
    }

    private void numRecognitionInterval_ValueChanged(object sender, AntdUI.DecimalEventArgs e)
    {
        continuousRecognitionTimer.Interval = Math.Max(100, (int)(numRecognitionInterval.Value * 1000));
        SaveSettingsFromControls();
    }

    private async void continuousRecognitionTimer_Tick(object sender, EventArgs e)
    {
        if (!_isRecognizing)
            await CaptureAndRecognizeAsync();
    }

    protected override void WndProc(ref Message m)
    {
        if (IsEquipmentTabActive && m.Msg == WmHotKey && m.WParam.ToInt32() == RecognitionHotKeyId)
            _ = CaptureAndRecognizeAsync();

        base.WndProc(ref m);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_registeredRecognitionHotKey != Keys.None)
        {
            UnregisterHotKey(Handle, RecognitionHotKeyId);
            _registeredRecognitionHotKey = Keys.None;
        }

        base.OnHandleDestroyed(e);
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

    /// <summary>展开/收起底部截图预览面板。</summary>
    private void btnToggleScreenshot_Click(object sender, EventArgs e)
    {
        pnlScreenshot.Visible = !pnlScreenshot.Visible;
        btnToggleShot.Text = pnlScreenshot.Visible ? "收起截图" : "查看截图";
    }

    private void ShowEquipmentInfo(Modules.Ocr.EquipmentInfo info)
    {
        _lastInfo = info;

        var meta = $"等级 {info.Level} · 强化 +{info.EnhanceLevel}";
        if (!string.IsNullOrEmpty(info.Quality))
            meta += $" · {info.Quality}";
        lblMeta.Text = meta;

        lblScoreValue.Text = info.Score.ToString("0.##");
        lblMainStat.Text = $"主属性：{info.MainStatName} {info.MainStatValue}";

        listSubStats.Items.Clear();
        foreach (var sub in info.SubStats)
        {
            var rollText = sub.RollCount > 0 ? $"({sub.RollCount})" : string.Empty;
            var text = $"{sub.Name}{rollText} {sub.Value}";
            if (!string.IsNullOrEmpty(sub.EnhanceValue))
                text += $" ({sub.EnhanceValue})";
            listSubStats.Items.Add(text);
        }

        lblSet.Text = $"套装：{info.SetName}";

        UpdateAdvice();
        ShowHeroRecommendations(info);
    }

    /// <summary>比较会影响界面展示与推荐结果的装备字段，忽略每轮都可能不同的 OCR 调试文本。</summary>
    private static bool HasSameEquipmentResult(
        Modules.Ocr.EquipmentInfo? previous,
        Modules.Ocr.EquipmentInfo current)
    {
        if (previous == null ||
            previous.Level != current.Level ||
            previous.EnhanceLevel != current.EnhanceLevel ||
            previous.Quality != current.Quality ||
            previous.MainStatName != current.MainStatName ||
            previous.MainStatValue != current.MainStatValue ||
            previous.SetName != current.SetName ||
            Math.Abs(previous.Score - current.Score) > 0.001 ||
            previous.SubStats.Count != current.SubStats.Count)
        {
            return false;
        }

        return previous.SubStats.Zip(current.SubStats).All(pair =>
            pair.First.Name == pair.Second.Name &&
            pair.First.Value == pair.Second.Value &&
            pair.First.EnhanceValue == pair.Second.EnhanceValue);
    }

    /// <summary>持续识别只接收结构完整的装备数据，避免强化动画中的残缺画面覆盖当前结果。</summary>
    private static bool IsValidEquipmentResult(Modules.Ocr.EquipmentInfo info)
    {
        if (info.Level is <= 0 or > 100 ||
            (info.EnhanceLevel != 0 && info.EnhanceLevel is not (3 or 6 or 9 or 12 or 15)) ||
            string.IsNullOrWhiteSpace(info.Quality) ||
            string.IsNullOrWhiteSpace(info.MainStatName) ||
            string.IsNullOrWhiteSpace(info.MainStatValue) ||
            string.IsNullOrWhiteSpace(info.SetName) ||
            info.SubStats.Count is < 1 or > 4 ||
            !double.IsFinite(info.Score) ||
            info.Score <= 0 ||
            info.RawText.Contains("[OCR 失败:", StringComparison.Ordinal))
        {
            return false;
        }

        return info.SubStats.All(sub =>
            !string.IsNullOrWhiteSpace(sub.Name) &&
            !string.IsNullOrWhiteSpace(sub.Value));
    }

    /// <summary>按当前阈值计算并展示强化建议（识别完成或阈值变更时调用）。</summary>
    private void UpdateAdvice()
    {
        if (_lastInfo == null)
            return;

        var result = Modules.Recommend.EnhancementAdvisor.Analyze(
            _lastInfo, (double)numLeftThreshold.Value, (double)numRightThreshold.Value,
            (double)numLevel88Threshold.Value, (double)_numHeroMatchThreshold.Value,
            _chkHeroicOnlyGambleSpeed.Checked);

        lblAdviceBadge.Text = result.Text;
        lblAdviceBadge.BackColor = result.Advice switch
        {
            Modules.Recommend.EnhanceAdvice.Continue => AdviceContinueColor,
            Modules.Recommend.EnhanceAdvice.Keep => AdviceContinueColor,
            Modules.Recommend.EnhanceAdvice.GambleSpeed => AdviceGambleColor,
            Modules.Recommend.EnhanceAdvice.Reforge => AdviceReforgeColor,
            Modules.Recommend.EnhanceAdvice.GiveUp => AdviceGiveUpColor,
            Modules.Recommend.EnhanceAdvice.GiveUpFixedMain => AdviceGiveUpColor,
            _ => AdviceNoneColor,
        };
        lblAdviceDetail.Text = result.Detail;
    }

    private void numThreshold_ValueChanged(object sender, AntdUI.DecimalEventArgs e)
    {
        SaveSettingsFromControls();
        UpdateAdvice();
    }

    /// <summary>
    /// 根据识别出的装备信息，展示官方战绩（前排分段）匹配出的适用角色（头像 + 名字 + 匹配度）。
    /// </summary>
    private void ShowHeroRecommendations(Modules.Ocr.EquipmentInfo info)
    {
        // 清空旧的推荐项并释放头像图片
        var oldItems = flowHeroes.Controls.Cast<Control>().ToList();
        flowHeroes.Controls.Clear();
        foreach (var control in oldItems)
            control.Dispose();

        if (!Modules.Recommend.HeroDatabase.Instance.IsLoaded)
        {
            lblHeroesTitle.Text = "适用角色（缺少 heroes.json，请先运行 tools/HeroDataCollector）";
            return;
        }

        var recommendations = Modules.Recommend.HeroRecommender.Recommend(info);
        lblHeroesTitle.Text = recommendations.Count > 0 ? "适用角色" : "适用角色（无匹配）";

        foreach (var rec in recommendations)
        {
            // 这些控件是运行时创建的，不会经过 Designer 的自动缩放，需要按当前显示器 DPI 换算。
            var item = new Panel
            {
                Width = ScalePixel(172),
                Height = ScalePixel(62),
                Margin = new Padding(0, 0, ScalePixel(10), ScalePixel(10)),
                BackColor = Color.FromArgb(248, 249, 250),
            };

            var avatar = new PictureBox
            {
                Location = new Point(ScalePixel(9), ScalePixel(9)),
                Size = new Size(ScalePixel(44), ScalePixel(44)),
                SizeMode = PictureBoxSizeMode.Zoom,
            };
            if (rec.AvatarPath != null)
                avatar.Image = LoadImageNoLock(rec.AvatarPath);

            var nameLabel = new Label
            {
                Location = new Point(ScalePixel(62), ScalePixel(11)),
                Size = new Size(ScalePixel(104), ScalePixel(20)),
                Font = HeroNameFont,
                ForeColor = TextDarkColor,
                Text = rec.Name,
                AutoEllipsis = true,
            };

            var scoreLabel = new Label
            {
                Location = new Point(ScalePixel(62), ScalePixel(33)),
                Size = new Size(ScalePixel(104), ScalePixel(20)),
                Font = HeroScoreFont,
                ForeColor = AccentColor,
                Text = $"匹配度 {rec.Score:0.#}%",
                AutoEllipsis = true,
            };

            if (rec.MatchedStats.Count > 0)
            {
                var tip = $"命中属性：{string.Join("、", rec.MatchedStats)}";
                toolTip.SetToolTip(item, tip);
                toolTip.SetToolTip(avatar, tip);
                toolTip.SetToolTip(nameLabel, tip);
                toolTip.SetToolTip(scoreLabel, tip);
            }

            item.Controls.Add(avatar);
            item.Controls.Add(nameLabel);
            item.Controls.Add(scoreLabel);
            flowHeroes.Controls.Add(item);
        }
    }

    private int ScalePixel(int logicalPixel)
    {
        return (int)Math.Round(logicalPixel * DeviceDpi / 96D);
    }

    /// <summary>读文件加载图片且不占用文件句柄（避免锁住 Assets 下的头像）。</summary>
    private static Image LoadImageNoLock(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new Bitmap(new MemoryStream(bytes));
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
