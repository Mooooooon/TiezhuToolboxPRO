using TiezhuToolbox.Modules.Ocr;
using TiezhuToolbox.Modules.Recommend;
using TiezhuToolbox.Modules.Automation;
using System.Windows.Forms;

if (args.Contains("--config-smoke"))
{
    var testRoot = Path.Combine(Path.GetTempPath(), "TiezhuToolbox-config-test-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable("TIEZHU_TOOLBOX_USER_ROOT", testRoot);
    try
    {
        var persistedProfileKey = DemandDatabase.Instance.Sets
            .SelectMany(set => set.Profiles.Select(profile =>
                SetProfileMatcher.CreateProfileKey(set.Code, profile.Id)))
            .First();
        Exception? settingsError = null;
        var settingsThread = new Thread(() =>
        {
            try
            {
                using (var firstForm = new TiezhuToolbox.MainForm())
                {
                    var threshold = typeof(TiezhuToolbox.MainForm).GetField("numLeftThreshold",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    threshold.GetType().GetProperty("Value")!.SetValue(threshold, 31M);
                    var level88Threshold = typeof(TiezhuToolbox.MainForm).GetField("numLevel88Threshold",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    var defaultLevel88Value = (decimal)level88Threshold.GetType().GetProperty("Value")!.GetValue(level88Threshold)!;
                    if (defaultLevel88Value != 28M)
                        throw new InvalidOperationException($"88级默认阈值错误：{defaultLevel88Value}");
                    level88Threshold.GetType().GetProperty("Value")!.SetValue(level88Threshold, 33M);
                    var address = (Control)typeof(TiezhuToolbox.MainForm).GetField("txtAddress",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    address.Text = "127.0.0.1:5555";
                    var maxAutoEquipment = typeof(TiezhuToolbox.MainForm).GetField("_numAutoMaxEquipment",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    maxAutoEquipment.GetType().GetProperty("Value")!.SetValue(maxAutoEquipment, 17M);
                    var disposalMethod = typeof(TiezhuToolbox.MainForm).GetField("_comboAutoDisposalMethod",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    disposalMethod.GetType().GetProperty("SelectedValue")!.SetValue(disposalMethod, "分解");
                    var matchThreshold = typeof(TiezhuToolbox.MainForm).GetField("_numHeroMatchThreshold",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    matchThreshold.GetType().GetProperty("Value")!.SetValue(matchThreshold, 82M);
                    var stopOnValuable = typeof(TiezhuToolbox.MainForm).GetField("_chkAutoStopOnValuableEquipment",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    stopOnValuable.GetType().GetProperty("Checked")!.SetValue(stopOnValuable, false);
                    var heroicOnlyGambleSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkHeroicOnlyGambleSpeed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    heroicOnlyGambleSpeed.GetType().GetProperty("Checked")!.SetValue(heroicOnlyGambleSpeed, true);
                    var speedSetRequiresSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkSpeedSetRequiresSpeed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    var criticalNecklaceMainStatRule = typeof(TiezhuToolbox.MainForm).GetField("_chkCriticalNecklaceMainStatRule",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    if (!(bool)speedSetRequiresSpeed.GetType().GetProperty("Checked")!.GetValue(speedSetRequiresSpeed)!
                        || !(bool)criticalNecklaceMainStatRule.GetType().GetProperty("Checked")!.GetValue(criticalNecklaceMainStatRule)!)
                        throw new InvalidOperationException("两项特殊强化规则没有默认开启");
                    speedSetRequiresSpeed.GetType().GetProperty("Checked")!.SetValue(speedSetRequiresSpeed, false);
                    criticalNecklaceMainStatRule.GetType().GetProperty("Checked")!.SetValue(criticalNecklaceMainStatRule, false);
                    typeof(TiezhuToolbox.MainForm).GetMethod("SetDemandProfileEnabled",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(firstForm, new object[] { persistedProfileKey, false });
                    typeof(TiezhuToolbox.MainForm).GetMethod("SaveSettingsFromControls",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(firstForm, null);
                }
                using var secondForm = new TiezhuToolbox.MainForm();
                var loadedThreshold = typeof(TiezhuToolbox.MainForm).GetField("numLeftThreshold",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var value = (decimal)loadedThreshold.GetType().GetProperty("Value")!.GetValue(loadedThreshold)!;
                var loadedLevel88Threshold = typeof(TiezhuToolbox.MainForm).GetField("numLevel88Threshold",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var level88Value = (decimal)loadedLevel88Threshold.GetType().GetProperty("Value")!.GetValue(loadedLevel88Threshold)!;
                var loadedAddress = (Control)typeof(TiezhuToolbox.MainForm).GetField("txtAddress",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var loadedMaxAutoEquipment = typeof(TiezhuToolbox.MainForm).GetField("_numAutoMaxEquipment",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var maxAutoValue = (decimal)loadedMaxAutoEquipment.GetType().GetProperty("Value")!.GetValue(loadedMaxAutoEquipment)!;
                var loadedDisposalMethod = typeof(TiezhuToolbox.MainForm).GetField("_comboAutoDisposalMethod",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var disposalValue = loadedDisposalMethod.GetType().GetProperty("SelectedValue")!.GetValue(loadedDisposalMethod) as string;
                var loadedMatchThreshold = typeof(TiezhuToolbox.MainForm).GetField("_numHeroMatchThreshold",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var matchValue = (decimal)loadedMatchThreshold.GetType().GetProperty("Value")!.GetValue(loadedMatchThreshold)!;
                var loadedStopOnValuable = typeof(TiezhuToolbox.MainForm).GetField("_chkAutoStopOnValuableEquipment",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var stopOnValuableValue = (bool)loadedStopOnValuable.GetType().GetProperty("Checked")!.GetValue(loadedStopOnValuable)!;
                var loadedHeroicOnlyGambleSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkHeroicOnlyGambleSpeed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var heroicOnlyGambleSpeedValue = (bool)loadedHeroicOnlyGambleSpeed.GetType().GetProperty("Checked")!
                    .GetValue(loadedHeroicOnlyGambleSpeed)!;
                var loadedSpeedSetRequiresSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkSpeedSetRequiresSpeed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var speedSetRequiresSpeedValue = (bool)loadedSpeedSetRequiresSpeed.GetType().GetProperty("Checked")!
                    .GetValue(loadedSpeedSetRequiresSpeed)!;
                var loadedCriticalNecklaceMainStatRule = typeof(TiezhuToolbox.MainForm).GetField("_chkCriticalNecklaceMainStatRule",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var criticalNecklaceMainStatRuleValue = (bool)loadedCriticalNecklaceMainStatRule.GetType().GetProperty("Checked")!
                    .GetValue(loadedCriticalNecklaceMainStatRule)!;
                var loadedDisabledProfiles = (IReadOnlySet<string>)typeof(TiezhuToolbox.MainForm)
                    .GetField("_disabledDemandProfiles",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(secondForm)!;
                if (value != 31M || level88Value != 33M || maxAutoValue != 17M
                    || disposalValue != "分解" || matchValue != 82M || stopOnValuableValue
                    || !heroicOnlyGambleSpeedValue
                    || speedSetRequiresSpeedValue || criticalNecklaceMainStatRuleValue
                    || !loadedDisabledProfiles.Contains(persistedProfileKey)
                    || loadedAddress.Text != "127.0.0.1:5555")
                    throw new InvalidOperationException("软件设置重载结果不一致");
            }
            catch (Exception ex)
            {
                settingsError = ex;
            }
        });
        settingsThread.SetApartmentState(ApartmentState.STA);
        settingsThread.Start();
        settingsThread.Join();
        if (settingsError != null)
            throw new InvalidOperationException("软件设置持久化测试失败", settingsError);
        Console.WriteLine("配置持久化测试通过：强化规则与停用需求子类均可保存并恢复");
    }
    finally
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, recursive: true);
    }
    return;
}

if (args.Contains("--automation-smoke"))
{
    var imagePaths = args.Where(arg => arg != "--automation-smoke").ToArray();
    if (imagePaths.Length != 7 || imagePaths.Any(path => !File.Exists(path)))
        throw new ArgumentException("--automation-smoke 后需依次提供背包、强化、等级弹窗、已登记材料、出售确认、分解确认、经验溢出奖励截图");

    using var matcher = new AutomationScreenMatcher();
    using var backpack = new Bitmap(imagePaths[0]);
    using var enhance = new Bitmap(imagePaths[1]);
    using var popup = new Bitmap(imagePaths[2]);
    using var registered = new Bitmap(imagePaths[3]);
    using var sellConfirmation = new Bitmap(imagePaths[4]);
    using var extractConfirmation = new Bitmap(imagePaths[5]);
    using var rewardPopup = new Bitmap(imagePaths[6]);

    void AssertScreen(Bitmap image, AutomationGameScreen expected)
    {
        var actual = matcher.DetectScreen(image, out var confidence);
        Console.WriteLine($"  界面：期望 {expected}，实际 {actual}，置信度 {confidence:P1}");
        if (actual != expected)
            throw new InvalidOperationException($"自动强化界面识别失败：期望 {expected}，实际 {actual}（{confidence:P1}）");
    }

    AssertScreen(backpack, AutomationGameScreen.Backpack);
    AssertScreen(enhance, AutomationGameScreen.EnhanceEquipment);
    AssertScreen(popup, AutomationGameScreen.AutoRegisterPopup);
    AssertScreen(registered, AutomationGameScreen.EnhanceEquipment);
    AssertScreen(sellConfirmation, AutomationGameScreen.SellConfirmation);
    AssertScreen(extractConfirmation, AutomationGameScreen.ExtractConfirmation);
    AssertScreen(rewardPopup, AutomationGameScreen.EnhancementRewardPopup);

    var expectedButtons = new[]
    {
        (backpack, AutomationTemplate.BackpackEnhance),
        (enhance, AutomationTemplate.AutoRegister),
        (enhance, AutomationTemplate.Sell),
        (enhance, AutomationTemplate.Extract),
        (popup, AutomationTemplate.Target3),
        (popup, AutomationTemplate.Target6),
        (popup, AutomationTemplate.Target9),
        (popup, AutomationTemplate.Target12),
        (popup, AutomationTemplate.Target15),
        (registered, AutomationTemplate.ReadyEnhance),
        (sellConfirmation, AutomationTemplate.SellConfirmButton),
        (extractConfirmation, AutomationTemplate.ExtractConfirmButton),
        (rewardPopup, AutomationTemplate.RewardClose),
    };
    foreach (var (image, template) in expectedButtons)
    {
        var match = matcher.Find(image, template);
        Console.WriteLine($"  按钮：{template} {match.Confidence:P1} @ {match.Center}");
        if (!match.IsMatch())
            throw new InvalidOperationException($"自动强化按钮识别失败：{template}（{match.Confidence:P1}）");
    }

    var targetRows = new[]
    {
        AutomationTemplate.Target15,
        AutomationTemplate.Target12,
        AutomationTemplate.Target9,
        AutomationTemplate.Target6,
        AutomationTemplate.Target3,
    }.Select(template => matcher.Find(popup, template).Center.Y).ToArray();
    if (!targetRows.Zip(targetRows.Skip(1), (upper, lower) => lower - upper)
            .All(gap => gap is >= 55 and <= 90))
    {
        throw new InvalidOperationException(
            $"强化等级按钮行定位异常：{string.Join("/", targetRows)}");
    }

    if (matcher.HasRegisteredMaterials(enhance) || matcher.HasRegisteredMaterials(popup))
        throw new InvalidOperationException("空材料槽被误判为已登记材料");
    if (!matcher.HasRegisteredMaterials(registered))
        throw new InvalidOperationException("已登记的强化材料未被识别");

    var targets = new[] { 0, 3, 6, 9, 12 }.Select(level => AutomationScreenMatcher.NextTargetLevel(level)).ToArray();
    if (!targets.SequenceEqual(new int?[] { 3, 6, 9, 12, 15 })
        || AutomationScreenMatcher.NextTargetLevel(15) != null)
        throw new InvalidOperationException("下一强化档位计算错误");

    using var resized = new Bitmap(1280, 720);
    using (var graphics = Graphics.FromImage(resized))
        graphics.DrawImage(backpack, new Rectangle(0, 0, resized.Width, resized.Height));
    AssertScreen(resized, AutomationGameScreen.Backpack);

    var automationTemplateDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TiezhuToolbox", "Assets", "Templates"));
    using (var ocr = new OcrEngine(automationTemplateDir))
    {
        var info = await ocr.RecognizeAsync(imagePaths[1]);
        var advice = EnhancementAdvisor.Analyze(info, 24, 24, 28);
        Console.WriteLine($"  OCR：{info.Level}级 {info.Quality}，+{info.EnhanceLevel}，{info.Score:0.##}分，建议={advice.Advice}");
        if (info.Level != 85 || info.Quality != "英雄鞋子" || info.EnhanceLevel != 0
            || advice.Advice != EnhanceAdvice.GiveUp)
            throw new InvalidOperationException("自动强化样例的 OCR 或强化建议结果不符合预期");
    }

    Console.WriteLine("自动强化测试通过：7 个界面、13 个按钮、材料槽、分辨率缩放、OCR 与强化建议均正常");
    return;
}

if (args.Contains("--ui-smoke"))
{
    var uiTestRoot = Path.Combine(Path.GetTempPath(), "TiezhuToolbox-ui-test-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable("TIEZHU_TOOLBOX_USER_ROOT", uiTestRoot);
    Exception? uiError = null;
    var thread = new Thread(() =>
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            using var form = new TiezhuToolbox.MainForm();
            form.Show();
            Application.DoEvents();
            var dpiScale = form.DeviceDpi / 96D;
            int DpiPixel(int logicalPixel) => (int)Math.Round(logicalPixel * dpiScale);
            if (form.AutoScaleMode != AutoScaleMode.Dpi)
                throw new InvalidOperationException($"主窗体未启用 DPI 缩放：{form.AutoScaleMode}");

            var tabsField = typeof(TiezhuToolbox.MainForm).GetField("_mainTabs",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("未找到主页签");
            var tabs = tabsField.GetValue(form) ?? throw new InvalidOperationException("主页签未初始化");
            var pages = tabs.GetType().GetProperty("Pages")?.GetValue(tabs) as System.Collections.ICollection;
            if (pages?.Count != 4)
                throw new InvalidOperationException($"页签数量错误：{pages?.Count}");

            var selectedIndex = tabs.GetType().GetProperty("SelectedIndex")!;
            void CaptureTab(string name)
            {
                var directory = Environment.GetEnvironmentVariable("TIEZHU_UI_CAPTURE_DIR");
                if (string.IsNullOrWhiteSpace(directory))
                    return;
                Directory.CreateDirectory(directory);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save(Path.Combine(directory, name + ".png"));
            }
            var deviceSelect = typeof(TiezhuToolbox.MainForm).GetField("comboDevices",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var deviceReadOnly = (bool)deviceSelect.GetType().GetProperty("ReadOnly")!.GetValue(deviceSelect)!;
            if (deviceReadOnly)
                throw new InvalidOperationException("设备下拉框被完全禁用，无法展开选择");
            var deviceListMode = (bool)deviceSelect.GetType().GetProperty("List")!.GetValue(deviceSelect)!;
            if (!deviceListMode)
                throw new InvalidOperationException("设备下拉框仍允许文字输入");
            var expandDrop = deviceSelect.GetType().GetProperty("ExpandDrop")!;
            expandDrop.SetValue(deviceSelect, true);
            Application.DoEvents();
            if (!(bool)expandDrop.GetValue(deviceSelect)!)
                throw new InvalidOperationException("设备下拉框无法展开");
            expandDrop.SetValue(deviceSelect, false);
            Application.DoEvents();
            var addressInput = (Control)typeof(TiezhuToolbox.MainForm).GetField("txtAddress",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (addressInput.Width < DpiPixel(200))
                throw new InvalidOperationException($"ADB 地址输入框宽度不足：{addressInput.Width}");
            var showDemand = typeof(TiezhuToolbox.MainForm).GetMethod("ShowDemandRecommendations",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("找不到套装需求展示方法");
            showDemand.Invoke(form,
            [
                new EquipmentInfo
                {
                    Level = 88,
                    Quality = "传说鞋子",
                    SetName = "速度套装",
                    MainStatName = "速度",
                    MainStatValue = "1",
                    SubStats =
                    {
                        new SubStat { Name = "生命值", Value = "8%" },
                        new SubStat { Name = "防御力", Value = "8%" },
                        new SubStat { Name = "效果命中", Value = "8%" },
                        new SubStat { Name = "效果抗性", Value = "8%" },
                    },
                },
            ]);
            Application.DoEvents();
            var demandResults = (FlowLayoutPanel)typeof(TiezhuToolbox.MainForm).GetField("flowHeroes",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (demandResults.Controls.Count == 0 || demandResults.Controls.Count > 5)
                throw new InvalidOperationException($"装备页需求子类卡片数量错误：{demandResults.Controls.Count}");
            var firstCard = demandResults.Controls[0];
            var collapsedHeight = firstCard.Height;
            var header = firstCard.Controls.Cast<Control>().OfType<Panel>()
                .First(panel => panel.Cursor == Cursors.Hand);
            typeof(Control).GetMethod("OnClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(header, new object[] { EventArgs.Empty });
            Application.DoEvents();
            if (firstCard.Height <= collapsedHeight)
                throw new InvalidOperationException("装备页需求子类卡片无法展开英雄配装");
            CaptureTab("equipment");
            var timer = (System.Windows.Forms.Timer)(typeof(TiezhuToolbox.MainForm)
                .GetField("continuousRecognitionTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(form) ?? throw new InvalidOperationException("持续识别计时器未初始化"));
            timer.Interval = 60000;
            var loadingField = typeof(TiezhuToolbox.MainForm).GetField("_isLoadingSettings",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var continuousCheck = typeof(TiezhuToolbox.MainForm).GetField("chkContinuousRecognition",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            loadingField.SetValue(form, true);
            continuousCheck.GetType().GetProperty("Checked")!.SetValue(continuousCheck, true);
            loadingField.SetValue(form, false);
            typeof(TiezhuToolbox.MainForm).GetMethod("ApplyRecognitionAvailability",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form, new object[] { false });
            if (!timer.Enabled)
                throw new InvalidOperationException("装备页未恢复持续识别");

            selectedIndex.SetValue(tabs, 1);
            Application.DoEvents();
            CaptureTab("auto-enhance");
            var autoStart = (Control)typeof(TiezhuToolbox.MainForm).GetField("_btnAutoStart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var autoLog = (RichTextBox)typeof(TiezhuToolbox.MainForm).GetField("_autoLog",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (!autoStart.Enabled || !autoLog.ReadOnly || timer.Enabled)
                throw new InvalidOperationException("自动强化页初始状态不正确");
            if (autoLog.Right < autoLog.Parent!.ClientSize.Width - autoLog.Parent.Padding.Right - 2)
                throw new InvalidOperationException(
                    $"自动强化日志未填满内容区：日志={autoLog.Bounds}，父容器={autoLog.Parent.ClientSize}");

            selectedIndex.SetValue(tabs, 2);
            Application.DoEvents();
            CaptureTab("demand-analysis");
            var demandBrowser = typeof(TiezhuToolbox.MainForm).GetField("_demandBrowserControl",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var setList = (ListBox)demandBrowser.GetType().GetField("_setList",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(demandBrowser)!;
            var profilesPanel = (FlowLayoutPanel)demandBrowser.GetType().GetField("_profiles",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(demandBrowser)!;
            if (setList.Items.Count != 23)
                throw new InvalidOperationException($"需求分析套装数量错误：{setList.Items.Count}");
            var populatedIndex = Enumerable.Range(0, setList.Items.Count)
                .First(index => ((DemandSet)setList.Items[index]!).Profiles.Count > 0);
            setList.SelectedIndex = populatedIndex;
            Application.DoEvents();
            if (profilesPanel.Controls.Count == 0)
                throw new InvalidOperationException("需求分析页未显示属性子类");
            var profileCards = profilesPanel.Controls.Cast<Control>()
                .Where(control => Equals(control.Tag, "profile-card"))
                .ToList();
            var profileSwitches = profileCards
                .SelectMany(card => card.Controls.Cast<Control>())
                .OfType<Panel>()
                .SelectMany(header => header.Controls.Cast<Control>())
                .OfType<AntdUI.Switch>()
                .ToList();
            if (profileSwitches.Count != ((DemandSet)setList.SelectedItem!).Profiles.Count
                || profileSwitches.Any(profileSwitch => !profileSwitch.Checked))
                throw new InvalidOperationException("需求子类参与匹配开关数量或默认状态错误");
            var analysisCard = profileCards[0];
            var analysisCollapsedHeight = analysisCard.Height;
            var analysisHeader = analysisCard.Controls.Cast<Control>().OfType<Panel>()
                .First(panel => panel.Cursor == Cursors.Hand);
            var analysisBuilds = analysisCard.Controls.Cast<Control>().OfType<Panel>()
                .First(panel => panel != analysisHeader);
            if (analysisBuilds.Visible)
                throw new InvalidOperationException("需求分析页角色列表没有默认折叠");
            typeof(Control).GetMethod("OnClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(analysisHeader, new object[] { EventArgs.Empty });
            Application.DoEvents();
            if (!analysisBuilds.Visible || analysisCard.Height <= analysisCollapsedHeight)
                throw new InvalidOperationException("需求分析页角色列表无法展开");
            CaptureTab("demand-analysis-expanded");
            var firstProfileSwitch = profileSwitches[0];
            firstProfileSwitch.Checked = false;
            Application.DoEvents();
            var disabledProfiles = (IReadOnlySet<string>)typeof(TiezhuToolbox.MainForm)
                .GetField("_disabledDemandProfiles",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(form)!;
            if (firstProfileSwitch.Tag is not string disabledKey
                || !disabledProfiles.Contains(disabledKey))
                throw new InvalidOperationException("需求子类开关没有更新匹配过滤配置");
            if (timer.Enabled)
                throw new InvalidOperationException("离开装备页后持续识别仍在运行");
            selectedIndex.SetValue(tabs, 3);
            Application.DoEvents();
            var settingInputs = new[] { "numLeftThreshold", "numRightThreshold", "numLevel88Threshold", "comboRecognitionHotKey", "numRecognitionInterval" }
                .Select(name => (Control)typeof(TiezhuToolbox.MainForm).GetField(name,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!);
            if (settingInputs.Any(control =>
                    control.Height < DpiPixel(32) || control.Width < DpiPixel(70)))
                throw new InvalidOperationException("软件设置输入框尺寸不足");
            var settingRowLabels = new[]
                {
                    "lblThresholdGroup", "lblThLeft", "lblThRight", "lblTh88",
                    "lblRecognitionGroup", "lblRecognitionHotKey", "lblRecognitionInterval", "lblIntervalUnit",
                }
                .Select(name => (Label)typeof(TiezhuToolbox.MainForm).GetField(name,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!);
            var clippedSettingLabel = settingRowLabels.FirstOrDefault(label =>
                label.GetPreferredSize(Size.Empty).Width > label.ClientSize.Width);
            if (clippedSettingLabel != null)
                throw new InvalidOperationException(
                    $"软件设置标签被裁剪：{clippedSettingLabel.Text}，"
                    + $"需要 {clippedSettingLabel.GetPreferredSize(Size.Empty).Width}，"
                    + $"实际 {clippedSettingLabel.ClientSize.Width}");
            var thresholdPanel = (Control)typeof(TiezhuToolbox.MainForm).GetField("thresholdPanel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var level88Input = (Control)typeof(TiezhuToolbox.MainForm).GetField("numLevel88Threshold",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (level88Input.Right > thresholdPanel.ClientSize.Width || level88Input.Bottom > thresholdPanel.ClientSize.Height)
                throw new InvalidOperationException(
                    $"88级阈值输入框被裁剪：输入框={level88Input.Bounds}，容器={thresholdPanel.ClientSize}");
            var settingsRulesLabel = (Label)typeof(TiezhuToolbox.MainForm).GetField("_settingsRulesLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var heroicOnlySpeedCheck = (Control)typeof(TiezhuToolbox.MainForm).GetField("_chkHeroicOnlyGambleSpeed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (heroicOnlySpeedCheck.Width < DpiPixel(400) || heroicOnlySpeedCheck.Height < DpiPixel(32))
                throw new InvalidOperationException("紫装只赌速度设置项尺寸不足");
            var requiredRuleTexts = new[]
                {
                    "红装赌速度", "紫装只赌速度", "速度套速度规则", "暴击项链规则",
                    "套装子类", "右三主属性", "强化分数", "固定主属性",
                };
            if (requiredRuleTexts.Any(text => !settingsRulesLabel.Text.Contains(text)))
                throw new InvalidOperationException("软件设置页缺少自动规则说明");
            var preferredRulesHeight = settingsRulesLabel.GetPreferredSize(
                new Size(settingsRulesLabel.ClientSize.Width, 0)).Height;
            if (preferredRulesHeight > settingsRulesLabel.ClientSize.Height)
                throw new InvalidOperationException(
                    $"自动规则说明被裁剪：需要 {preferredRulesHeight}，实际 {settingsRulesLabel.ClientSize.Height}");
            CaptureTab("software-settings");
            var settingsHost = settingsRulesLabel.Parent?.Parent?.Parent as ScrollableControl
                ?? throw new InvalidOperationException("找不到软件设置滚动容器");
            settingsHost.AutoScrollPosition = new Point(0, settingsHost.VerticalScroll.Maximum);
            Application.DoEvents();
            CaptureTab("software-settings-rules");
            selectedIndex.SetValue(tabs, 0);
            Application.DoEvents();
            if (!timer.Enabled)
                throw new InvalidOperationException("返回装备页后持续识别未恢复");
            loadingField.SetValue(form, true);
            continuousCheck.GetType().GetProperty("Checked")!.SetValue(continuousCheck, false);
            loadingField.SetValue(form, false);
            typeof(TiezhuToolbox.MainForm).GetMethod("ApplyRecognitionAvailability",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form, new object[] { false });

            if (!DemandDatabase.Instance.IsLoaded || DemandDatabase.Instance.Sets.Count != 23)
                throw new InvalidOperationException("静态需求数据未加载");
            var topPanel = (Control)typeof(TiezhuToolbox.MainForm).GetField("topPanel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            Console.WriteLine($"  布局尺寸：窗体={form.ClientSize.Width}，页签={((Control)tabs).ClientSize.Width}，工具栏={topPanel.ClientSize.Width}，DPI={form.DeviceDpi}");
            form.Close();
        }
        catch (Exception ex)
        {
            uiError = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join(TimeSpan.FromSeconds(20));
    if (Directory.Exists(uiTestRoot))
        Directory.Delete(uiTestRoot, recursive: true);
    if (thread.IsAlive)
        throw new TimeoutException("界面冒烟测试超时");
    if (uiError != null)
        throw new InvalidOperationException("界面冒烟测试失败", uiError);
    Console.WriteLine("界面冒烟测试通过：4 个页签，23 个套装需求");
    return;
}

var screenshotsDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\bin\Release\net9.0-windows\win-x64\publish\screenshots";
var templateDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\Assets\Templates";

// 新旧两种分辨率的截图
var imageNames = args.Length > 0
    ? args
    : new[] { "MuMuNxDevice_20260717_031029.png", "MuMuNxDevice_20260717_041111.png" };

// 合成样例自检（无需截图）：
// 样例一：速度套 + 速度主属性鞋 + 副属性{防御,生命,命中,抗性} → 调香师维波里丝(c5154) 应为 100%
// 样例二：暴击套 + 暴击率主属性项链 + 同样副属性 → c5154 主属性/套装均不符，不得出现
// 样例三：速度套速度鞋，副属性{生命,防御,命中,暴击率}但强化全跳暴击率 → c5154 应出现但匹配度大降（<50%）
if (args.Contains("--demand-data"))
{
    var database = DemandDatabase.Instance;
    if (!database.IsLoaded)
        throw new InvalidOperationException($"静态需求数据未加载：{database.ErrorMessage}");
    var profiles = database.Sets.SelectMany(set => set.Profiles).ToList();
    var builds = profiles.SelectMany(profile => profile.Heroes).ToList();
    var uniqueHeroes = builds.Select(hero => hero.Code).Distinct(StringComparer.Ordinal).Count();
    if (database.Sets.Count != 23 || database.Sets.Count(set => set.Profiles.Count > 0) != 21
        || profiles.Count != 171 || builds.Count != 644 || uniqueHeroes != 100)
    {
        throw new InvalidOperationException(
            $"需求数据规模错误：套装 {database.Sets.Count}/有数据 {database.Sets.Count(set => set.Profiles.Count > 0)}/子类 {profiles.Count}/配装 {builds.Count}/英雄 {uniqueHeroes}");
    }
    var duplicateBuildPreserved = profiles.Any(profile => profile.Heroes
        .GroupBy(hero => hero.Code, StringComparer.Ordinal)
        .Any(group => group.Select(hero => hero.ComboName).Distinct(StringComparer.Ordinal).Count() > 1));
    if (!duplicateBuildPreserved)
        throw new InvalidOperationException("同英雄不同完整套装组合未保留");
    var missingSetIcons = database.Sets
        .Where(set => DemandDatabase.GetSetIconPath(set.Code) == null)
        .Select(set => set.Code)
        .ToList();
    var missingAvatars = builds.Select(hero => hero.Code)
        .Distinct(StringComparer.Ordinal)
        .Where(code => DemandDatabase.GetAvatarPath(code) == null)
        .ToList();
    if (missingSetIcons.Count > 0 || missingAvatars.Count > 0)
        throw new InvalidOperationException(
            $"静态图片缺失：套装[{string.Join(",", missingSetIcons)}] 英雄[{string.Join(",", missingAvatars)}]");
    var invalidDocument = new DemandDataDocument
    {
        SchemaVersion = DemandDatabase.CurrentSchemaVersion,
        UpdatedAt = "test",
        Sets =
        {
            new DemandSet
            {
                Code = "invalid",
                Name = "无效套装",
                Profiles =
                {
                    new DemandProfile
                    {
                        Id = "bad",
                        Name = "错误权重",
                        Stats = { "速度" },
                        Weights = new Dictionary<string, double> { ["速度"] = 11 },
                    },
                },
            },
        },
    };
    if (DemandDatabase.Validate(invalidDocument).Count == 0)
        throw new InvalidOperationException("非法需求权重未被数据校验拒绝");
    var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", "demand-profiles.json");
    var json = File.ReadAllText(dataPath);
    var forbidden = new[] { "gear_path", "supply_pieces", "inventory", "C:\\Users\\" };
    if (forbidden.Any(json.Contains))
        throw new InvalidOperationException("静态需求数据仍包含个人库存或供给字段");
    Console.WriteLine("需求数据校验通过：23 套装 / 21 有数据 / 171 子类 / 644 配装 / 100 英雄");
    return;
}

// 合成样例自检（无需截图）：验证套装子类权重、右三满值、固定主属性与强化规则。
if (args.Contains("--synthetic"))
{
    Dictionary<string, double> Weights(
        double speed = 0,
        double hp = 0,
        double crit = 0,
        double critDamage = 0,
        double attack = 0,
        double defense = 0,
        double effectHit = 0,
        double effectResistance = 0)
        => new()
        {
            ["攻击力"] = attack, ["生命值"] = hp, ["防御力"] = defense, ["速度"] = speed,
            ["暴击率"] = crit, ["暴击伤害"] = critDamage,
            ["效果命中"] = effectHit, ["效果抗性"] = effectResistance,
        };

    var weightedSet = new DemandSet
    {
        Code = "set_speed",
        Name = "速度套装",
        Profiles =
        {
            new DemandProfile
            {
                Id = "hp-spd",
                Name = "生命值·速度",
                Stats = { "生命值", "速度" },
                Weights = Weights(speed: 4, hp: 2),
                DemandWeight = 10,
                Heroes =
                {
                    new DemandHeroBuild
                    {
                        Code = "c5154", Name = "调香师维波里丝", ComboName = "速度+生命值",
                        SampleShare = 0.4, DemandContribution = 2, Weights = Weights(speed: 4, hp: 2),
                    },
                    new DemandHeroBuild
                    {
                        Code = "c5154", Name = "调香师维波里丝", ComboName = "速度+免疫",
                        SampleShare = 0.2, DemandContribution = 1, Weights = Weights(speed: 3.5, hp: 2.5),
                    },
                },
            },
        },
    };

    EquipmentInfo RightGear(string mainName, string mainValue, int level = 88) => new()
    {
        Level = level,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = mainName,
        MainStatValue = mainValue,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "防御力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    };

    var speedShoe = SetProfileMatcher.Match(RightGear("速度", "1"), weightedSet, int.MaxValue).Single();
    var hpShoe = SetProfileMatcher.Match(RightGear("生命值", "1%"), weightedSet, int.MaxValue).Single();
    Console.WriteLine($"  速度鞋匹配 {speedShoe.Score}% / 生命鞋匹配 {hpShoe.Score}%");
    if (speedShoe.Score <= hpShoe.Score)
        throw new InvalidOperationException("高速度权重子类未优先推荐速度鞋");
    if (speedShoe.Heroes.Count != 2 || speedShoe.Heroes.Select(hero => hero.ComboName).Distinct().Count() != 2)
        throw new InvalidOperationException("同英雄不同完整套装组合未分别返回");
    var disabledWeightedProfile = new HashSet<string>(StringComparer.Ordinal)
    {
        SetProfileMatcher.CreateProfileKey(weightedSet.Code, weightedSet.Profiles[0].Id),
    };
    if (SetProfileMatcher.Match(
            RightGear("速度", "1"), weightedSet, int.MaxValue, disabledWeightedProfile).Count != 0)
        throw new InvalidOperationException("已停用需求子类仍参与装备匹配");

    string MainContribution(string mainName, string mainValue, string stat, double weight)
    {
        var set = new DemandSet
        {
            Code = "test", Name = "测试套装",
            Profiles =
            {
                new DemandProfile
                {
                    Id = stat, Name = stat, Stats = { stat }, Weights = Weights(
                        speed: stat == "速度" ? weight : 0,
                        hp: stat == "生命值" ? weight : 0,
                        crit: stat == "暴击率" ? weight : 0,
                        critDamage: stat == "暴击伤害" ? weight : 0),
                },
            },
        };
        var part = stat is "暴击率" or "暴击伤害" ? "项链" : "鞋子";
        var info = new EquipmentInfo
        {
            Level = 88, Quality = "传说" + part, MainStatName = mainName, MainStatValue = mainValue,
            SubStats = { new SubStat { Name = "效果命中", Value = "8%" } },
        };
        return SetProfileMatcher.Match(info, set, 1).Single().MainStatContribution;
    }
    if (!MainContribution("暴击率", "1%", "暴击率", 1).Contains("价值 90")
        || !MainContribution("暴击伤害", "1%", "暴击伤害", 1).Contains("价值 78.75")
        || !MainContribution("生命值", "1%", "生命值", 1).Contains("价值 65"))
        throw new InvalidOperationException("右三满值换算错误");

    var score85 = SetProfileMatcher.Match(RightGear("速度", "1", 85), weightedSet, 1).Single().Score;
    var score88 = SetProfileMatcher.Match(RightGear("速度", "1", 88), weightedSet, 1).Single().Score;
    var score90 = SetProfileMatcher.Match(RightGear("速度", "1", 90), weightedSet, 1).Single().Score;
    if (score85 != score88 || score88 != score90)
        throw new InvalidOperationException("85→90预估与88/90满值结果不一致");
    if (SetProfileMatcher.Match(RightGear("生命值", "500", 88), weightedSet, 1).Count != 0)
        throw new InvalidOperationException("固定值右三仍匹配需求子类");
    if (SetProfileMatcher.Match(RightGear("速度", "1", 75), weightedSet, 1).Count != 0
        || SetProfileMatcher.Match(RightGear(string.Empty, string.Empty, 88), weightedSet, 1).Count != 0)
        throw new InvalidOperationException("不支持等级或未识别右三主属性仍返回完整匹配");
    if (SetProfileMatcher.Match(RightGear("速度", "1"), new DemandSet
        {
            Code = "empty", Name = "空套装",
        }, 1).Count != 0)
        throw new InvalidOperationException("无数据套装错误回退到旧算法");

    EquipmentInfo LeftGear(string mainValue) => new()
    {
        Level = 88, Quality = "传说武器", MainStatName = "攻击力", MainStatValue = mainValue,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    };
    var leftA = SetProfileMatcher.Match(LeftGear("100"), weightedSet, 1).Single().Score;
    var leftB = SetProfileMatcher.Match(LeftGear("999"), weightedSet, 1).Single().Score;
    if (leftA != leftB)
        throw new InvalidOperationException("左三固定主属性改变了匹配度");

    var subScoreA = EquipmentScoreCalculator.Calculate(RightGear("速度", "1").SubStats);
    var subScoreB = EquipmentScoreCalculator.Calculate(RightGear("生命值", "1%").SubStats);
    if (subScoreA != subScoreB)
        throw new InvalidOperationException("主属性错误加入副属性装备分");

    EquipmentInfo WeightedWeapon(bool includeEnhanceText = true) => new()
    {
        Level = 88,
        EnhanceLevel = 15,
        Quality = "传说武器",
        SetName = "速度套装",
        MainStatName = "攻击力",
        MainStatValue = "515",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat
            {
                Name = "速度", Value = "13", RollCount = 2,
                EnhanceValue = includeEnhanceText ? "+8" : null,
            },
            new SubStat { Name = "效果抗性", Value = "8%" },
            new SubStat
            {
                Name = "效果命中", Value = "31%", RollCount = 3,
                EnhanceValue = includeEnhanceText ? "+23%" : null,
            },
        },
    };
    var allocationSet = new DemandSet
    {
        Code = "set_speed",
        Name = "速度套装",
        Profiles =
        {
            new DemandProfile
            {
                Id = "hp-spd-hit-res",
                Name = "生命值·速度·效果命中·效果抗性",
                Stats = { "生命值", "速度", "效果命中", "效果抗性" },
                Weights = Weights(speed: 3.1, hp: 2.2, effectHit: 1.2, effectResistance: 2.3),
            },
            new DemandProfile
            {
                Id = "atk-hp-def-spd-hit",
                Name = "攻击力·生命值·防御力·速度·效果命中",
                Stats = { "攻击力", "生命值", "防御力", "速度", "效果命中" },
                Weights = Weights(
                    speed: 2.6, hp: 2, attack: 1.7, defense: 1.2, effectHit: 2.5),
            },
            new DemandProfile
            {
                Id = "spd-hit",
                Name = "速度·效果命中",
                Stats = { "速度", "效果命中" },
                Weights = Weights(speed: 4.1, effectHit: 1.8),
            },
            new DemandProfile
            {
                Id = "hp-spd",
                Name = "生命值·速度",
                Stats = { "生命值", "速度" },
                Weights = Weights(speed: 4, hp: 2),
            },
        },
    };
    var allocationResults = SetProfileMatcher.Match(
            WeightedWeapon(), allocationSet, int.MaxValue)
        .ToDictionary(result => result.ProfileId, StringComparer.Ordinal);
    var fourStatScore = allocationResults["hp-spd-hit-res"].Score;
    var fiveStatScore = allocationResults["atk-hp-def-spd-hit"].Score;
    var speedHitScore = allocationResults["spd-hit"].Score;
    var speedHpScore = allocationResults["hp-spd"].Score;
    Console.WriteLine(
        $"  新需求匹配：四项全中 {fourStatScore}% / 五项缺攻防 {fiveStatScore}% / "
        + $"速度命中 {speedHitScore}% / 速度生命 {speedHpScore}%");
    if (Math.Abs(fourStatScore - 90.1) > 0.1 || fourStatScore <= fiveStatScore)
        throw new InvalidOperationException("四项完全命中子类未按权重分布得到高匹配");
    if (Math.Abs(speedHitScore - 81.9) > 0.1
        || speedHpScore >= 60
        || speedHitScore - speedHpScore < 25)
        throw new InvalidOperationException("双属性子类未区分初始歪词条与歪强化");

    var estimatedResults = SetProfileMatcher.Match(
            WeightedWeapon(includeEnhanceText: false), allocationSet, int.MaxValue)
        .ToDictionary(result => result.ProfileId, StringComparer.Ordinal);
    if (Math.Abs(estimatedResults["hp-spd"].Score - speedHpScore) > 1)
        throw new InvalidOperationException("强化增量漏识别时的 RollCount 估算偏差过大");

    void AssertAdvice(
        string title,
        EquipmentInfo info,
        EnhanceAdvice expected,
        bool heroicOnly = false,
        bool speedSetRequiresSpeed = true,
        bool criticalNecklaceMainStatRule = true)
    {
        var result = EnhancementAdvisor.Analyze(
            info,
            24,
            24,
            28,
            heroicOnlyGambleSpeed: heroicOnly,
            speedSetRequiresSpeed: speedSetRequiresSpeed,
            criticalNecklaceMainStatRule: criticalNecklaceMainStatRule);
        Console.WriteLine($"  {title} → {result.Text}（{result.Detail}）");
        if (result.Advice != expected)
            throw new InvalidOperationException($"强化建议回归失败：{title}，期望 {expected}，实际 {result.Advice}");
    }
    AssertAdvice("传说武器 +3 第一跳歪但可赌速度", new EquipmentInfo
    {
        Level = 85, Quality = "传说武器", EnhanceLevel = 3,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GambleSpeed);
    AssertAdvice("传说武器 +6 连歪两跳", new EquipmentInfo
    {
        Level = 85, Quality = "传说武器", EnhanceLevel = 6,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUp);
    AssertAdvice("紫装只赌速度 +0 无速度", new EquipmentInfo
    {
        Level = 85, Quality = "英雄武器", EnhanceLevel = 0,
        SubStats = { new SubStat { Name = "攻击力", Value = "20%" } },
    }, EnhanceAdvice.GiveUp, heroicOnly: true);
    AssertAdvice("紫装只赌速度 +0 速度3", new EquipmentInfo
    {
        Level = 85, Quality = "英雄武器", EnhanceLevel = 0,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GambleSpeed, heroicOnly: true);
    AssertAdvice("紫装只赌速度不包含鞋子", new EquipmentInfo
    {
        Level = 85, Quality = "英雄鞋子", EnhanceLevel = 0,
        MainStatName = "生命值", MainStatValue = "65%",
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUp, heroicOnly: true);
    AssertAdvice("88级 +15 高分保留", new EquipmentInfo
    {
        Level = 88, Quality = "传说武器", EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "15" },
            new SubStat { Name = "攻击力", Value = "20%" },
            new SubStat { Name = "暴击率", Value = "12%" },
            new SubStat { Name = "暴击伤害", Value = "20%" },
        },
    }, EnhanceAdvice.Keep);
    AssertAdvice("固定防御鞋", new EquipmentInfo
    {
        Level = 88, Quality = "传说鞋子", MainStatName = "防御力", MainStatValue = "500",
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUpFixedMain);

    EquipmentInfo SpeedSetHpBoots() => new()
    {
        Level = 85,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = "生命值",
        MainStatValue = "65%",
        SubStats =
        {
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "防御力", Value = "20%" },
            new SubStat { Name = "效果命中", Value = "20%" },
            new SubStat { Name = "效果抗性", Value = "20%" },
        },
    };
    AssertAdvice(
        "速度套生命鞋特殊规则",
        SpeedSetHpBoots(),
        EnhanceAdvice.GiveUp);
    AssertAdvice(
        "关闭速度套特殊规则",
        SpeedSetHpBoots(),
        EnhanceAdvice.Continue,
        speedSetRequiresSpeed: false);

    EquipmentInfo AttackSetNecklace(string mainStat)
    {
        var info = new EquipmentInfo
        {
            Level = 85,
            Quality = "传说项链",
            SetName = "攻击套装",
            MainStatName = mainStat,
            MainStatValue = mainStat == "暴击伤害" ? "70%" : "65%",
            SubStats =
            {
                new SubStat { Name = "速度", Value = "5" },
                new SubStat { Name = "暴击率", Value = "12%" },
                new SubStat { Name = "效果命中", Value = "8%" },
            },
        };
        info.SubStats.Add(mainStat == "暴击伤害"
            ? new SubStat { Name = "攻击力", Value = "20%" }
            : new SubStat { Name = "暴击伤害", Value = "20%" });
        return info;
    }
    AssertAdvice(
        "双爆需求攻击项链特殊规则",
        AttackSetNecklace("攻击力"),
        EnhanceAdvice.GiveUp);
    AssertAdvice(
        "关闭暴击项链特殊规则",
        AttackSetNecklace("攻击力"),
        EnhanceAdvice.Continue,
        criticalNecklaceMainStatRule: false);
    AssertAdvice(
        "双爆需求暴伤项链",
        AttackSetNecklace("暴击伤害"),
        EnhanceAdvice.Continue);

    var classifyCriticalWeights = typeof(EnhancementAdvisor).GetMethod(
        "GetHighCriticalWeights",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("找不到暴击项链权重分类方法");
    string ClassifyCriticalWeights(IEnumerable<string> stats, double crit, double critDamage)
    {
        var result = classifyCriticalWeights.Invoke(null, new object[]
        {
            stats.ToList(),
            Weights(speed: 3, crit: crit, critDamage: critDamage),
        });
        return result?.ToString() ?? string.Empty;
    }
    if (ClassifyCriticalWeights(new[] { "速度", "暴击率" }, 1.5, 0) != "CriticalChance"
        || ClassifyCriticalWeights(new[] { "速度", "暴击伤害" }, 0, 1.5) != "CriticalDamage"
        || ClassifyCriticalWeights(new[] { "速度", "暴击率", "暴击伤害" }, 1.5, 1.5)
        != "CriticalChance, CriticalDamage")
    {
        throw new InvalidOperationException("单暴击、单暴伤和双爆需求未被分别识别");
    }

    var inferEnhanceLevel = typeof(OcrEngine).GetMethod(
        "InferEnhanceLevelByRolls",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("找不到强化等级推导方法");
    var inferred = (int?)inferEnhanceLevel.Invoke(null, new object[] { "英雄铠甲", 3, 2 });
    if (inferred != 6)
        throw new InvalidOperationException($"强化等级推导失败：期望6，实际{inferred}");

    Console.WriteLine("套装子类匹配、主属性量化与强化建议合成测试通过");
    return;
}

using var engine = new OcrEngine(templateDir);

foreach (var name in imageNames)
{
    var imagePath = Path.Combine(screenshotsDir, name);
    if (!File.Exists(imagePath))
    {
        Console.WriteLine($"截图不存在: {imagePath}");
        continue;
    }

    Console.WriteLine($"===== 测试图片: {name} =====");

    var info = await engine.RecognizeAsync(imagePath);

    Console.WriteLine("识别结果:");
    Console.WriteLine($"  装备等级: {info.Level}");
    Console.WriteLine($"  强化等级: +{info.EnhanceLevel}");
    Console.WriteLine($"  装备品质: {info.Quality}");
    Console.WriteLine($"  主属性: {info.MainStatName} {info.MainStatValue}");
    Console.WriteLine($"  副属性:");
    foreach (var sub in info.SubStats)
    {
        var rollText = sub.RollCount > 0 ? $"({sub.RollCount})" : string.Empty;
        Console.WriteLine($"    - {sub.Name}{rollText} {sub.Value}" + (string.IsNullOrEmpty(sub.EnhanceValue) ? "" : $" ({sub.EnhanceValue})"));
    }
    Console.WriteLine($"  套装: {info.SetName}");
    Console.WriteLine($"  装备分数: {info.Score}");

    // 强化建议（阈值 24/24）
    var advice = EnhancementAdvisor.Analyze(info, 24, 24);
    Console.WriteLine($"  强化建议: {advice.Text}（{advice.Detail}）");

    // 装备 → 当前套装属性子类推荐
    var recommendations = SetProfileMatcher.Match(info);
    Console.WriteLine("  适用子类:");
    foreach (var rec in recommendations)
        Console.WriteLine($"    - {rec.ProfileName} 匹配度 {rec.Score}%  命中=[{string.Join(",", rec.MatchedStats)}] {rec.MainStatContribution}");
    if (recommendations.Count == 0)
        Console.WriteLine("    （无匹配或静态需求数据缺失）");

    Console.WriteLine();
    Console.WriteLine("原始文本:");
    Console.WriteLine(info.RawText);
    Console.WriteLine();
}
