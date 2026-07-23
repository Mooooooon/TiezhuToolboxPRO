using TiezhuToolbox.Modules.Automation;

namespace TiezhuToolbox;

public partial class MainForm
{
    private AntdUI.TabPage _autoEnhanceTab = null!;
    private AntdUI.Button _btnAutoStart = null!;
    private AntdUI.Button _btnAutoStop = null!;
    private AntdUI.Button _btnAutoClearLog = null!;
    private AntdUI.InputNumber _numAutoMaxEquipment = null!;
    private AntdUI.Select _comboAutoDisposalMethod = null!;
    private AntdUI.InputNumber _numHeroMatchThreshold = null!;
    private AntdUI.Checkbox _chkAutoStopOnValuableEquipment = null!;
    private AntdUI.Checkbox _chkHeroicOnlyGambleSpeed = null!;
    private Label _lblAutoDevice = null!;
    private Label _lblAutoState = null!;
    private Label _lblAutoStats = null!;
    private RichTextBox _autoLog = null!;
    private CancellationTokenSource? _autoEnhanceCancellation;

    private bool IsAutoEnhancing => _autoEnhanceCancellation != null;

    private Control CreateAutoEnhanceContent()
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
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
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
            Text = "自动强化",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Location = new Point(22, 18),
            AutoSize = true,
        };
        var hint = new Label
        {
            Text = "请先在游戏中打开背包装备列表并选中第一件装备。程序会用标题和按钮图片确认位置，无法确认时立即停止。",
            ForeColor = Color.FromArgb(95, 99, 104),
            Location = new Point(24, 57),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(720, 24),
            AutoEllipsis = true,
        };
        var warning = new Label
        {
            Text = "注意：淘汰装备会按设置出售或分解；符合保留条件时按设置停止，或返回背包继续下一件。",
            ForeColor = AdviceGiveUpColor,
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold),
            Location = new Point(24, 83),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(720, 24),
            AutoEllipsis = true,
        };

        _lblAutoDevice = new Label
        {
            Text = "设备：跟随顶部设备选择",
            ForeColor = TextDarkColor,
            Location = new Point(24, 123),
            Size = new Size(300, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        _btnAutoStart = new AntdUI.Button
        {
            Text = "开始自动强化",
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(708, 123),
            Size = new Size(132, 34),
            Radius = 6,
            Type = AntdUI.TTypeMini.Primary,
        };
        _btnAutoStart.Click += btnAutoStart_Click;

        _btnAutoStop = new AntdUI.Button
        {
            Text = "停止",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(848, 123),
            Size = new Size(88, 34),
            Radius = 6,
            Enabled = false,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = AdviceGiveUpColor,
            ForeColor = AdviceGiveUpColor,
        };
        _btnAutoStop.Click += (_, _) =>
        {
            if (_autoEnhanceCancellation == null)
                return;
            AppendAutoLog(AutoEnhancementLogLevel.Warning, "用户请求停止，正在结束当前操作……");
            _autoEnhanceCancellation.Cancel();
            _btnAutoStop.Enabled = false;
        };

        controlCard.Resize += (_, _) =>
        {
            hint.Width = Math.Max(ScalePixel(300), controlCard.ClientSize.Width - ScalePixel(48));
            warning.Width = Math.Max(ScalePixel(300), controlCard.ClientSize.Width - ScalePixel(48));
            _btnAutoStop.Left = controlCard.ClientSize.Width - ScalePixel(110);
            _btnAutoStart.Left = _btnAutoStop.Left - ScalePixel(140);
        };
        controlCard.Controls.AddRange(new Control[]
        {
            title, hint, warning, _lblAutoDevice, _btnAutoStart, _btnAutoStop,
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
            Text = "过程日志",
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = TextDarkColor,
            Dock = DockStyle.Left,
            Width = 88,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblAutoState = new Label
        {
            Text = "未开始",
            ForeColor = AdviceNoneColor,
            Dock = DockStyle.Left,
            Width = 150,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _lblAutoStats = new Label
        {
            Text = "已处理 0 · 强化 0 · 出售 0 · 分解 0",
            ForeColor = Color.FromArgb(95, 99, 104),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(620, 0),
            Size = new Size(300, 42),
            TextAlign = ContentAlignment.MiddleRight,
        };
        _btnAutoClearLog = new AntdUI.Button
        {
            Text = "清空日志",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(858, 4),
            Size = new Size(88, 34),
            Radius = 6,
            BorderWidth = 1,
            DefaultBack = Color.White,
            DefaultBorderColor = Color.FromArgb(218, 220, 224),
        };
        _btnAutoClearLog.Click += (_, _) => _autoLog.Clear();
        logHeader.Resize += (_, _) =>
        {
            _btnAutoClearLog.Left = Math.Max(0, logHeader.ClientSize.Width - _btnAutoClearLog.Width);
            _lblAutoStats.Left = Math.Max(
                ScalePixel(250),
                _btnAutoClearLog.Left - _lblAutoStats.Width - ScalePixel(8));
        };
        logHeader.Controls.AddRange(new Control[] { logTitle, _lblAutoState, _lblAutoStats, _btnAutoClearLog });

        _autoLog = new RichTextBox
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
        logCard.Controls.Add(_autoLog);
        logCard.Controls.Add(logHeader);

        host.Controls.Add(controlCard, 0, 0);
        host.Controls.Add(logCard, 0, 1);
        return host;
    }

    private async void btnAutoStart_Click(object? sender, EventArgs e)
    {
        if (IsAutoEnhancing)
            return;

        var deviceIndex = comboDevices.SelectedIndex;
        if (deviceIndex < 0 || deviceIndex >= _devices.Count)
        {
            AppendAutoLog(AutoEnhancementLogLevel.Error, "请先在“装备强化”页选择一个已连接的 ADB 设备");
            return;
        }

        var disposalMethod = GetSelectedDisposalMethod();
        var disposalName = disposalMethod == EquipmentDisposalMethod.Sell ? "出售" : "分解";
        var confirmation = MessageBox.Show(
            this,
            $"自动强化会永久{disposalName}不符合当前强化建议的装备。\r\n\r\n" +
            "开始前请确认：\r\n" +
            "1. 游戏已停在背包装备列表，并已选中准备处理的第一件装备；\r\n" +
            "2. 已勾选“隐藏已配戴装备”；\r\n" +
            "3. 已勾选“隐藏MAX强化装备”。\r\n\r\n" +
            "以上设置均已完成，是否开始？",
            "确认开始自动强化",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
            return;

        var device = _devices[deviceIndex];
        _autoEnhanceCancellation = new CancellationTokenSource();
        var cancellationToken = _autoEnhanceCancellation.Token;
        _btnAutoStart.Enabled = false;
        _btnAutoStop.Enabled = true;
        _numAutoMaxEquipment.Enabled = false;
        _comboAutoDisposalMethod.Enabled = false;
        _numHeroMatchThreshold.Enabled = false;
        _chkAutoStopOnValuableEquipment.Enabled = false;
        _chkHeroicOnlyGambleSpeed.Enabled = false;
        _lblAutoDevice.Text = $"设备：{device.Serial}";
        _lblAutoState.Text = "运行中";
        _lblAutoState.ForeColor = AdviceContinueColor;
        ApplyRecognitionAvailability(showHotKeySuccess: false);

        var options = AutoEnhancementOptions.CreateDefault(
            (int)_numAutoMaxEquipment.Value,
            (double)numLeftThreshold.Value,
            (double)numRightThreshold.Value,
            (double)numLevel88Threshold.Value,
            (double)_numHeroMatchThreshold.Value,
            disposalMethod,
            _chkAutoStopOnValuableEquipment.Checked,
            _chkHeroicOnlyGambleSpeed.Checked);
        var progress = new Progress<AutoEnhancementProgress>(value =>
        {
            AppendAutoLog(value.Level, value.Message);
            _lblAutoStats.Text = $"已处理 {value.Processed} · 强化 {value.Enhanced} · 出售 {value.Sold} · 分解 {value.Extracted}";
        });
        var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Templates");

        try
        {
            using var runner = new AutoEnhancementRunner(device.Serial, templateDir, options, progress);
            var result = await runner.RunAsync(cancellationToken);
            _lblAutoStats.Text = $"已处理 {result.Processed} · 强化 {result.Enhanced} · 出售 {result.Sold} · 分解 {result.Extracted}";
            _lblAutoState.Text = result.StoppedForValuableEquipment ? "已安全停止" : "已完成";
            _lblAutoState.ForeColor = result.StoppedForValuableEquipment ? AdviceGambleColor : AdviceContinueColor;
            AppendAutoLog(AutoEnhancementLogLevel.Success, result.Message);
            UpdateStatus(result.Message);
        }
        catch (OperationCanceledException)
        {
            _lblAutoState.Text = "已停止";
            _lblAutoState.ForeColor = AdviceGambleColor;
            AppendAutoLog(AutoEnhancementLogLevel.Warning, "自动强化已由用户停止");
            UpdateStatus("自动强化已停止");
        }
        catch (Exception ex)
        {
            _lblAutoState.Text = "发生错误，已停机";
            _lblAutoState.ForeColor = AdviceGiveUpColor;
            AppendAutoLog(AutoEnhancementLogLevel.Error, ex.Message);
            WriteDebugLog($"自动强化失败：{ex}");
            UpdateStatus($"自动强化已停止：{ex.Message}");
        }
        finally
        {
            _autoEnhanceCancellation?.Dispose();
            _autoEnhanceCancellation = null;
            if (!IsDisposed)
            {
                _btnAutoStart.Enabled = true;
                _btnAutoStop.Enabled = false;
                _numAutoMaxEquipment.Enabled = true;
                _comboAutoDisposalMethod.Enabled = true;
                _numHeroMatchThreshold.Enabled = true;
                _chkAutoStopOnValuableEquipment.Enabled = true;
                _chkHeroicOnlyGambleSpeed.Enabled = true;
                ApplyRecognitionAvailability(showHotKeySuccess: false);
            }
        }
    }

    private EquipmentDisposalMethod GetSelectedDisposalMethod()
        => (_comboAutoDisposalMethod.SelectedValue as string ?? _comboAutoDisposalMethod.Text) == "分解"
            ? EquipmentDisposalMethod.Extract
            : EquipmentDisposalMethod.Sell;

    private void AppendAutoLog(AutoEnhancementLogLevel level, string message)
    {
        if (_autoLog == null || _autoLog.IsDisposed)
            return;
        if (_autoLog.InvokeRequired)
        {
            _autoLog.BeginInvoke(() => AppendAutoLog(level, message));
            return;
        }

        var label = level switch
        {
            AutoEnhancementLogLevel.Action => "操作",
            AutoEnhancementLogLevel.Recognition => "识别",
            AutoEnhancementLogLevel.Warning => "警告",
            AutoEnhancementLogLevel.Error => "错误",
            AutoEnhancementLogLevel.Success => "完成",
            _ => "信息",
        };
        var color = level switch
        {
            AutoEnhancementLogLevel.Action => AccentColor,
            AutoEnhancementLogLevel.Recognition => AdviceReforgeColor,
            AutoEnhancementLogLevel.Warning => AdviceGambleColor,
            AutoEnhancementLogLevel.Error => AdviceGiveUpColor,
            AutoEnhancementLogLevel.Success => AdviceContinueColor,
            _ => TextDarkColor,
        };

        _autoLog.SelectionStart = _autoLog.TextLength;
        _autoLog.SelectionLength = 0;
        _autoLog.SelectionColor = color;
        _autoLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] [{label}] {message}{Environment.NewLine}");
        _autoLog.SelectionColor = _autoLog.ForeColor;
        _autoLog.ScrollToCaret();

        // 防止长时间运行后日志控件无限增长。
        if (_autoLog.TextLength > 250_000)
        {
            _autoLog.Select(0, Math.Min(50_000, _autoLog.TextLength));
            _autoLog.SelectedText = string.Empty;
        }
    }
}
