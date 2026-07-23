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
        var database = HeroDatabase.Instance;
        var original = database.Profiles.First(profile => profile.AllowedSets.Count > 0 && profile.UsefulStats.Count > 0);
        var matchingGear = new EquipmentInfo
        {
            Quality = "传说武器",
            SubStats =
            [
                new SubStat
                {
                    Name = original.UsefulStats[0],
                    Value = original.UsefulStats[0] == "速度" ? "4" : "8%",
                },
            ],
        };
        var excludedProfile = original.Clone();
        excludedProfile.IsExcluded = true;
        if (HeroRecommender.Recommend(matchingGear, [excludedProfile], database.SetCodesByName).Count != 0)
            throw new InvalidOperationException("已屏蔽英雄仍参与装备推荐");
        var custom = original.Clone();
        custom.UsefulStats.Clear();
        custom.AllowedSets.Clear();
        custom.NecklaceMainStats.Clear();
        custom.RingMainStats.Clear();
        custom.BootsMainStats.Clear();
        custom.IsExcluded = true;
        database.SaveOverride(custom);

        var overridePath = Path.Combine(testRoot, "hero-overrides.json");
        if (!File.Exists(overridePath))
            throw new InvalidOperationException("英雄覆盖文件未保存");
        database.Reload();
        var reloaded = database.GetProfile(original.Code)!;
        if (reloaded.UsefulStats.Count != 0 || reloaded.AllowedSets.Count != 0 || !reloaded.IsExcluded)
            throw new InvalidOperationException("英雄覆盖配置未在重载后生效");

        var archivePath = Path.Combine(testRoot, "hero-config-archive.json");
        database.ExportOverrides(archivePath);
        if (!File.Exists(archivePath))
            throw new InvalidOperationException("英雄配置存档未导出");
        database.ResetOverride(original.Code);
        var reset = database.GetProfile(original.Code)!;
        if (reset.AllowedSets.Count == 0 || reset.IsExcluded)
            throw new InvalidOperationException("恢复英雄默认配置失败");
        database.ImportOverrides(archivePath);
        var imported = database.GetProfile(original.Code)!;
        if (!imported.IsExcluded || imported.AllowedSets.Count != 0)
            throw new InvalidOperationException("英雄配置存档导入失败");
        database.ResetOverride(original.Code);

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
                if (value != 31M || level88Value != 33M || maxAutoValue != 17M
                    || disposalValue != "分解" || matchValue != 82M || stopOnValuableValue
                    || !heroicOnlyGambleSpeedValue
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
        Console.WriteLine($"配置持久化测试通过：{original.Name}（{original.Code}），软件设置 31/33/17/分解/82%/紫装只赌速度/符合后继续/127.0.0.1:5555");
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
            if (addressInput.Width < 200)
                throw new InvalidOperationException($"ADB 地址输入框宽度不足：{addressInput.Width}");
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
            CaptureTab("hero-config");
            var heroConfig = typeof(TiezhuToolbox.MainForm).GetField("_heroConfigControl",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var heroList = (ListBox)heroConfig.GetType().GetField("_heroList",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(heroConfig)!;
            var usefulChecks = (System.Collections.IDictionary)heroConfig.GetType().GetField("_usefulStatChecks",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(heroConfig)!;
            var resistanceCheck = (Control)(usefulChecks["效果抗性"]
                ?? throw new InvalidOperationException("有效属性缺少效果抗性选项"));
            var usefulOptions = resistanceCheck.Parent
                ?? throw new InvalidOperationException("效果抗性选项未加入有效属性区域");
            void AssertUsefulStatsVisible()
            {
                foreach (Control check in usefulChecks.Values)
                    if (check.Right + check.Margin.Right > usefulOptions.ClientSize.Width
                        || check.Bottom + check.Margin.Bottom > usefulOptions.ClientSize.Height)
                        throw new InvalidOperationException(
                            $"有效属性选项被裁剪：{check.Text}={check.Bounds}，容器={usefulOptions.ClientSize}");
            }

            var originalClientSize = form.ClientSize;
            form.ClientSize = new Size(form.MinimumSize.Width, originalClientSize.Height);
            Application.DoEvents();
            AssertUsefulStatsVisible();
            var narrowSectionHeight = usefulOptions.Parent!.Height;
            form.ClientSize = new Size(1400, originalClientSize.Height);
            Application.DoEvents();
            AssertUsefulStatsVisible();
            var wideSectionHeight = usefulOptions.Parent!.Height;
            if (narrowSectionHeight <= wideSectionHeight)
                throw new InvalidOperationException(
                    $"有效属性区域未随宽度调整高度：窄={narrowSectionHeight}，宽={wideSectionHeight}");
            form.ClientSize = originalClientSize;
            Application.DoEvents();
            AssertUsefulStatsVisible();
            var firstUsefulCheck = usefulChecks.Values.Cast<object>().First();
            var checkedProperty = firstUsefulCheck.GetType().GetProperty("Checked")!;
            var changedValue = !(bool)checkedProperty.GetValue(firstUsefulCheck)!;
            checkedProperty.SetValue(firstUsefulCheck, changedValue);
            Application.DoEvents();
            heroList.SelectedIndex = 1;
            heroList.SelectedIndex = 0;
            Application.DoEvents();
            if ((bool)checkedProperty.GetValue(firstUsefulCheck)! != changedValue)
                throw new InvalidOperationException("英雄配置在切换角色后恢复为旧状态");
            var participatesCheck = heroConfig.GetType().GetField("_participatesInMatchingCheck",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(heroConfig)!;
            var participatesProperty = participatesCheck.GetType().GetProperty("Checked")!;
            participatesProperty.SetValue(participatesCheck, false);
            Application.DoEvents();
            heroList.SelectedIndex = 1;
            heroList.SelectedIndex = 0;
            Application.DoEvents();
            if ((bool)participatesProperty.GetValue(participatesCheck)!)
                throw new InvalidOperationException("英雄屏蔽状态在切换角色后丢失");
            heroList.TopIndex = 0;
            heroList.GetType().GetMethod("OnMouseWheel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(heroList, new object[] { new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120) });
            if (heroList.TopIndex == 0)
                throw new InvalidOperationException("隐藏滚动条后英雄列表无法使用滚轮滚动");
            var heroWithMostCombos = HeroDatabase.Instance.Profiles.OrderByDescending(profile => profile.SetCombos.Count).First();
            heroConfig.GetType().GetMethod("RefreshData")!.Invoke(heroConfig, new object?[] { heroWithMostCombos.Code });
            Application.DoEvents();
            var editor = (FlowLayoutPanel)(heroConfig.GetType().GetField("_editor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(heroConfig)
                ?? throw new InvalidOperationException("英雄编辑区未初始化"));
            editor.AutoScrollPosition = new Point(0, editor.VerticalScroll.Maximum);
            Application.DoEvents();
            CaptureTab("hero-config-bottom");
            var lastContentSection = editor.Controls.Cast<Control>().Reverse().Skip(1).First();
            var visibleEditorHeight = editor.Parent?.ClientSize.Height ?? editor.ClientSize.Height;
            if (lastContentSection.Bottom > visibleEditorHeight)
                throw new InvalidOperationException($"英雄编辑区滚动到底后内容仍被截断：{lastContentSection.Bottom}/{visibleEditorHeight}");
            var comboInfo = (Label)heroConfig.GetType().GetField("_comboInfo",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(heroConfig)!;
            if (comboInfo.PreferredHeight > comboInfo.ClientSize.Height)
                throw new InvalidOperationException($"官方组合内容仍被截断：{comboInfo.PreferredHeight}/{comboInfo.ClientSize.Height}");
            if (timer.Enabled)
                throw new InvalidOperationException("离开装备页后持续识别仍在运行");
            selectedIndex.SetValue(tabs, 3);
            Application.DoEvents();
            var settingInputs = new[] { "numLeftThreshold", "numRightThreshold", "numLevel88Threshold", "comboRecognitionHotKey", "numRecognitionInterval" }
                .Select(name => (Control)typeof(TiezhuToolbox.MainForm).GetField(name,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!);
            if (settingInputs.Any(control => control.Height < 32 || control.Width < 70))
                throw new InvalidOperationException("软件设置输入框尺寸不足");
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
            if (heroicOnlySpeedCheck.Width < 400 || heroicOnlySpeedCheck.Height < 32)
                throw new InvalidOperationException("紫装只赌速度设置项尺寸不足");
            var requiredRuleTexts = new[]
                { "红装赌速度", "紫装只赌速度", "速度硬门槛", "速度鞋默认", "双爆项链默认", "速度套补全" };
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

            if (HeroDatabase.Instance.Profiles.Count < 300)
                throw new InvalidOperationException($"全部英雄数据未加载：{HeroDatabase.Instance.Profiles.Count}");
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
    Console.WriteLine($"界面冒烟测试通过：4 个页签，{HeroDatabase.Instance.Profiles.Count} 个英雄");
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
if (args.Contains("--synthetic"))
{
    IReadOnlyList<HeroRecommendation> Print(string title, EquipmentInfo info)
    {
        Console.WriteLine($"===== 合成样例: {title} =====");
        var recs = HeroRecommender.Recommend(info, top: int.MaxValue);
        foreach (var rec in recs.Take(5))
            Console.WriteLine($"  {rec.Name}({rec.Code}) 匹配度 {rec.Score}%");
        Console.WriteLine(recs.Any(r => r.Code == "c5154") ? "  → 含 c5154" : "  → 不含 c5154");
        Console.WriteLine();
        return recs;
    }

    var perfectRightGear = new EquipmentInfo
    {
        Level = 88,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = "速度",
        MainStatValue = "45",
        SubStats =
        {
            new SubStat { Name = "防御力", Value = "10%" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "7%" },
            new SubStat { Name = "效果抗性", Value = "7%" },
        },
    };
    var perfectRightRecommendations = Print("速度套速度鞋 副属性{防御,生命,命中,抗性}", perfectRightGear);
    var c5154Perfect = perfectRightRecommendations.SingleOrDefault(r => r.Code == "c5154");
    if (c5154Perfect?.Score != 100)
        throw new InvalidOperationException(
            $"右三件主属性占用需求回归失败：c5154 期望 100%，实际 {c5154Perfect?.Score.ToString() ?? "未推荐"}%");

    var rejectedRightGear = new EquipmentInfo
    {
        Level = 88,
        Quality = "传说项链",
        SetName = "暴击套装",
        MainStatName = "暴击率",
        MainStatValue = "55%",
        SubStats =
        {
            new SubStat { Name = "防御力", Value = "10%" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "8" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    };
    var rejectedRightRecommendations = Print(
        "暴击套暴击项链 副属性{防御,生命,速度,命中}", rejectedRightGear);
    if (rejectedRightRecommendations.Any(r => r.Code == "c5154"))
        throw new InvalidOperationException("右三件主属性/套装硬门槛回归失败：不应推荐 c5154");

    var wastedRollGear = new EquipmentInfo
    {
        Level = 88,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = "速度",
        MainStatValue = "45",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "6%" },
            new SubStat { Name = "防御力", Value = "6%" },
            new SubStat { Name = "效果命中", Value = "6%" },
            new SubStat { Name = "暴击率", Value = "24%", RollCount = 5 },
        },
    };
    var wastedRollRecommendations = Print(
        "速度套速度鞋 副属性{生命,防御,命中,暴击率}强化全跳暴击", wastedRollGear);
    var c5154WastedRolls = wastedRollRecommendations.SingleOrDefault(r => r.Code == "c5154");
    if (c5154WastedRolls == null || c5154WastedRolls.Score >= 50)
        throw new InvalidOperationException(
            $"无用属性强化惩罚回归失败：c5154 应低于 50%，实际 {c5154WastedRolls?.Score.ToString() ?? "未推荐"}%");

    var noSpeedTankGear = new EquipmentInfo
    {
        Level = 85,
        Quality = "传说头盔",
        MainStatName = "生命值",
        MainStatValue = "540",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "7%" },
            new SubStat { Name = "效果抗性", Value = "7%" },
            new SubStat { Name = "防御力", Value = "7%" },
            new SubStat { Name = "效果命中", Value = "6%" },
        },
    };
    var speedRequiredTankCodes = new[] { "c1137", "c2022", "c3094", "c4004", "c4044" };
    var leakedSpeedRequiredTanks = HeroRecommender.Recommend(noSpeedTankGear, top: int.MaxValue)
        .Where(r => speedRequiredTankCodes.Contains(r.Code))
        .Select(r => r.Name)
        .ToList();
    Console.WriteLine("===== 无速度坦克装硬门槛样例 =====");
    Console.WriteLine(leakedSpeedRequiredTanks.Count == 0
        ? "  需要速度的五名角色均已淘汰"
        : $"  错误推荐：{string.Join("、", leakedSpeedRequiredTanks)}");
    if (leakedSpeedRequiredTanks.Count > 0)
        throw new InvalidOperationException("缺少速度的坦克装备错误推荐给需要速度的角色");
    Console.WriteLine();

    Console.WriteLine("===== 自定义英雄主属性配置样例 =====");
    var customProfiles = new List<HeroProfile>
    {
        new()
        {
            Code = "test001",
            Name = "测试英雄",
            UsefulStats = new List<string> { "速度", "生命值" },
            AllowedSets = new List<string> { "set_speed" },
            NecklaceMainStats = new List<string> { "生命值%" },
            RingMainStats = new List<string> { "生命值%" },
            BootsMainStats = new List<string> { "速度" },
        },
    };
    var customSetNames = new Dictionary<string, string> { ["速度套装"] = "set_speed" };
    EquipmentInfo CustomGear(string quality, string mainName, string mainValue)
    {
        var info = new EquipmentInfo
        {
            Quality = quality,
            SetName = "速度套装",
            MainStatName = mainName,
            MainStatValue = mainValue,
        };
        // 主属性占用角色需求时，用另一个需求作为有效副属性，避免制造游戏中不可能出现的重复属性。
        info.SubStats.Add(mainName == "生命值" && mainValue.Contains('%')
            ? new SubStat { Name = "速度", Value = "4" }
            : new SubStat { Name = "生命值", Value = "8%" });
        info.SubStats.Add(new SubStat { Name = "防御力", Value = "8%" });
        info.SubStats.Add(new SubStat { Name = "效果命中", Value = "8%" });
        info.SubStats.Add(new SubStat { Name = "效果抗性", Value = "8%" });
        return info;
    }
    void AssertCustom(string title, EquipmentInfo gear, bool shouldMatch)
    {
        var matched = HeroRecommender.Recommend(gear, customProfiles, customSetNames).Any();
        Console.WriteLine($"  {title} → {(matched ? "匹配" : "淘汰")}");
        if (matched != shouldMatch)
            throw new InvalidOperationException($"自定义英雄配置回归失败：{title}");
    }
    AssertCustom("速度鞋允许速度主属性", CustomGear("传说鞋子", "速度", "45"), true);
    AssertCustom("项链拒绝速度主属性", CustomGear("传说项链", "速度", "45"), false);
    AssertCustom("项链接受生命值百分比", CustomGear("传说项链", "生命值", "65%"), true);
    AssertCustom("戒指拒绝固定生命值", CustomGear("传说戒指", "生命值", "500"), false);
    AssertCustom("角色需求速度但装备没有速度", new EquipmentInfo
    {
        Quality = "传说装备",
        SetName = "速度套装",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "防御力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    }, false);

    void AssertCustomScore(string title, EquipmentInfo gear, double expectedScore)
    {
        var recommendation = HeroRecommender.Recommend(gear, customProfiles, customSetNames).SingleOrDefault()
            ?? throw new InvalidOperationException($"推荐匹配度回归失败：{title}，未推荐测试英雄");
        Console.WriteLine($"  {title} → {recommendation.Score}%");
        if (Math.Abs(recommendation.Score - expectedScore) > 0.1)
            throw new InvalidOperationException(
                $"推荐匹配度回归失败：{title}，期望 {expectedScore}%，实际 {recommendation.Score}%");
    }

    AssertCustomScore("两种需求配两种有效副属性，其余为必然填充", new EquipmentInfo
    {
        Quality = "传说装备",
        SetName = "速度套装",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    }, 100);
    AssertCustomScore("只覆盖两种需求中的一种", new EquipmentInfo
    {
        Quality = "传说装备",
        SetName = "速度套装",
        SubStats =
        {
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "防御力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    }, 50);
    AssertCustomScore("速度主属性鞋只需覆盖剩余的生命值需求",
        CustomGear("传说鞋子", "速度", "45"), 100);
    AssertCustomScore("无用属性吃到强化仍会降低匹配度", new EquipmentInfo
    {
        Quality = "传说装备",
        SetName = "速度套装",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "24%", RollCount = 5 },
        },
    }, 44.4);

    var singleStatProfiles = new List<HeroProfile>
    {
        new()
        {
            Code = "test002",
            Name = "单属性测试英雄",
            UsefulStats = new List<string> { "生命值" },
            AllowedSets = new List<string> { "set_speed" },
        },
    };
    var singleStatGear = new EquipmentInfo
    {
        Level = 85,
        EnhanceLevel = 15,
        Quality = "传说头盔",
        SetName = "速度套装",
        MainStatName = "生命值",
        MainStatValue = "700",
        SubStats =
        {
            new SubStat { Name = "速度", Value = "2" },
            new SubStat { Name = "攻击力", Value = "16%", RollCount = 1 },
            new SubStat { Name = "生命值", Value = "19%", RollCount = 2 },
            new SubStat { Name = "暴击伤害", Value = "14%", RollCount = 2 },
        },
    };
    var singleStatRecommendation = HeroRecommender
        .Recommend(singleStatGear, singleStatProfiles, customSetNames)
        .SingleOrDefault()
        ?? throw new InvalidOperationException("单属性角色强化分配回归失败：未推荐测试英雄");
    Console.WriteLine($"  单属性角色仅两跳生命 → {singleStatRecommendation.Score}%");
    if (Math.Abs(singleStatRecommendation.Score - 50.7) > 0.1)
        throw new InvalidOperationException(
            $"单属性角色强化分配回归失败：期望 50.7%，实际 {singleStatRecommendation.Score}%");
    Console.WriteLine();

    Console.WriteLine("===== 官方属性直方图推导 =====");
    var c1034Histograms = new Dictionary<string, double[]>
    {
        ["att"] = [0, 2250, 0, 0, 0, 175, 2358, 180, 18, 1],
        ["def"] = [2250, 2716, 15, 1, 0, 0, 0, 0, 0, 0],
        ["max_hp"] = [2982, 1997, 3, 0, 0, 0, 0, 0, 0, 0],
        ["speed"] = [2250, 0, 2, 3, 24, 4, 46, 214, 1177, 1262],
        ["cri"] = [2250, 0, 0, 0, 0, 0, 0, 5, 209, 2518],
        ["cri_dmg"] = [2250, 0, 0, 0, 0, 1, 1413, 868, 349, 101],
        ["acc"] = [352, 15, 0, 0, 0, 0, 0, 0, 0, 0],
        ["res"] = [342, 0, 0, 1, 0, 0, 0, 0, 0, 0],
    };
    var unavailableSamples = HeroUsefulStatAnalyzer.EstimateUnavailableSamples(c1034Histograms);
    var c1034UsefulStats = HeroUsefulStatAnalyzer.InferUsefulStats(c1034Histograms);
    Console.WriteLine($"  c1034 不可见样本={unavailableSamples:0}，有效属性={string.Join("、", c1034UsefulStats)}");
    if (unavailableSamples != 2250 ||
        !c1034UsefulStats.SequenceEqual(new[] { "攻击力", "速度", "暴击率", "暴击伤害" }))
        throw new InvalidOperationException("c1034 官方属性直方图推导失败");
    var c1126Histograms = new Dictionary<string, double[]>
    {
        ["att"] = [65, 8, 166, 2, 1, 0, 0, 0, 0, 0],
        ["def"] = [65, 5, 35, 29, 35, 73, 0, 0, 0, 0],
        ["max_hp"] = [65, 1, 28, 86, 44, 16, 2, 0, 0, 0],
        ["speed"] = [0, 65, 0, 0, 0, 0, 0, 0, 0, 177],
        ["cri"] = [222, 10, 9, 0, 0, 0, 0, 1, 0, 0],
        ["cri_dmg"] = [223, 4, 0, 15, 0, 0, 0, 0, 0, 0],
        ["acc"] = [0, 0, 0, 0, 0, 15, 10, 35, 36, 81],
        ["res"] = [65, 10, 0, 0, 0, 0, 0, 0, 0, 0],
    };
    var c1126UsefulStats = HeroUsefulStatAnalyzer.InferUsefulStats(c1126Histograms);
    Console.WriteLine($"  c1126 有效属性={string.Join("、", c1126UsefulStats)}（不得误加攻击力）");
    if (!c1126UsefulStats.SequenceEqual(new[] { "防御力", "速度", "效果命中" }))
        throw new InvalidOperationException("c1126 官方属性直方图推导过宽");

    var setImpliedStats = new List<string> { "防御力", "生命值", "效果抗性" };
    HeroUsefulStatAnalyzer.ApplySetImplications(setImpliedStats,
    [
        new HeroSetCombo { Sets = new List<string> { "set_res", "set_speed" }, Rate = 12.5 },
    ]);
    Console.WriteLine($"  主流速度套补足有效属性={string.Join("、", setImpliedStats)}");
    if (!setImpliedStats.Contains("速度"))
        throw new InvalidOperationException("主流速度套未补足速度有效属性");
    Console.WriteLine();

    Console.WriteLine("===== 默认主属性推导规则 =====");
    void AssertDerived(string title, IReadOnlyCollection<string> actual, params string[] expected)
    {
        var matches = actual.Count == expected.Length && expected.All(actual.Contains);
        Console.WriteLine($"  {title} → {string.Join("、", actual)}");
        if (!matches)
            throw new InvalidOperationException($"默认主属性推导失败：{title}，期望 {string.Join("、", expected)}");
    }
    AssertDerived("有效属性含暴击时项链只保留暴击",
        EquipmentRules.DeriveNecklaceMainStats(new[] { "攻击力", "生命值", "暴击率" }), "暴击率");
    AssertDerived("有效属性含暴击和爆伤时项链只保留两者",
        EquipmentRules.DeriveNecklaceMainStats(new[] { "攻击力", "暴击率", "暴击伤害" }), "暴击率", "暴击伤害");
    AssertDerived("无暴击属性时项链按普通有效属性推导",
        EquipmentRules.DeriveNecklaceMainStats(new[] { "攻击力", "生命值" }), "攻击力%", "生命值%");
    AssertDerived("有效属性含速度时鞋子只保留速度",
        EquipmentRules.DeriveBootsMainStats(new[] { "速度", "生命值", "防御力" }), "速度");
    AssertDerived("无速度属性时鞋子按普通有效属性推导",
        EquipmentRules.DeriveBootsMainStats(new[] { "生命值", "防御力" }), "防御力%", "生命值%");
    Console.WriteLine();

    Console.WriteLine("===== 副属性强化次数推导 =====");
    var inferEnhanceLevel = typeof(OcrEngine).GetMethod(
        "InferEnhanceLevelByRolls",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("找不到强化等级推导方法");
    void AssertInferredEnhanceLevel(string title, string quality, int subStatCount, int totalRolls, int? expected)
    {
        var actual = (int?)inferEnhanceLevel.Invoke(null, new object[] { quality, subStatCount, totalRolls });
        Console.WriteLine($"  {title} → {(actual is int level ? $"+{level}" : "不推导")}");
        if (actual != expected)
            throw new InvalidOperationException($"强化等级推导失败：{title}，期望 {expected?.ToString() ?? "null"}，实际 {actual?.ToString() ?? "null"}");
    }
    AssertInferredEnhanceLevel("英雄三词条、累计强化 2 次", "英雄铠甲", 3, 2, 6);
    AssertInferredEnhanceLevel("英雄三词条、累计强化 3 次", "英雄铠甲", 3, 3, 9);
    AssertInferredEnhanceLevel("英雄四词条、第四条刚解锁", "英雄铠甲", 4, 3, 12);
    AssertInferredEnhanceLevel("英雄四词条、累计强化 4 次", "英雄铠甲", 4, 4, 15);
    AssertInferredEnhanceLevel("英雄四词条但计数不足", "英雄铠甲", 4, 2, null);
    AssertInferredEnhanceLevel("传说四词条、累计强化 2 次", "传说铠甲", 4, 2, 6);
    Console.WriteLine();

    // 强化建议自检（85级阈值 24/24，88级阈值 28）
    void PrintAdvice(
        string title,
        EquipmentInfo info,
        EnhanceAdvice? expected = null,
        bool heroicOnlyGambleSpeed = false)
    {
        var r = EnhancementAdvisor.Analyze(
            info, 24, 24, 28, heroicOnlyGambleSpeed: heroicOnlyGambleSpeed);
        Console.WriteLine($"  [强化建议] {title} → {r.Text}（{r.Detail}）");
        if (expected != null && r.Advice != expected)
            throw new InvalidOperationException($"强化建议回归失败：期望 {expected}，实际 {r.Advice}");
    }

    Console.WriteLine("===== 强化建议样例（85级阈值 24/24，88级阈值 28） =====");
    PrintAdvice("传说武器 +3 第一跳歪掉但仍可赌速度（应：继续赌速度）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 3,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GambleSpeed);
    PrintAdvice("传说武器 +6 连歪两跳（应：放弃）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 6,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUp);
    PrintAdvice("紫装只赌速度 +0 高分但无速度（应：放弃）", new EquipmentInfo
    {
        Level = 85,
        Quality = "英雄武器",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "攻击力", Value = "20%" },
            new SubStat { Name = "暴击率", Value = "15%" },
            new SubStat { Name = "暴击伤害", Value = "20%" },
        },
    }, EnhanceAdvice.GiveUp, heroicOnlyGambleSpeed: true);
    PrintAdvice("紫装只赌速度 +0 低分但速度 3（应：继续赌速度）", new EquipmentInfo
    {
        Level = 85,
        Quality = "英雄武器",
        EnhanceLevel = 0,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GambleSpeed, heroicOnlyGambleSpeed: true);
    PrintAdvice("紫装只赌速度 +3 第一跳歪掉（应：放弃）", new EquipmentInfo
    {
        Level = 85,
        Quality = "英雄武器",
        EnhanceLevel = 3,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUp, heroicOnlyGambleSpeed: true);
    PrintAdvice("紫装只赌速度 +12 新增第四词条不提高速度要求（应：继续赌速度）", new EquipmentInfo
    {
        Level = 85,
        Quality = "英雄武器",
        EnhanceLevel = 12,
        SubStats = { new SubStat { Name = "速度", Value = "12" } },
    }, EnhanceAdvice.GambleSpeed, heroicOnlyGambleSpeed: true);
    PrintAdvice("传说武器 +0 高分（应：继续强化）", new EquipmentInfo
    {
        Quality = "传说武器",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "8" },
            new SubStat { Name = "暴击率", Value = "8%" },
            new SubStat { Name = "攻击力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    });
    PrintAdvice("弱化套效果命中戒指 +0 分数达标但最高匹配度低于 70% 且无速度（应：放弃）", new EquipmentInfo
    {
        Quality = "传说戒指",
        SetName = "弱化套装",
        MainStatName = "效果命中",
        MainStatValue = "60%",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "暴击率", Value = "5%" },
            new SubStat { Name = "暴击伤害", Value = "7%" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    }, EnhanceAdvice.GiveUp);
    PrintAdvice("弱化套效果命中戒指 +0 匹配度低但速度 3（应：按分数继续强化）", new EquipmentInfo
    {
        Quality = "传说戒指",
        SetName = "弱化套装",
        MainStatName = "效果命中",
        MainStatValue = "60%",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "暴击率", Value = "5%" },
            new SubStat { Name = "暴击伤害", Value = "7%" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "速度", Value = "3" },
        },
    }, EnhanceAdvice.Continue);
    PrintAdvice("传说戒指 固定防御主属性速度4（应：作为速度散件继续赌）", new EquipmentInfo
    {
        Quality = "传说戒指",
        MainStatName = "防御力",
        MainStatValue = "60",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "防御力", Value = "8%" },
            new SubStat { Name = "攻击力", Value = "38" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "4" },
        },
    });
    PrintAdvice("传说武器 +0 低分带速度3（应：继续赌速度）", new EquipmentInfo
    {
        Quality = "传说武器",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "3" },
            new SubStat { Name = "生命值", Value = "4%" },
            new SubStat { Name = "防御力", Value = "4%" },
            new SubStat { Name = "效果命中", Value = "4%" },
        },
    });
    PrintAdvice("传说戒指 固定攻击主属性（应：固定值主属性，建议放弃）", new EquipmentInfo
    {
        Quality = "传说戒指",
        MainStatName = "攻击力",
        MainStatValue = "500",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "2" },
            new SubStat { Name = "暴击率", Value = "8%" },
            new SubStat { Name = "攻击力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    });
    PrintAdvice("传说戒指 百分比主属性低分无速度（应：分数过低，建议放弃）", new EquipmentInfo
    {
        Quality = "传说戒指",
        MainStatName = "攻击力",
        MainStatValue = "60%",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "4%" },
            new SubStat { Name = "防御力", Value = "4%" },
            new SubStat { Name = "效果命中", Value = "4%" },
            new SubStat { Name = "效果抗性", Value = "4%" },
        },
    });
    PrintAdvice("传说武器 +15 预计重铸恰好 65 分（应：建议重铸）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "效果命中", Value = "7%", RollCount = 0 },
            new SubStat { Name = "攻击力", Value = "15%", RollCount = 1 },
            new SubStat { Name = "暴击率", Value = "4%", RollCount = 0 },
            new SubStat { Name = "暴击伤害", Value = "22%", RollCount = 4 },
        },
    });
    PrintAdvice("传说武器 +15 预计重铸 64 分（应：分数过低，建议放弃）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "效果命中", Value = "6%", RollCount = 0 },
            new SubStat { Name = "攻击力", Value = "15%", RollCount = 1 },
            new SubStat { Name = "暴击率", Value = "4%", RollCount = 0 },
            new SubStat { Name = "暴击伤害", Value = "22%", RollCount = 4 },
        },
    });
    PrintAdvice("传说武器 +15 预计重铸低于 65 但速度 15（应：建议重铸）", new EquipmentInfo
    {
        Level = 85,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "15", RollCount = 5 },
            new SubStat { Name = "生命值", Value = "4%", RollCount = 0 },
            new SubStat { Name = "防御力", Value = "4%", RollCount = 0 },
            new SubStat { Name = "效果命中", Value = "4%", RollCount = 0 },
        },
    });
    PrintAdvice("传说武器 90 级 +15（应：已完成重铸）", new EquipmentInfo
    {
        Level = 90,
        Quality = "传说武器",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "16", RollCount = 5 },
            new SubStat { Name = "生命值", Value = "18%", RollCount = 0 },
            new SubStat { Name = "防御力", Value = "18%", RollCount = 0 },
            new SubStat { Name = "效果命中", Value = "18%", RollCount = 0 },
        },
    });
    PrintAdvice("88级传说武器 +3 恰好 35 分（应：按每跳 +7 继续强化）", new EquipmentInfo
    {
        Level = 88,
        Quality = "传说武器",
        EnhanceLevel = 3,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "6" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "防御力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    }, EnhanceAdvice.Continue);
    PrintAdvice("88级传说武器 +3 只有 34 分（应：分数不达 35，仅继续赌速度）", new EquipmentInfo
    {
        Level = 88,
        Quality = "传说武器",
        EnhanceLevel = 3,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "6" },
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "防御力", Value = "7%" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    }, EnhanceAdvice.GambleSpeed);
    PrintAdvice("88级速度鞋 +15 恰好 63 分（应：不可重铸，建议保留）", new EquipmentInfo
    {
        Level = 88,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = "速度",
        MainStatValue = "45",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "3" },
            new SubStat { Name = "生命值", Value = "20%" },
            new SubStat { Name = "防御力", Value = "20%" },
            new SubStat { Name = "效果命中", Value = "17%" },
        },
    }, EnhanceAdvice.Keep);
    PrintAdvice("88级速度鞋 +15 只有 62 分（应：不可重铸，建议放弃）", new EquipmentInfo
    {
        Level = 88,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = "速度",
        MainStatValue = "45",
        EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "3" },
            new SubStat { Name = "生命值", Value = "20%" },
            new SubStat { Name = "防御力", Value = "19%" },
            new SubStat { Name = "效果命中", Value = "17%" },
        },
    }, EnhanceAdvice.GiveUp);
    PrintAdvice("速度鞋 低分（应：分数过低，建议放弃，鞋子不赌速度）", new EquipmentInfo
    {
        Quality = "传说鞋子",
        MainStatName = "速度",
        MainStatValue = "45",
        EnhanceLevel = 0,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "4%" },
            new SubStat { Name = "防御力", Value = "4%" },
            new SubStat { Name = "效果命中", Value = "4%" },
            new SubStat { Name = "效果抗性", Value = "4%" },
        },
    });
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

    // 装备 → 适用角色推荐（官方战绩前排分段数据）
    var recommendations = HeroRecommender.Recommend(info);
    Console.WriteLine("  适用角色:");
    foreach (var rec in recommendations)
        Console.WriteLine($"    - {rec.Name}({rec.Code}) 匹配度 {rec.Score}%  命中副属性=[{string.Join(",", rec.MatchedStats)}] 套装命中={rec.SetMatched}");
    if (recommendations.Count == 0)
        Console.WriteLine("    （无匹配或 heroes.json 缺失）");

    Console.WriteLine();
    Console.WriteLine("原始文本:");
    Console.WriteLine(info.RawText);
    Console.WriteLine();
}
