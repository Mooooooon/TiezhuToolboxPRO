using TiezhuToolbox.Modules.Ocr;
using TiezhuToolbox.Modules.Recommend;
using System.Windows.Forms;

if (args.Contains("--config-smoke"))
{
    var testRoot = Path.Combine(Path.GetTempPath(), "TiezhuToolbox-config-test-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable("TIEZHU_TOOLBOX_USER_ROOT", testRoot);
    try
    {
        var database = HeroDatabase.Instance;
        var original = database.Profiles.First(profile => profile.AllowedSets.Count > 0);
        var custom = original.Clone();
        custom.UsefulStats.Clear();
        custom.AllowedSets.Clear();
        custom.NecklaceMainStats.Clear();
        custom.RingMainStats.Clear();
        custom.BootsMainStats.Clear();
        database.SaveOverride(custom);

        var overridePath = Path.Combine(testRoot, "hero-overrides.json");
        if (!File.Exists(overridePath))
            throw new InvalidOperationException("英雄覆盖文件未保存");
        database.Reload();
        var reloaded = database.GetProfile(original.Code)!;
        if (reloaded.UsefulStats.Count != 0 || reloaded.AllowedSets.Count != 0)
            throw new InvalidOperationException("英雄覆盖配置未在重载后生效");
        database.ResetOverride(original.Code);
        var reset = database.GetProfile(original.Code)!;
        if (reset.AllowedSets.Count == 0)
            throw new InvalidOperationException("恢复英雄默认配置失败");

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
                if (value != 31M || level88Value != 33M || loadedAddress.Text != "127.0.0.1:5555")
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
        Console.WriteLine($"配置持久化测试通过：{original.Name}（{original.Code}），软件设置 31/33/127.0.0.1:5555");
    }
    finally
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, recursive: true);
    }
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
            if (pages?.Count != 3)
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
            selectedIndex.SetValue(tabs, 2);
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
            CaptureTab("software-settings");
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
    Console.WriteLine($"界面冒烟测试通过：3 个页签，{HeroDatabase.Instance.Profiles.Count} 个英雄");
    return;
}

var screenshotsDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\bin\Release\net9.0-windows\win-x64\publish\screenshots";
var templateDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\Assets\Templates";

// 新旧两种分辨率的截图
var imageNames = args.Length > 0
    ? args
    : new[] { "MuMuNxDevice_20260717_031029.png", "MuMuNxDevice_20260717_041111.png" };

// 合成样例自检（无需截图）：
// 样例一：速度套 + 速度主属性鞋 + 副属性{防御,生命,速度,命中} → 调香师维波里丝(c5154) 应为 100%
// 样例二：暴击套 + 暴击率主属性项链 + 同样副属性 → c5154 主属性/套装均不符，不得出现
// 样例三：速度套速度鞋，副属性{生命,防御,速度,暴击率}但强化全跳暴击率 → c5154 应出现但匹配度大降（<50%）
if (args.Contains("--synthetic"))
{
    void Print(string title, EquipmentInfo info)
    {
        Console.WriteLine($"===== 合成样例: {title} =====");
        var recs = HeroRecommender.Recommend(info);
        foreach (var rec in recs)
            Console.WriteLine($"  {rec.Name}({rec.Code}) 匹配度 {rec.Score}%");
        Console.WriteLine(recs.Any(r => r.Code == "c5154") ? "  → 含 c5154" : "  → 不含 c5154");
        Console.WriteLine();
    }

    Print("速度套速度鞋 副属性{防御,生命,速度,命中}", new EquipmentInfo
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
            new SubStat { Name = "速度", Value = "8" },
            new SubStat { Name = "效果命中", Value = "7%" },
        },
    });

    Print("暴击套暴击项链 副属性{防御,生命,速度,命中}", new EquipmentInfo
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
    });

    Print("速度套速度鞋 副属性{生命,防御,速度,暴击率}强化全跳暴击", new EquipmentInfo
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
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "暴击率", Value = "20%" },
        },
    });

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
    EquipmentInfo CustomGear(string quality, string mainName, string mainValue) => new()
    {
        Quality = quality,
        SetName = "速度套装",
        MainStatName = mainName,
        MainStatValue = mainValue,
        SubStats = { new SubStat { Name = "生命值", Value = "8%" }, new SubStat { Name = "速度", Value = "8" } },
    };
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
    void PrintAdvice(string title, EquipmentInfo info, EnhanceAdvice? expected = null)
    {
        var r = EnhancementAdvisor.Analyze(info, 24, 24, 28);
        Console.WriteLine($"  [强化建议] {title} → {r.Text}（{r.Detail}）");
        if (expected != null && r.Advice != expected)
            throw new InvalidOperationException($"强化建议回归失败：期望 {expected}，实际 {r.Advice}");
    }

    Console.WriteLine("===== 强化建议样例（85级阈值 24/24，88级阈值 28） =====");
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

    // 装备 → 适用角色推荐（官方战绩传说分段数据）
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
