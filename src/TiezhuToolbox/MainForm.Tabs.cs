using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox;

public partial class MainForm
{
    private readonly AppSettings _settings = AppSettingsStore.Load();
    private readonly HashSet<string> _disabledDemandProfiles = new(StringComparer.Ordinal);
    private AntdUI.Tabs _mainTabs = null!;
    private AntdUI.TabPage _equipmentTab = null!;
    private DemandBrowserControl _demandBrowserControl = null!;
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
        NormalizeDesignerControlForRuntime(thresholdPanel);
        NormalizeDesignerControlForRuntime(recognitionSettingsPanel);
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
        var demandTab = new AntdUI.TabPage { Text = "需求分析", BackColor = Color.White };
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

        _disabledDemandProfiles.UnionWith(_settings.DisabledDemandProfiles);
        _demandBrowserControl = new DemandBrowserControl(
            profileKey => !_disabledDemandProfiles.Contains(profileKey),
            SetDemandProfileEnabled);
        demandTab.Controls.Add(_demandBrowserControl);
        _demandBrowserControl.ApplyInitialDpiScale(_layoutDpi);

        var autoEnhanceContent = CreateAutoEnhanceContent();
        _autoEnhanceTab.Controls.Add(autoEnhanceContent);
        ScaleRuntimePage(autoEnhanceContent);

        var settingsContent = CreateSettingsContent();
        settingsTab.Controls.Add(settingsContent);
        ScaleRuntimePage(settingsContent);

        _mainTabs.Pages.Add(_equipmentTab);
        _mainTabs.Pages.Add(_autoEnhanceTab);
        _mainTabs.Pages.Add(demandTab);
        _mainTabs.Pages.Add(settingsTab);
        _mainTabs.SelectedIndex = 0;
        _mainTabs.SelectedIndexChanged += MainTabs_SelectedIndexChanged;
        Controls.Add(_mainTabs);
        Controls.SetChildIndex(_mainTabs, 0);
        Controls.SetChildIndex(statusStrip, 1);

        LoadSettingsIntoControls();
        txtAddress.Leave += (_, _) => SaveSettingsFromControls();
        ResumeLayout(performLayout: true);
        LayoutTopToolbar();
    }

    private void LayoutTopToolbar()
    {
        var margin = ScalePixel(12);
        var gap = ScalePixel(8);
        var right = topPanel.ClientSize.Width - margin;
        PlaceFromRight(btnCaptureRecognize, ScalePixel(112), ref right, gap);
        PlaceFromRight(btnToggleShot, ScalePixel(92), ref right, gap);
        PlaceFromRight(btnOpenFolder, ScalePixel(76), ref right, gap);
        PlaceFromRight(btnRefresh, ScalePixel(76), ref right, gap);
        PlaceFromRight(btnConnect, ScalePixel(76), ref right, gap);
        PlaceFromRight(txtAddress, ScalePixel(210), ref right, gap);
        comboDevices.Location = new Point(margin, ScalePixel(15));
        comboDevices.Size = new Size(Math.Max(ScalePixel(180), right - margin), ScalePixel(34));
    }

    private void PlaceFromRight(Control control, int width, ref int right, int gap)
    {
        right -= width;
        control.Location = new Point(right, ScalePixel(15));
        control.Size = new Size(width, ScalePixel(34));
        right -= gap;
    }

    private Control CreateSettingsContent()
    {
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };
        var card = new Panel
        {
            BackColor = Color.White,
            Location = new Point(24, 24),
            Size = new Size(720, 950),
            Padding = new Padding(24),
        };
        host.Resize += (_, _) => card.Width = Math.Min(
            ScalePixel(760),
            Math.Max(ScalePixel(560), host.ClientSize.Width - ScalePixel(48)));

        var title = new Label
        {
            Text = "软件设置",
            Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold),
            Location = new Point(24, 18),
            Size = new Size(650, 48),
        };
        var scoreTitle = CreateSettingsHeading("强化分数", "85级按左/右三件阈值每跳 +6；88级使用独立阈值，每跳 +7。", 70);
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
            ConfigureSettingsRowLabel(label);

        var recognitionTitle = CreateSettingsHeading("识别控制", "全局快捷键和持续识别只在“装备强化”页生效。", 188);
        recognitionSettingsPanel.Dock = DockStyle.None;
        recognitionSettingsPanel.Location = new Point(24, 258);
        recognitionSettingsPanel.Size = new Size(620, 34);
        recognitionSettingsPanel.AutoSize = false;
        recognitionSettingsPanel.Margin = Padding.Empty;
        comboRecognitionHotKey.Size = new Size(76, 34);
        chkContinuousRecognition.Size = new Size(108, 34);
        numRecognitionInterval.Size = new Size(88, 34);
        foreach (var label in new[]
                 { lblRecognitionGroup, lblRecognitionHotKey, lblRecognitionInterval, lblIntervalUnit })
            ConfigureSettingsRowLabel(label);

        var automationTitle = CreateSettingsHeading(
            "自动强化",
            "设置淘汰装备的处理方式、单次处理上限、最低需求匹配度和赌速度规则。",
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
            Text = "最低需求匹配度",
            ForeColor = TextDarkColor,
            Size = new Size(106, 34),
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

        _chkSpeedSetRequiresSpeed = new AntdUI.Checkbox
        {
            Text = "速度套只强化带速度的装备（鞋子看主属性，其他部位看副属性）",
            Checked = true,
            Location = new Point(24, 466),
            Size = new Size(520, 34),
        };
        _chkSpeedSetRequiresSpeed.CheckedChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };

        _chkCriticalNecklaceMainStatRule = new AntdUI.Checkbox
        {
            Text = "暴击/暴伤高权重子类的项链只强化对应主属性",
            Checked = true,
            Location = new Point(24, 502),
            Size = new Size(520, 34),
        };
        _chkCriticalNecklaceMainStatRule.CheckedChanged += (_, _) =>
        {
            SaveSettingsFromControls();
            UpdateAdvice();
        };

        _chkAutoStopOnValuableEquipment = new AntdUI.Checkbox
        {
            Text = "遇到符合保留条件的装备后停止（关闭后将返回背包并继续下一件）",
            Checked = true,
            Location = new Point(24, 538),
            Size = new Size(520, 34),
        };
        _chkAutoStopOnValuableEquipment.CheckedChanged += (_, _) => SaveSettingsFromControls();

        var rulesTitle = CreateSettingsHeading(
            "自动规则说明",
            "推荐匹配与套装需求数据会自动应用以下规则。",
            586);
        var rulesPanel = new Panel
        {
            BackColor = Color.FromArgb(247, 249, 252),
            Location = new Point(24, 646),
            Size = new Size(690, 194),
            Padding = new Padding(12, 9, 12, 9),
        };
        _settingsRulesLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9.2F),
            ForeColor = Color.FromArgb(66, 70, 77),
            Text = "• 红装赌速度：比紫装多一次强化机会，允许累计歪一跳。\r\n"
                   + "• 紫装只赌速度：鞋子除外；开启后忽略分数与匹配度，按严格速度阶梯处理。\r\n"
                   + "• 速度套速度规则：鞋子必须为速度主属性，其他部位必须含速度副属性。\r\n"
                   + "• 暴击项链规则：暴击率或暴伤达到高权重时，项链只接受对应的主属性。\r\n"
                   + "• 套装子类：只匹配当前套装下人工维护的属性组合，不使用旧角色算法回退。\r\n"
                   + "• 右三主属性：85级按90级满值预估，88/90使用同一满值档参与用途匹配。\r\n"
                   + "• 强化分数：始终只统计副属性；主属性不会加入分数阶梯或重铸分数。\r\n"
                   + "• 固定主属性：右三固定攻击、生命、防御不匹配任何需求子类。",
        };
        rulesPanel.Controls.Add(_settingsRulesLabel);

        var reset = new AntdUI.Button
        {
            Text = "恢复默认设置",
            Location = new Point(24, 866),
            Size = new Size(120, 34),
            Radius = 6,
        };
        reset.Click += (_, _) => ResetSettings();

        card.Controls.Add(reset);
        card.Controls.Add(rulesPanel);
        card.Controls.Add(rulesTitle);
        card.Controls.Add(_chkAutoStopOnValuableEquipment);
        card.Controls.Add(_chkCriticalNecklaceMainStatRule);
        card.Controls.Add(_chkSpeedSetRequiresSpeed);
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

    /// <summary>
    /// 设置行来自设计器页面，高 DPI 下字体可能先于运行时页面完成缩放。
    /// 标签宽度交给首选尺寸计算，避免固定像素宽度只显示部分文字。
    /// </summary>
    private static void ConfigureSettingsRowLabel(Label label)
    {
        label.AutoSize = true;
        label.MinimumSize = new Size(0, 34);
        label.TextAlign = ContentAlignment.MiddleLeft;
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
            _numHeroMatchThreshold.Value = _settings.MinimumDemandMatchScore;
            _chkAutoStopOnValuableEquipment.Checked = _settings.AutoEnhanceStopOnValuableEquipment;
            _chkHeroicOnlyGambleSpeed.Checked = _settings.HeroicOnlyGambleSpeed;
            _chkSpeedSetRequiresSpeed.Checked = _settings.SpeedSetRequiresSpeed;
            _chkCriticalNecklaceMainStatRule.Checked = _settings.CriticalNecklaceMainStatRule;
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
        _settings.MinimumDemandMatchScore = _numHeroMatchThreshold.Value;
        _settings.AutoEnhanceStopOnValuableEquipment = _chkAutoStopOnValuableEquipment.Checked;
        _settings.HeroicOnlyGambleSpeed = _chkHeroicOnlyGambleSpeed.Checked;
        _settings.SpeedSetRequiresSpeed = _chkSpeedSetRequiresSpeed.Checked;
        _settings.CriticalNecklaceMainStatRule = _chkCriticalNecklaceMainStatRule.Checked;
        _settings.DisabledDemandProfiles = _disabledDemandProfiles
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
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
        _settings.MinimumDemandMatchScore = defaults.MinimumDemandMatchScore;
        _settings.AutoEnhanceStopOnValuableEquipment = defaults.AutoEnhanceStopOnValuableEquipment;
        _settings.HeroicOnlyGambleSpeed = defaults.HeroicOnlyGambleSpeed;
        _settings.SpeedSetRequiresSpeed = defaults.SpeedSetRequiresSpeed;
        _settings.CriticalNecklaceMainStatRule = defaults.CriticalNecklaceMainStatRule;
        _disabledDemandProfiles.Clear();
        _settings.DisabledDemandProfiles.Clear();
        LoadSettingsIntoControls();
        _demandBrowserControl.RefreshProfiles();
        SaveSettingsFromControls();
        ApplyRecognitionAvailability(showHotKeySuccess: false);
        UpdateAdvice();
        UpdateStatus("软件设置已恢复默认");
    }

    private void SetDemandProfileEnabled(string profileKey, bool enabled)
    {
        if (enabled)
            _disabledDemandProfiles.Remove(profileKey);
        else
            _disabledDemandProfiles.Add(profileKey);

        SaveSettingsFromControls();
        if (_lastInfo != null)
        {
            ShowDemandRecommendations(_lastInfo);
            UpdateAdvice();
        }
    }

    private void MainTabs_SelectedIndexChanged(object sender, AntdUI.IntEventArgs e)
    {
        ApplyRecognitionAvailability(showHotKeySuccess: false);
        if (IsEquipmentTabActive && _lastInfo != null)
        {
            ShowDemandRecommendations(_lastInfo);
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

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _autoEnhanceCancellation?.Cancel();
        base.OnFormClosing(e);
    }
}
