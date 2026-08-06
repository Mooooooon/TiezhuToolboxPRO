using TiezhuToolbox.Modules.GearScan;

namespace TiezhuToolbox;

public partial class MainForm
{
    private AntdUI.TabPage _gearScanTab = null!;
    private AntdUI.Button _btnGearScanStart = null!;
    private AntdUI.Button _btnGearScanStop = null!;
    private AntdUI.Button _btnGearScanExport = null!;
    private AntdUI.Button _btnGearScanClearLog = null!;
    private AntdUI.Select _comboGearScanMinimumEnhance = null!;
    private AntdUI.Select _comboGearScanHeroFilter = null!;
    private AntdUI.Checkbox _chkGearScanKeepCapture = null!;
    private Label _lblGearScanState = null!;
    private Label _lblGearScanStats = null!;
    private RichTextBox _gearScanLog = null!;
    private GearScanCapture? _gearScanCapture;
    private GearScanResult? _gearScanResult;
    private CancellationTokenSource? _gearScanCancellation;
    private int _activeGearScanMinimumEnhance = 6;
    private GearScanHeroFilter _activeGearScanHeroFilter = GearScanHeroFilter.All;

    private const string HeroFilterAllText = "全部英雄";
    private const string HeroFilterFiveText = "至少5星且5觉醒";
    private const string HeroFilterSixText = "仅6星6觉醒";

    private bool IsGearScanning => _gearScanCapture != null;

    private Control CreateGearScanContent()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(245, 246, 248),
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 2,
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var controlCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(22),
            Margin = new Padding(0, 0, 0, 14),
        };
        var title = new Label
        {
            Text = "装备扫描",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(22, 16),
            AutoSize = true,
        };
        var hint = new Label
        {
            Text = "关闭游戏后开始扫描，再启动第七史诗并进入大厅；如需仓库装备，请在停止前打开装备仓库一次。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(24, 55),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(820, 24),
            AutoEllipsis = true,
        };
        var privacy = new Label
        {
            Text = "无需 Python/Npcap/Wireshark，也不会上传游戏数据。抓包使用 Windows 自带 pktmon（开始时需 UAC），解析与导出全部在本机完成。",
            ForeColor = AdviceContinueColor,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Location = new Point(24, 80),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(820, 24),
            AutoEllipsis = true,
        };

        var enhanceLabel = new Label
        {
            Text = "最低强化",
            ForeColor = TextDarkColor,
            Location = new Point(24, 116),
            Size = new Size(72, 34),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _comboGearScanMinimumEnhance = new AntdUI.Select
        {
            List = true,
            ReadOnly = false,
            Location = new Point(96, 116),
            Size = new Size(82, 34),
            Radius = 6,
        };
        _comboGearScanMinimumEnhance.Items.AddRange(new object[] { "+0", "+3", "+6", "+9", "+12", "+15" });
        _comboGearScanMinimumEnhance.SelectedIndexChanged += (_, _) => SaveSettingsFromControls();

        var heroFilterLabel = new Label
        {
            Text = "英雄过滤",
            ForeColor = TextDarkColor,
            Location = new Point(204, 116),
            Size = new Size(72, 34),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _comboGearScanHeroFilter = new AntdUI.Select
        {
            List = true,
            ReadOnly = false,
            Location = new Point(276, 116),
            Size = new Size(176, 34),
            Radius = 6,
        };
        _comboGearScanHeroFilter.Items.AddRange(new object[]
        {
            HeroFilterAllText,
            HeroFilterFiveText,
            HeroFilterSixText,
        });
        _comboGearScanHeroFilter.SelectedIndexChanged += (_, _) => SaveSettingsFromControls();

        _chkGearScanKeepCapture = new AntdUI.Checkbox
        {
            Text = "保留原始抓包用于排错（可能含账号隐私）",
            Checked = false,
            Location = new Point(24, 154),
            Size = new Size(340, 34),
        };

        _btnGearScanStart = new AntdUI.Button
        {
            Text = "开始扫描",
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(632, 164),
            Size = new Size(104, 34),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnGearScanStart.Click += btnGearScanStart_Click;
        _btnGearScanStop = new AntdUI.Button
        {
            Text = "停止并解析",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(744, 164),
            Size = new Size(112, 34),
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = AdviceGiveUpColor,
            ForeColor = AdviceGiveUpColor,
        };
        _btnGearScanStop.Click += btnGearScanStop_Click;
        _btnGearScanExport = new AntdUI.Button
        {
            Text = "导出 gear.txt",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(864, 164),
            Size = new Size(120, 34),
            Radius = 6,
            Enabled = false,
        };
        _btnGearScanExport.Click += btnGearScanExport_Click;

        controlCard.Resize += (_, _) =>
        {
            hint.Width = Math.Max(ScalePixel(360), controlCard.ClientSize.Width - ScalePixel(48));
            privacy.Width = hint.Width;
            _btnGearScanExport.Left = controlCard.ClientSize.Width - ScalePixel(142);
            _btnGearScanStop.Left = _btnGearScanExport.Left - ScalePixel(120);
            _btnGearScanStart.Left = _btnGearScanStop.Left - ScalePixel(112);
        };
        controlCard.Controls.AddRange(new Control[]
        {
            title, hint, privacy, enhanceLabel, _comboGearScanMinimumEnhance,
            heroFilterLabel, _comboGearScanHeroFilter,
            _chkGearScanKeepCapture,
            _btnGearScanStart, _btnGearScanStop, _btnGearScanExport,
        });

        var logCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
            Margin = Padding.Empty,
        };
        var logHeader = new Panel { Dock = DockStyle.Top, Height = 42 };
        var logTitle = new Label
        {
            Text = "扫描日志",
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Dock = DockStyle.Left,
            Width = 88,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblGearScanState = new Label
        {
            Text = "未开始",
            ForeColor = AdviceNoneColor,
            Dock = DockStyle.Left,
            Width = 160,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblGearScanStats = new Label
        {
            Text = "装备 0 · 英雄 0",
            ForeColor = Color.FromArgb(95, 99, 104),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(650, 0),
            Size = new Size(220, 42),
            TextAlign = ContentAlignment.MiddleRight,
        };
        _btnGearScanClearLog = new AntdUI.Button
        {
            Text = "清空日志",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(878, 4),
            Size = new Size(88, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnGearScanClearLog.Click += (_, _) => _gearScanLog.Clear();
        logHeader.Resize += (_, _) =>
        {
            _btnGearScanClearLog.Left = Math.Max(0, logHeader.ClientSize.Width - _btnGearScanClearLog.Width);
            _lblGearScanStats.Left = Math.Max(
                ScalePixel(260),
                _btnGearScanClearLog.Left - _lblGearScanStats.Width - ScalePixel(8));
        };
        logHeader.Controls.AddRange(new Control[] { logTitle, _lblGearScanState, _lblGearScanStats, _btnGearScanClearLog });

        _gearScanLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 249, 250),
            ForeColor = TextDarkColor,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            DetectUrls = false,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };
        logCard.Controls.Add(_gearScanLog);
        logCard.Controls.Add(logHeader);
        host.Controls.Add(controlCard, 0, 0);
        host.Controls.Add(logCard, 0, 1);
        return host;
    }

    private async void btnGearScanStart_Click(object? sender, EventArgs e)
    {
        if (IsGearScanning)
            return;
        if (!GearScanCapture.IsSupported)
        {
            AppendGearScanLog("当前系统没有可用的 pktmon；需要 Windows 10 1809 或更高版本。", AdviceGiveUpColor);
            return;
        }

        _gearScanResult = null;
        _activeGearScanMinimumEnhance = GetGearScanMinimumEnhance();
        _activeGearScanHeroFilter = GetGearScanHeroFilter();
        _gearScanCancellation?.Dispose();
        _gearScanCancellation = new CancellationTokenSource();
        _gearScanCapture = new GearScanCapture();
        SetGearScanUi(running: true, processing: true, state: "正在请求管理员权限…", AdviceGambleColor);
        AppendGearScanLog("正在启动 Windows Packet Monitor；请在 UAC 窗口中允许。", TextDarkColor);
        try
        {
            await _gearScanCapture.StartAsync(_gearScanCancellation.Token);
            SetGearScanUi(running: true, processing: false, state: "扫描中", AdviceContinueColor);
            AppendGearScanLog("扫描已开始。现在启动第七史诗并进入大厅；如需仓库装备，再打开装备仓库一次。", AdviceContinueColor);
            UpdateStatus("装备扫描中：请启动游戏并进入大厅");
        }
        catch (OperationCanceledException ex)
        {
            AppendGearScanLog(ex.Message, AdviceGambleColor);
            await FinishGearScanCaptureAsync(keepDiagnostics: false);
            SetGearScanUi(running: false, processing: false, state: "已取消", AdviceNoneColor);
        }
        catch (Exception ex)
        {
            AppendGearScanLog("开始失败：" + ex.Message, AdviceGiveUpColor);
            WriteDebugLog("装备扫描开始失败：" + ex);
            await FinishGearScanCaptureAsync(keepDiagnostics: false);
            SetGearScanUi(running: false, processing: false, state: "启动失败", AdviceGiveUpColor);
        }
    }

    private async void btnGearScanStop_Click(object? sender, EventArgs e)
    {
        if (_gearScanCapture == null || _gearScanCancellation == null)
            return;

        var capture = _gearScanCapture;
        var keepDiagnostics = _chkGearScanKeepCapture.Checked;
        var succeeded = false;
        SetGearScanUi(running: true, processing: true, state: "停止并读取抓包…", AdviceGambleColor);
        AppendGearScanLog("正在停止抓包并提取目标端口数据…", TextDarkColor);
        try
        {
            var streams = await capture.StopAndExtractAsync(_gearScanCancellation.Token);
            AppendGearScanLog($"已重组 {streams.Count} 段游戏数据，正在本机解密并解析…", TextDarkColor);
            _lblGearScanState.Text = "本地解析中…";

            _gearScanResult = await Task.Run(
                () => new EpicSevenLocalGearParser().Parse(
                    capture.PcapngPath,
                    _activeGearScanMinimumEnhance,
                    _activeGearScanHeroFilter),
                _gearScanCancellation.Token);
            succeeded = true;
            _lblGearScanStats.Text = $"装备 {_gearScanResult.ItemCount} · 英雄 {_gearScanResult.HeroCount}";
            var levelZero = _gearScanResult.LevelZeroItemCount > 0
                ? $"；其中 {_gearScanResult.LevelZeroItemCount} 件等级为 0，导入 Fribbels 后需手动修正"
                : string.Empty;
            var inferredLevel = _gearScanResult.InferredLevelItemCount > 0
                ? $"；已按不可重铸的 88 级装备修复 {_gearScanResult.InferredLevelItemCount} 件未收录活动装备"
                : string.Empty;
            AppendGearScanLog(
                $"解析完成：{_gearScanResult.ItemCount} 件装备、{_gearScanResult.HeroCount} 名英雄" +
                $"（{GetGearScanHeroFilterText(_activeGearScanHeroFilter)}）{levelZero}{inferredLevel}。",
                AdviceContinueColor);
            SetGearScanUi(running: false, processing: false, state: "可导出", AdviceContinueColor);
            UpdateStatus($"装备扫描完成：{_gearScanResult.ItemCount} 件装备，可导出 gear.txt");
        }
        catch (OperationCanceledException)
        {
            AppendGearScanLog("扫描处理已取消。", AdviceGambleColor);
            SetGearScanUi(running: false, processing: false, state: "已取消", AdviceNoneColor);
        }
        catch (Exception ex)
        {
            AppendGearScanLog("扫描失败：" + ex.Message, AdviceGiveUpColor);
            AppendGearScanLog(keepDiagnostics
                ? "诊断抓包已保留在：" + capture.DiagnosticDirectory
                : "原始抓包将按默认隐私设置删除；如需排错，请勾选“保留原始抓包”后重试。", AdviceGambleColor);
            WriteDebugLog("装备扫描处理失败：" + ex);
            SetGearScanUi(running: false, processing: false, state: "扫描失败", AdviceGiveUpColor);
            UpdateStatus("装备扫描失败：" + ex.Message);
        }
        finally
        {
            await FinishGearScanCaptureAsync(keepDiagnostics);
            if (succeeded && keepDiagnostics)
                AppendGearScanLog("已按设置保留诊断抓包：" + capture.DiagnosticDirectory, AdviceGambleColor);
        }
    }

    private void btnGearScanExport_Click(object? sender, EventArgs e)
    {
        if (_gearScanResult == null)
            return;
        using var dialog = new SaveFileDialog
        {
            Title = "导出 Fribbels 装备数据",
            FileName = "gear.txt",
            Filter = "Fribbels 装备数据 (gear.txt)|gear.txt|文本文件 (*.txt)|*.txt",
            AddExtension = true,
            DefaultExt = "txt",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            AppPaths.WriteTextAtomic(dialog.FileName, _gearScanResult.GearText);
            AppendGearScanLog("已导出：" + dialog.FileName, AdviceContinueColor);
            UpdateStatus("gear.txt 导出完成");
        }
        catch (Exception ex)
        {
            AppendGearScanLog("导出失败：" + ex.Message, AdviceGiveUpColor);
        }
    }

    private int GetGearScanMinimumEnhance()
    {
        var value = _comboGearScanMinimumEnhance.SelectedValue as string ?? _comboGearScanMinimumEnhance.Text;
        return int.TryParse(value.TrimStart('+'), out var result) ? result : 6;
    }

    private GearScanHeroFilter GetGearScanHeroFilter()
    {
        var value = _comboGearScanHeroFilter.SelectedValue as string ?? _comboGearScanHeroFilter.Text;
        return value switch
        {
            HeroFilterFiveText => GearScanHeroFilter.AtLeastFiveStarsFiveAwakened,
            HeroFilterSixText => GearScanHeroFilter.SixStarsSixAwakened,
            _ => GearScanHeroFilter.All,
        };
    }

    private static string GetGearScanHeroFilterText(GearScanHeroFilter filter)
        => filter switch
        {
            GearScanHeroFilter.AtLeastFiveStarsFiveAwakened => HeroFilterFiveText,
            GearScanHeroFilter.SixStarsSixAwakened => HeroFilterSixText,
            _ => HeroFilterAllText,
        };

    private void SetGearScanUi(bool running, bool processing, string state, Color stateColor)
    {
        _btnGearScanStart.Enabled = !running && !processing;
        _btnGearScanStop.Enabled = running && !processing;
        _btnGearScanExport.Enabled = !running && !processing && _gearScanResult != null;
        _comboGearScanMinimumEnhance.Enabled = !running;
        _comboGearScanHeroFilter.Enabled = !running;
        _lblGearScanState.Text = state;
        _lblGearScanState.ForeColor = stateColor;
    }

    private void AppendGearScanLog(string message, Color color)
    {
        if (_gearScanLog.TextLength > 0)
            _gearScanLog.AppendText(Environment.NewLine);
        _gearScanLog.SelectionStart = _gearScanLog.TextLength;
        _gearScanLog.SelectionColor = Color.FromArgb(120, 124, 130);
        _gearScanLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
        _gearScanLog.SelectionColor = color;
        _gearScanLog.AppendText(message);
        _gearScanLog.SelectionColor = _gearScanLog.ForeColor;
        _gearScanLog.ScrollToCaret();
    }

    private async Task FinishGearScanCaptureAsync(bool keepDiagnostics)
    {
        var capture = _gearScanCapture;
        _gearScanCapture = null;
        if (capture != null)
        {
            await capture.DisposeAsync();
            capture.Cleanup(keepDiagnostics);
        }
        _gearScanCancellation?.Dispose();
        _gearScanCancellation = null;
    }

    private void RequestGearScanShutdown()
    {
        _gearScanCancellation?.Cancel();
        _gearScanCapture?.RequestAbort();
    }
}
