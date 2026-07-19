using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

public partial class MainForm
{
    private readonly AppSettings _settings = AppSettingsStore.Load();
    private AntdUI.Tabs _mainTabs = null!;
    private AntdUI.TabPage _equipmentTab = null!;
    private HeroConfigControl _heroConfigControl = null!;
    private CancellationTokenSource? _heroUpdateCancellation;
    private bool _isLoadingSettings;

    private bool IsEquipmentTabActive => _mainTabs.SelectedTab == _equipmentTab;

    private void InitializeTabsAndSettings()
    {
        SuspendLayout();
        Controls.Remove(topPanel);
        Controls.Remove(mainTable);
        Controls.Remove(pnlScreenshot);

        equipTable.Controls.Remove(settingsDivider);
        equipTable.Controls.Remove(thresholdPanel);
        equipTable.Controls.Remove(recognitionSettingsPanel);
        while (equipTable.RowStyles.Count > 8)
            equipTable.RowStyles.RemoveAt(equipTable.RowStyles.Count - 1);
        equipTable.RowCount = 8;

        _mainTabs = new AntdUI.Tabs
        {
            Dock = DockStyle.Fill,
            Type = AntdUI.TabType.Line,
            Gap = 28,
            Padding = new Padding(0),
        };
        _equipmentTab = new AntdUI.TabPage { Text = "装备强化", BackColor = Color.White };
        var heroTab = new AntdUI.TabPage { Text = "英雄配置", BackColor = Color.White };
        var settingsTab = new AntdUI.TabPage { Text = "软件设置", BackColor = Color.FromArgb(245, 246, 248) };

        _equipmentTab.Controls.Add(mainTable);
        _equipmentTab.Controls.Add(pnlScreenshot);
        _equipmentTab.Controls.Add(topPanel);
        foreach (var control in new Control[]
                 { comboDevices, txtAddress, btnConnect, btnRefresh, btnOpenFolder, btnToggleShot, btnCaptureRecognize })
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        // Select.List 是 AntdUI 提供的不可编辑选择模式：禁止文字输入，但仍可展开下拉。
        comboDevices.ReadOnly = false;
        comboDevices.List = true;
        topPanel.Resize += (_, _) => LayoutTopToolbar();

        _heroConfigControl = new HeroConfigControl();
        _heroConfigControl.UpdateRequested += async (_, _) => await UpdateHeroDataAsync();
        _heroConfigControl.CancelUpdateRequested += (_, _) => _heroUpdateCancellation?.Cancel();
        heroTab.Controls.Add(_heroConfigControl);
        settingsTab.Controls.Add(CreateSettingsContent());

        _mainTabs.Pages.Add(_equipmentTab);
        _mainTabs.Pages.Add(heroTab);
        _mainTabs.Pages.Add(settingsTab);
        _mainTabs.SelectedIndex = 0;
        _mainTabs.SelectedIndexChanged += MainTabs_SelectedIndexChanged;
        Controls.Add(_mainTabs);
        Controls.SetChildIndex(_mainTabs, 0);
        Controls.SetChildIndex(statusStrip, 1);

        LoadSettingsIntoControls();
        txtAddress.Leave += (_, _) => SaveSettingsFromControls();
        HeroDatabase.Instance.Changed += HeroDatabase_Changed;
        ResumeLayout(performLayout: true);
        LayoutTopToolbar();
    }

    private void LayoutTopToolbar()
    {
        const int margin = 12;
        const int gap = 8;
        // 设计器控件会随 DPI 自动缩放；运行时重排坐标要换回 96 DPI 逻辑宽度，
        // 否则高分屏下会把右侧按钮排到窗口可视区域之外。
        var logicalWidth = (int)Math.Round(topPanel.ClientSize.Width * 96D / Math.Max(96, DeviceDpi));
        var right = logicalWidth - margin;
        PlaceFromRight(btnCaptureRecognize, 112, ref right, gap);
        PlaceFromRight(btnToggleShot, 92, ref right, gap);
        PlaceFromRight(btnOpenFolder, 76, ref right, gap);
        PlaceFromRight(btnRefresh, 76, ref right, gap);
        PlaceFromRight(btnConnect, 76, ref right, gap);
        PlaceFromRight(txtAddress, 210, ref right, gap);
        comboDevices.Location = new Point(margin, 15);
        comboDevices.Size = new Size(Math.Max(180, right - margin), 34);
    }

    private static void PlaceFromRight(Control control, int width, ref int right, int gap)
    {
        right -= width;
        control.Location = new Point(right, 15);
        control.Size = new Size(width, 34);
        right -= gap;
    }

    private Control CreateSettingsContent()
    {
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };
        var card = new Panel
        {
            BackColor = Color.White,
            Location = new Point(24, 24),
            Size = new Size(720, 390),
            Padding = new Padding(24),
        };
        host.Resize += (_, _) => card.Width = Math.Min(760, Math.Max(560, host.ClientSize.Width - 48));

        var title = new Label
        {
            Text = "软件设置",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 48,
        };
        var scoreTitle = CreateSettingsHeading("强化分数", "85级按左/右三件阈值每跳 +6；88级使用独立阈值，每跳 +7。", 62);
        thresholdPanel.Dock = DockStyle.None;
        thresholdPanel.Location = new Point(24, 132);
        // 高 DPI 下 FlowLayoutPanel 会按缩放后的 Margin 排列子控件，预留足够宽度避免最右侧 88 级输入框被裁剪。
        thresholdPanel.Size = new Size(700, 34);
        thresholdPanel.AutoSize = false;
        thresholdPanel.Margin = Padding.Empty;
        numLeftThreshold.Size = new Size(82, 34);
        numRightThreshold.Size = new Size(82, 34);
        numLevel88Threshold.Size = new Size(82, 34);
        foreach (var label in new[] { lblThresholdGroup, lblThLeft, lblThRight, lblTh88 })
            label.Height = 34;

        var recognitionTitle = CreateSettingsHeading("识别控制", "全局快捷键和持续识别只在“装备强化”页生效。", 188);
        recognitionSettingsPanel.Dock = DockStyle.None;
        recognitionSettingsPanel.Location = new Point(24, 258);
        recognitionSettingsPanel.Size = new Size(620, 34);
        recognitionSettingsPanel.AutoSize = false;
        recognitionSettingsPanel.Margin = Padding.Empty;
        comboRecognitionHotKey.Size = new Size(76, 34);
        chkContinuousRecognition.Size = new Size(108, 34);
        numRecognitionInterval.Size = new Size(88, 34);
        foreach (var label in new[] { lblRecognitionGroup, lblRecognitionHotKey, lblRecognitionInterval, lblIntervalUnit })
            label.Height = 34;

        var reset = new AntdUI.Button
        {
            Text = "恢复默认设置",
            Location = new Point(24, 324),
            Size = new Size(120, 34),
            Radius = 6,
        };
        reset.Click += (_, _) => ResetSettings();

        card.Controls.Add(reset);
        card.Controls.Add(recognitionSettingsPanel);
        card.Controls.Add(recognitionTitle);
        card.Controls.Add(thresholdPanel);
        card.Controls.Add(scoreTitle);
        card.Controls.Add(title);
        host.Controls.Add(card);
        return host;
    }

    private static Label CreateSettingsHeading(string title, string description, int top)
        => new()
        {
            Text = $"{title}\r\n{description}",
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 33, 36),
            Location = new Point(24, top),
            Size = new Size(650, 52),
        };

    private void LoadSettingsIntoControls()
    {
        _isLoadingSettings = true;
        try
        {
            numLeftThreshold.Value = _settings.LeftThreshold;
            numRightThreshold.Value = _settings.RightThreshold;
            numLevel88Threshold.Value = _settings.Level88Threshold;
            comboRecognitionHotKey.SelectedValue = _settings.RecognitionHotKey;
            chkContinuousRecognition.Checked = _settings.ContinuousRecognition;
            numRecognitionInterval.Value = _settings.RecognitionIntervalSeconds;
            continuousRecognitionTimer.Interval = Math.Max(100, (int)(_settings.RecognitionIntervalSeconds * 1000));
            txtAddress.Text = _settings.AdbAddress;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void SaveSettingsFromControls()
    {
        if (_isLoadingSettings)
            return;
        _settings.LeftThreshold = numLeftThreshold.Value;
        _settings.RightThreshold = numRightThreshold.Value;
        _settings.Level88Threshold = numLevel88Threshold.Value;
        _settings.RecognitionHotKey = comboRecognitionHotKey.SelectedValue as string
            ?? comboRecognitionHotKey.Text;
        _settings.ContinuousRecognition = chkContinuousRecognition.Checked;
        _settings.RecognitionIntervalSeconds = numRecognitionInterval.Value;
        _settings.AdbAddress = txtAddress.Text.Trim();
        try
        {
            AppSettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            UpdateStatus($"保存设置失败：{ex.Message}");
            WriteDebugLog($"保存设置失败：{ex}");
        }
    }

    private void ResetSettings()
    {
        if (_registeredRecognitionHotKey != Keys.None)
        {
            UnregisterHotKey(Handle, RecognitionHotKeyId);
            _registeredRecognitionHotKey = Keys.None;
        }
        var defaults = AppSettings.CreateDefault();
        _settings.LeftThreshold = defaults.LeftThreshold;
        _settings.RightThreshold = defaults.RightThreshold;
        _settings.Level88Threshold = defaults.Level88Threshold;
        _settings.RecognitionHotKey = defaults.RecognitionHotKey;
        _settings.ContinuousRecognition = defaults.ContinuousRecognition;
        _settings.RecognitionIntervalSeconds = defaults.RecognitionIntervalSeconds;
        _settings.AdbAddress = defaults.AdbAddress;
        LoadSettingsIntoControls();
        SaveSettingsFromControls();
        ApplyRecognitionAvailability(showHotKeySuccess: false);
        UpdateAdvice();
        UpdateStatus("软件设置已恢复默认");
    }

    private void MainTabs_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
    {
        ApplyRecognitionAvailability(showHotKeySuccess: false);
        if (IsEquipmentTabActive && _lastInfo != null)
        {
            ShowHeroRecommendations(_lastInfo);
            UpdateAdvice();
        }
    }

    private void ApplyRecognitionAvailability(bool showHotKeySuccess)
    {
        continuousRecognitionTimer.Enabled = IsEquipmentTabActive && chkContinuousRecognition.Checked;
        if (!IsEquipmentTabActive)
        {
            if (_registeredRecognitionHotKey != Keys.None)
            {
                UnregisterHotKey(Handle, RecognitionHotKeyId);
                _registeredRecognitionHotKey = Keys.None;
            }
            return;
        }
        RegisterSelectedRecognitionHotKey(showHotKeySuccess);
    }

    private void HeroDatabase_Changed(object? sender, EventArgs e)
    {
        if (_lastInfo != null)
        {
            ShowHeroRecommendations(_lastInfo);
            UpdateAdvice();
        }
    }

    private async Task UpdateHeroDataAsync()
    {
        if (_heroUpdateCancellation != null)
            return;

        _heroUpdateCancellation = new CancellationTokenSource();
        var token = _heroUpdateCancellation.Token;
        var stagingDirectory = Path.Combine(AppPaths.UserRoot, $".hero-update-{Guid.NewGuid():N}");
        _heroConfigControl.BeginUpdate();
        UpdateStatus("正在更新官方英雄数据...");

        try
        {
            if (Directory.Exists(AppPaths.UserHeroDataDirectory))
                CopyDirectory(AppPaths.UserHeroDataDirectory, stagingDirectory);
            else
                Directory.CreateDirectory(stagingDirectory);

            var progress = new Progress<HeroDataUpdateProgress>(value =>
            {
                _heroConfigControl.ReportUpdate(value);
                UpdateStatus(value.Message);
            });
            using var service = new HeroDataUpdateService();
            var updateResult = await service.WritePackageAsync(stagingDirectory,
                HeroDatabase.Instance.GetBaseDocumentSnapshot(), progress, token);
            token.ThrowIfCancellationRequested();
            AppPaths.ReplaceUserHeroDataDirectory(stagingDirectory);
            HeroDatabase.Instance.Reload();
            _heroConfigControl.RefreshData();
            UpdateStatus(updateResult.Warnings.Count == 0
                ? $"官方英雄数据已更新：{HeroDatabase.Instance.SeasonCode}"
                : $"官方英雄数据已更新：{HeroDatabase.Instance.SeasonCode}，{updateResult.Warnings.Count} 个英雄沿用旧配置或为空");
        }
        catch (OperationCanceledException)
        {
            CleanupStagingDirectory(stagingDirectory);
            UpdateStatus("英雄数据更新已取消，原数据未改变");
        }
        catch (Exception ex)
        {
            CleanupStagingDirectory(stagingDirectory);
            UpdateStatus($"英雄数据更新失败：{ex.Message}");
            WriteDebugLog($"英雄数据更新失败：{ex}");
        }
        finally
        {
            _heroUpdateCancellation.Dispose();
            _heroUpdateCancellation = null;
            _heroConfigControl.EndUpdate();
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void CleanupStagingDirectory(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetFullPath(AppPaths.UserRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
        catch
        {
            // 清理暂存目录失败不覆盖原异常。
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _heroUpdateCancellation?.Cancel();
        HeroDatabase.Instance.Changed -= HeroDatabase_Changed;
        base.OnFormClosing(e);
    }
}
