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
    private int _layoutDpi = 96;
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
        _layoutDpi = Math.Max(96, DeviceDpi);
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
                UpdateStatus($"识别完成：等级 {info.Level}，装备分数 {info.Score:0.##}");
            }
            else if (!isContinuous)
            {
                UpdateStatus($"识别完成：结果未变化，装备分数 {info.Score:0.##}");
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
        ShowDemandRecommendations(info);
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
            _chkHeroicOnlyGambleSpeed.Checked,
            _chkSpeedSetRequiresSpeed.Checked,
            _chkCriticalNecklaceMainStatRule.Checked,
            _disabledDemandProfiles);

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

    /// <summary>根据识别出的装备信息，展示当前套装最匹配的属性子类及英雄配装。</summary>
    private void ShowDemandRecommendations(Modules.Ocr.EquipmentInfo info)
    {
        var oldItems = flowHeroes.Controls.Cast<Control>().ToList();
        flowHeroes.Controls.Clear();
        foreach (var control in oldItems)
            control.Dispose();

        var database = Modules.Recommend.DemandDatabase.Instance;
        if (!database.IsLoaded)
        {
            lblHeroesTitle.Text = $"套装需求（数据未加载：{database.ErrorMessage}）";
            return;
        }

        var set = database.FindSet(info.SetName);
        if (set == null)
        {
            lblHeroesTitle.Text = "套装需求（套装未识别）";
            return;
        }
        if (set.Profiles.Count == 0)
        {
            lblHeroesTitle.Text = $"{set.Name}需求（暂无内置数据）";
            return;
        }
        if (set.Profiles.All(profile => _disabledDemandProfiles.Contains(
                Modules.Recommend.SetProfileMatcher.CreateProfileKey(set.Code, profile.Id))))
        {
            lblHeroesTitle.Text = $"{set.Name}需求（全部子类已停用）";
            return;
        }

        var recommendations = Modules.Recommend.SetProfileMatcher.Match(
            info, disabledProfileKeys: _disabledDemandProfiles);
        lblHeroesTitle.Text = recommendations.Count > 0
            ? $"{set.Name}适用子类"
            : $"{set.Name}需求（装备属性无匹配）";
        flowHeroes.FlowDirection = FlowDirection.TopDown;
        flowHeroes.WrapContents = false;

        var cardWidth = Math.Max(ScalePixel(430),
            flowHeroes.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - ScalePixel(8));
        foreach (var rec in recommendations)
        {
            const int collapsedLogicalHeight = 86;
            var collapsedHeight = ScalePixel(collapsedLogicalHeight);
            var heroRowHeight = ScalePixel(52);
            var card = new Panel
            {
                Width = cardWidth,
                Height = collapsedHeight,
                Margin = new Padding(0, 0, 0, ScalePixel(10)),
                BackColor = Color.FromArgb(248, 249, 250),
            };
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = collapsedHeight,
                Cursor = Cursors.Hand,
            };
            var nameLabel = new Label
            {
                Text = rec.ProfileName,
                Location = new Point(ScalePixel(12), ScalePixel(9)),
                Size = new Size(cardWidth - ScalePixel(145), ScalePixel(24)),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = TextDarkColor,
                AutoEllipsis = true,
            };
            var scoreLabel = new Label
            {
                Text = $"{rec.Score:0.#}%",
                Location = new Point(cardWidth - ScalePixel(116), ScalePixel(9)),
                Size = new Size(ScalePixel(82), ScalePixel(24)),
                Font = HeroScoreFont,
                ForeColor = AccentColor,
                TextAlign = ContentAlignment.MiddleRight,
            };
            var toggleLabel = new Label
            {
                Text = "▼",
                Location = new Point(cardWidth - ScalePixel(31), ScalePixel(10)),
                Size = new Size(ScalePixel(20), ScalePixel(22)),
                ForeColor = Color.FromArgb(95, 99, 104),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            var statsLabel = new Label
            {
                Text = $"命中：{string.Join("、", rec.MatchedStats)}　需求权重 {rec.DemandWeight:0.##}",
                Location = new Point(ScalePixel(12), ScalePixel(37)),
                Size = new Size(cardWidth - ScalePixel(24), ScalePixel(20)),
                ForeColor = Color.FromArgb(70, 72, 76),
                AutoEllipsis = true,
            };
            var mainLabel = new Label
            {
                Text = string.IsNullOrWhiteSpace(rec.MainStatContribution)
                    ? "左三固定主属性不参与匹配"
                    : rec.MainStatContribution,
                Location = new Point(ScalePixel(12), ScalePixel(60)),
                Size = new Size(cardWidth - ScalePixel(24), ScalePixel(18)),
                ForeColor = Color.FromArgb(95, 99, 104),
                AutoEllipsis = true,
            };
            header.Controls.Add(nameLabel);
            header.Controls.Add(scoreLabel);
            header.Controls.Add(toggleLabel);
            header.Controls.Add(statsLabel);
            header.Controls.Add(mainLabel);

            var builds = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                AutoScroll = rec.Heroes.Count * heroRowHeight > ScalePixel(330),
                BackColor = Color.White,
            };
            var y = 0;
            foreach (var hero in rec.Heroes)
            {
                var row = CreateHeroBuildRecommendationRow(hero, cardWidth, heroRowHeight, y);
                builds.Controls.Add(row);
                y += heroRowHeight;
            }

            void ToggleExpanded(object? _, EventArgs __)
            {
                builds.Visible = !builds.Visible;
                toggleLabel.Text = builds.Visible ? "▲" : "▼";
                card.Height = builds.Visible
                    ? collapsedHeight + Math.Min(y, ScalePixel(330))
                    : collapsedHeight;
            }

            header.Click += ToggleExpanded;
            foreach (Control child in header.Controls)
            {
                child.Cursor = Cursors.Hand;
                child.Click += ToggleExpanded;
            }
            card.Controls.Add(builds);
            card.Controls.Add(header);
            flowHeroes.Controls.Add(card);
        }
    }

    private Panel CreateHeroBuildRecommendationRow(
        Modules.Recommend.HeroBuildRecommendation hero,
        int width,
        int height,
        int top)
    {
        var row = new Panel
        {
            Location = new Point(0, top),
            Size = new Size(width - ScalePixel(4), height),
            BackColor = Color.White,
        };
        var avatar = new PictureBox
        {
            Location = new Point(ScalePixel(12), ScalePixel(6)),
            Size = new Size(ScalePixel(40), ScalePixel(40)),
            SizeMode = PictureBoxSizeMode.Zoom,
        };
        if (hero.AvatarPath != null)
            avatar.Image = LoadImageNoLock(hero.AvatarPath);
        var name = new Label
        {
            Text = hero.Name,
            Location = new Point(ScalePixel(62), ScalePixel(6)),
            Size = new Size(ScalePixel(150), ScalePixel(20)),
            Font = HeroNameFont,
            AutoEllipsis = true,
        };
        var combo = new Label
        {
            Text = $"{hero.ComboName} · 样本 {hero.SampleShare:P1} · 需求 {hero.DemandContribution:0.###}",
            Location = new Point(ScalePixel(62), ScalePixel(27)),
            Size = new Size(Math.Max(ScalePixel(160), width - ScalePixel(205)), ScalePixel(19)),
            ForeColor = Color.FromArgb(95, 99, 104),
            AutoEllipsis = true,
        };
        var score = new Label
        {
            Text = $"{hero.Score:0.#}%",
            Location = new Point(width - ScalePixel(92), ScalePixel(15)),
            Size = new Size(ScalePixel(70), ScalePixel(22)),
            ForeColor = AccentColor,
            Font = HeroScoreFont,
            TextAlign = ContentAlignment.MiddleRight,
        };
        row.Controls.Add(avatar);
        row.Controls.Add(name);
        row.Controls.Add(combo);
        row.Controls.Add(score);
        toolTip.SetToolTip(row, $"命中属性：{string.Join("、", hero.MatchedStats)}");
        return row;
    }

    private void LblScoreHelp_Paint(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = lblScoreHelp.ClientRectangle;
        bounds.Inflate(-1, -1);

        using var circlePen = new Pen(lblScoreHelp.ForeColor, 1.2F);
        e.Graphics.DrawEllipse(circlePen, bounds);

        var scale = Math.Min(bounds.Width, bounds.Height) / 15F;
        var centerX = bounds.Left + bounds.Width / 2F;
        using var questionPen = new Pen(lblScoreHelp.ForeColor, Math.Max(1.2F, 1.4F * scale))
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
        };

        using var questionPath = new System.Drawing.Drawing2D.GraphicsPath();
        questionPath.AddBezier(
            centerX - 2.4F * scale, bounds.Top + 5.1F * scale,
            centerX - 2.1F * scale, bounds.Top + 2.6F * scale,
            centerX + 2.7F * scale, bounds.Top + 2.5F * scale,
            centerX + 2.7F * scale, bounds.Top + 5.2F * scale);
        questionPath.AddBezier(
            centerX + 2.7F * scale, bounds.Top + 5.2F * scale,
            centerX + 2.7F * scale, bounds.Top + 7.2F * scale,
            centerX, bounds.Top + 7.1F * scale,
            centerX, bounds.Top + 9.2F * scale);
        e.Graphics.DrawPath(questionPen, questionPath);

        var dotSize = Math.Max(1.5F, 1.7F * scale);
        using var dotBrush = new SolidBrush(lblScoreHelp.ForeColor);
        e.Graphics.FillEllipse(
            dotBrush,
            centerX - dotSize / 2F,
            bounds.Top + 11.2F * scale,
            dotSize,
            dotSize);
    }

    private int ScalePixel(int logicalPixel)
    {
        return (int)Math.Round(logicalPixel * _layoutDpi / 96D);
    }

    /// <summary>
    /// 设计器控件会在 InitializeComponent 中自动缩放；构造函数中动态创建的页面错过了该阶段，
    /// 因此在首次加入窗体时按当前显示器 DPI 补做一次边界缩放。
    /// </summary>
    private void ScaleRuntimePage(Control page)
    {
        if (_layoutDpi == 96)
            return;

        var factor = _layoutDpi / 96F;
        page.SuspendLayout();
        page.Scale(new SizeF(factor, factor));
        page.ResumeLayout(performLayout: true);
    }

    /// <summary>
    /// 从设计器页面移出的控件已经随窗体缩放过；它们将被放入随后统一缩放的动态页面，
    /// 先还原到 96 DPI 逻辑尺寸，避免边距和标签宽度被重复放大。
    /// </summary>
    private void NormalizeDesignerControlForRuntime(Control control)
    {
        if (_layoutDpi == 96)
            return;

        var factor = 96F / _layoutDpi;
        control.SuspendLayout();
        control.Scale(new SizeF(factor, factor));
        control.ResumeLayout(performLayout: false);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        _layoutDpi = Math.Max(96, e.DeviceDpiNew);
        _demandBrowserControl?.PrepareForDpiChange(_layoutDpi);
        base.OnDpiChanged(e);
        _demandBrowserControl?.CompleteDpiChange();
        LayoutTopToolbar();
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
