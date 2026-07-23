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
    private Label _settingsRulesLabel = null!;

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
        _autoEnhanceTab = new AntdUI.TabPage { Text = "自动强化", BackColor = Color.FromArgb(245, 246, 248) };
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
        _autoEnhanceTab.Controls.Add(CreateAutoEnhanceContent());
        settingsTab.Controls.Add(CreateSettingsContent());

        _mainTabs.Pages.Add(_equipmentTab);
        _mainTabs.Pages.Add(_autoEnhanceTab);
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
            Size = new Size(720, 820),
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

        var automationTitle = CreateSettingsHeading(
            "自动强化",
            "设置淘汰装备的处理方式、单次处理上限、最低角色匹配度和赌速度规则。",
            314);
        var automationPanel = new FlowLayoutPanel
        {
            Location = new Point(24, 384),
            Size = new Size(690, 34),
            AutoSize = false,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        var disposalLabel = new Label
        {
            Text = "装备处理方式",
            ForeColor = TextDarkColor,
            Size = new Size(96, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
        };
        _comboAutoDisposalMethod = new AntdUI.Select
        {
            List = true,
            ReadOnly = false,
            Size = new Size(86, 34),
            Radius = 6,
            Margin = new Padding(0, 0, 18, 0),
        };
        _comboAutoDisposalMethod.Items.AddRange(new object[] { "出售", "分解" });
        _comboAutoDisposalMethod.SelectedIndexChanged += (_, _) => SaveSettingsFromControls();

        var maxLabel = new Label
        {
            Text = "最多处理",
            ForeColor = TextDarkColor,
            Size = new Size(65, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
        };
        _numAutoMaxEquipment = new AntdUI.InputNumber
        {
            Size = new Size(76, 34),
            Minimum = 1,
            Maximum = 999,
            Value = 50,
            Radius = 6,
            Margin = Padding.Empty,
        };
        _numAutoMaxEquipment.ValueChanged += (_, _) => SaveSettingsFromControls();
        var maxUnit = new Label
        {
            Text = "件",
            ForeColor = TextDarkColor,
            Size = new Size(32, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 14, 0),
        };
        var matchLabel = new Label
        {
            Text = "最低匹配度",
            ForeColor = TextDarkColor,
            Size = new Size(78, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty,
        };
        _numHeroMatchThreshold = new AntdUI.InputNumber
        {
            Size = new Size(76, 34),
            Minimum = 0,
            Maximum = 100,
            Value = 70,
            Radius = 6,
            Margin = Padding.Empty,
        };
        _numHeroMatchThreshold.ValueChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };
        var matchUnit = new Label
        {
            Text = "%",
            ForeColor = TextDarkColor,
            Size = new Size(26, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 0, 0),
        };
        automationPanel.Controls.AddRange(new Control[]
        {
            disposalLabel, _comboAutoDisposalMethod, maxLabel, _numAutoMaxEquipment,
            maxUnit, matchLabel, _numHeroMatchThreshold, matchUnit,
        });

        _chkHeroicOnlyGambleSpeed = new AntdUI.Checkbox
        {
            Text = "紫装只赌速度（忽略分数和匹配度，速度不达标立即处理）",
            Checked = false,
            Location = new Point(24, 430),
            Size = new Size(470, 34),
        };
        _chkHeroicOnlyGambleSpeed.CheckedChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };

        _chkAutoStopOnValuableEquipment = new AntdUI.Checkbox
        {
            Text = "遇到符合保留条件的装备后停止（关闭后将返回背包并继续下一件）",
            Checked = true,
            Location = new Point(24, 466),
            Size = new Size(520, 34),
        };
        _chkAutoStopOnValuableEquipment.CheckedChanged += (_, _) => SaveSettingsFromControls();

        var rulesTitle = CreateSettingsHeading(
            "自动规则说明",
            "推荐匹配与角色默认配置会自动应用以下规则。",
            514);
        var rulesPanel = new Panel
        {
            BackColor = Color.FromArgb(247, 249, 252),
            Location = new Point(24, 574),
            Size = new Size(690, 150),
            Padding = new Padding(12, 9, 12, 9),
        };
        _settingsRulesLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9.2F),
            ForeColor = Color.FromArgb(66, 70, 77),
            Text = "• 红装赌速度：比紫装多一次强化机会，允许累计歪一跳。\r\n"
                   + "• 紫装只赌速度：开启后忽略分数与匹配度，按严格速度阶梯处理。\r\n"
                   + "• 速度硬门槛：角色需要速度时，装备必须带速度（速度鞋主属性也算），否则不推荐。\r\n"
                   + "• 速度鞋默认：采集数据包含速度时，鞋子主属性默认只勾选速度。\r\n"
                   + "• 双爆项链默认：角色同时需要暴击率和暴击伤害时，项链默认勾选双爆。\r\n"
                   + "• 速度套补全：主流搭配包含速度套时，自动把速度加入角色有效属性。",
        };
        rulesPanel.Controls.Add(_settingsRulesLabel);

        var reset = new AntdUI.Button
        {
            Text = "恢复默认设置",
            Location = new Point(24, 750),
            Size = new Size(120, 34),
            Radius = 6,
        };
        reset.Click += (_, _) => ResetSettings();

        card.Controls.Add(reset);
        card.Controls.Add(rulesPanel);
        card.Controls.Add(rulesTitle);
        card.Controls.Add(_chkAutoStopOnValuableEquipment);
        card.Controls.Add(_chkHeroicOnlyGambleSpeed);
        card.Controls.Add(automationPanel);
        card.Controls.Add(automationTitle);
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
            _numAutoMaxEquipment.Value = _settings.AutoEnhanceMaxEquipment;
            _comboAutoDisposalMethod.SelectedValue = _settings.AutoEnhanceDisposalMethod;
            _numHeroMatchThreshold.Value = _settings.MinimumHeroMatchScore;
            _chkAutoStopOnValuableEquipment.Checked = _settings.AutoEnhanceStopOnValuableEquipment;
            _chkHeroicOnlyGambleSpeed.Checked = _settings.HeroicOnlyGambleSpeed;
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
        _settings.AutoEnhanceMaxEquipment = (int)_numAutoMaxEquipment.Value;
        _settings.AutoEnhanceDisposalMethod = _comboAutoDisposalMethod.SelectedValue as string
            ?? _comboAutoDisposalMethod.Text;
        _settings.MinimumHeroMatchScore = _numHeroMatchThreshold.Value;
        _settings.AutoEnhanceStopOnValuableEquipment = _chkAutoStopOnValuableEquipment.Checked;
        _settings.HeroicOnlyGambleSpeed = _chkHeroicOnlyGambleSpeed.Checked;
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
        _settings.AutoEnhanceMaxEquipment = defaults.AutoEnhanceMaxEquipment;
        _settings.AutoEnhanceDisposalMethod = defaults.AutoEnhanceDisposalMethod;
        _settings.MinimumHeroMatchScore = defaults.MinimumHeroMatchScore;
        _settings.AutoEnhanceStopOnValuableEquipment = defaults.AutoEnhanceStopOnValuableEquipment;
        _settings.HeroicOnlyGambleSpeed = defaults.HeroicOnlyGambleSpeed;
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
        continuousRecognitionTimer.Enabled = IsEquipmentTabActive
                                             && !IsAutoEnhancing
                                             && chkContinuousRecognition.Checked;
        if (!IsEquipmentTabActive || IsAutoEnhancing)
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
                ? $"官方英雄数据已更新：{HeroDatabase.Instance.SeasonCode}，自定义配置已保留"
                : $"官方英雄数据已更新：{HeroDatabase.Instance.SeasonCode}，自定义配置已保留；{updateResult.Warnings.Count} 个英雄沿用旧配置或为空");
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
        _autoEnhanceCancellation?.Cancel();
        _heroUpdateCancellation?.Cancel();
        HeroDatabase.Instance.Changed -= HeroDatabase_Changed;
        base.OnFormClosing(e);
    }
}
