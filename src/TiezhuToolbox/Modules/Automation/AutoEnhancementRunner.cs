using TiezhuToolbox.Modules.Ocr;
using TiezhuToolbox.Modules.Recommend;

namespace TiezhuToolbox.Modules.Automation;

public enum AutoEnhancementLogLevel
{
    Info,
    Action,
    Recognition,
    Warning,
    Error,
    Success,
}

public enum EquipmentDisposalMethod
{
    Sell,
    Extract,
}

public sealed record AutoEnhancementProgress(
    AutoEnhancementLogLevel Level,
    string Message,
    int Processed,
    int Enhanced,
    int Sold,
    int Extracted);

public sealed record ReforgeEquipmentSummary(
    string SetName,
    string Part,
    IReadOnlyList<string> SubStats);

public sealed record AutoEnhancementSummary(
    int Processed,
    int Enhanced,
    int Sold,
    int Extracted,
    IReadOnlyList<ReforgeEquipmentSummary> ReforgeEquipment);

public sealed record AutoEnhancementOptions(
    int MaxEquipment,
    double LeftThreshold,
    double RightThreshold,
    double Level88Threshold,
    double MinimumDemandMatchScore,
    EquipmentDisposalMethod DisposalMethod,
    bool StopOnValuableEquipment,
    bool HeroicOnlyGambleSpeed,
    bool SpeedSetRequiresSpeed,
    bool CriticalNecklaceMainStatRule,
    TimeSpan UiTimeout,
    TimeSpan AnimationMinimumWait)
{
    public static AutoEnhancementOptions CreateDefault(
        int maxEquipment,
        double leftThreshold,
        double rightThreshold,
        double level88Threshold,
        double minimumDemandMatchScore = EnhancementAdvisor.DefaultMinimumDemandMatchScore,
        EquipmentDisposalMethod disposalMethod = EquipmentDisposalMethod.Sell,
        bool stopOnValuableEquipment = true,
        bool heroicOnlyGambleSpeed = false,
        bool speedSetRequiresSpeed = true,
        bool criticalNecklaceMainStatRule = true)
        => new(
            Math.Clamp(maxEquipment, 1, 999),
            leftThreshold,
            rightThreshold,
            level88Threshold,
            Math.Clamp(minimumDemandMatchScore, 0, 100),
            disposalMethod,
            stopOnValuableEquipment,
            heroicOnlyGambleSpeed,
            speedSetRequiresSpeed,
            criticalNecklaceMainStatRule,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(4));
}

public sealed record AutoEnhancementResult(
    AutoEnhancementSummary Summary,
    bool StoppedForValuableEquipment,
    string Message)
{
    public int Processed => Summary.Processed;
    public int Enhanced => Summary.Enhanced;
    public int Sold => Summary.Sold;
    public int Extracted => Summary.Extracted;
    public IReadOnlyList<ReforgeEquipmentSummary> ReforgeEquipment => Summary.ReforgeEquipment;
}

/// <summary>
/// 自动强化闭环：图片确认界面与按钮 → OCR 判断 → 单次 ADB 点击 → 再截图确认。
/// 任一界面、按钮或 OCR 结果不确定都会抛错停机，绝不按固定坐标继续盲点。
/// </summary>
public sealed class AutoEnhancementRunner : IDisposable
{
    private readonly string _serial;
    private readonly AutoEnhancementOptions _options;
    private readonly IProgress<AutoEnhancementProgress>? _progress;
    private readonly AutomationScreenMatcher _matcher = new();
    private readonly OcrEngine _ocrEngine;

    private int _processed;
    private int _enhanced;
    private int _sold;
    private int _extracted;
    private readonly List<ReforgeEquipmentSummary> _reforgeEquipment = new();

    public AutoEnhancementRunner(
        string serial,
        string ocrTemplateDirectory,
        AutoEnhancementOptions options,
        IProgress<AutoEnhancementProgress>? progress = null)
    {
        _serial = serial;
        _options = options;
        _progress = progress;
        _ocrEngine = new OcrEngine(ocrTemplateDirectory);
    }

    public async Task<AutoEnhancementResult> RunAsync(CancellationToken cancellationToken)
    {
        Report(AutoEnhancementLogLevel.Info,
            $"自动强化已启动，设备 {_serial}，本次最多处理 {_options.MaxEquipment} 件装备，" +
            $"淘汰装备处理方式：{DisposalDisplayName}，紫装规则：{(_options.HeroicOnlyGambleSpeed ? "只赌速度" : "按常规评分")}，" +
            $"速度套速度规则：{(_options.SpeedSetRequiresSpeed ? "开启" : "关闭")}，暴击项链规则：{(_options.CriticalNecklaceMainStatRule ? "开启" : "关闭")}，" +
            $"符合保留条件后：{(_options.StopOnValuableEquipment ? "停止" : "返回背包继续")}");

        while (_processed < _options.MaxEquipment)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(AutoEnhancementLogLevel.Info, $"准备处理第 {_processed + 1} 件装备");
            await EnterEnhancementScreenAsync(cancellationToken);

            int? expectedEnhanceLevel = null;
            var currentEquipmentEnhanced = false;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var screenshot = await CaptureAsync(cancellationToken);
                var screen = _matcher.DetectScreen(screenshot, out var screenConfidence);
                if (screen != AutomationGameScreen.EnhanceEquipment)
                    throw new InvalidOperationException(
                        $"OCR 前界面确认失败：期望“强化装备”，实际 {DescribeScreen(screen)}（{screenConfidence:P1}）");

                var path = ScreenshotHelper.SaveBitmap(
                    screenshot,
                    $"auto_{_processed + 1:000}_stage_{DateTime.Now:HHmmssfff}");
                Report(AutoEnhancementLogLevel.Info, $"已保存判定截图：{path}");

                var info = await _ocrEngine.RecognizeAsync(path);
                cancellationToken.ThrowIfCancellationRequested();
                EnsureValidEquipmentInfo(info);
                if (expectedEnhanceLevel is int expected && info.EnhanceLevel < expected)
                {
                    throw new InvalidOperationException(
                        $"强化结果校验失败：期望至少 +{expected}，OCR 识别为 +{info.EnhanceLevel}");
                }
                expectedEnhanceLevel = null;

                Report(AutoEnhancementLogLevel.Recognition, DescribeEquipment(info));
                var advice = EnhancementAdvisor.Analyze(
                    info,
                    _options.LeftThreshold,
                    _options.RightThreshold,
                    _options.Level88Threshold,
                    _options.MinimumDemandMatchScore,
                    _options.HeroicOnlyGambleSpeed,
                    _options.SpeedSetRequiresSpeed,
                    _options.CriticalNecklaceMainStatRule);
                Report(AutoEnhancementLogLevel.Recognition,
                    $"强化判断：{advice.Text}；{advice.Detail}");

                if (advice.Advice is EnhanceAdvice.Continue or EnhanceAdvice.GambleSpeed)
                {
                    var targetLevel = AutomationScreenMatcher.NextTargetLevel(info.EnhanceLevel);
                    if (targetLevel == null)
                    {
                        return FinishForValuableEquipment(
                            $"装备已达到 +{info.EnhanceLevel}，没有更高的自动强化档位");
                    }

                    await EnhanceToTargetAsync(screenshot, targetLevel.Value, cancellationToken);
                    expectedEnhanceLevel = targetLevel.Value;
                    if (!currentEquipmentEnhanced)
                    {
                        currentEquipmentEnhanced = true;
                        _enhanced++;
                    }
                    Report(AutoEnhancementLogLevel.Success,
                        $"+{targetLevel} 强化动画结束，开始重新识别并判断");
                    continue;
                }

                if (advice.Advice is EnhanceAdvice.GiveUp or EnhanceAdvice.GiveUpFixedMain)
                {
                    await DisposeRejectedEquipmentAsync(screenshot, cancellationToken);
                    _processed++;
                    if (_options.DisposalMethod == EquipmentDisposalMethod.Sell)
                        _sold++;
                    else
                        _extracted++;
                    Report(AutoEnhancementLogLevel.Success,
                        $"第 {_processed} 件装备已{DisposalDisplayName}，游戏已返回背包并选择下一件装备");
                    break;
                }

                if (advice.Advice is EnhanceAdvice.Keep or EnhanceAdvice.Reforge)
                {
                    if (advice.Advice == EnhanceAdvice.Reforge)
                        AddReforgeEquipment(info);

                    if (_options.StopOnValuableEquipment)
                    {
                        return FinishForValuableEquipment(
                            $"检测到值得保留的 +{info.EnhanceLevel} 装备：{advice.Text}。已安全停止，未执行{DisposalDisplayName}");
                    }

                    await ReturnToBackpackAndSelectFirstEquipmentAsync(cancellationToken);
                    _processed++;
                    Report(AutoEnhancementLogLevel.Success,
                        $"第 {_processed} 件装备符合保留条件，已保留并选中背包左上角第一件装备，继续流程");
                    break;
                }

                throw new InvalidOperationException(
                    $"强化建议为“{advice.Text}”，无法安全决定强化或{DisposalDisplayName}：{advice.Detail}");
            }
        }

        var message = $"已达到本次上限 {_options.MaxEquipment} 件，自动强化结束";
        Report(AutoEnhancementLogLevel.Success, message);
        return new AutoEnhancementResult(GetSummary(), false, message);
    }

    private async Task EnterEnhancementScreenAsync(CancellationToken cancellationToken)
    {
        using var screenshot = await CaptureAsync(cancellationToken);
        var screen = _matcher.DetectScreen(screenshot, out var confidence);
        switch (screen)
        {
            case AutomationGameScreen.EnhanceEquipment:
                Report(AutoEnhancementLogLevel.Info,
                    $"当前已在强化装备界面（图片置信度 {confidence:P1}）");
                return;

            case AutomationGameScreen.Backpack:
                Report(AutoEnhancementLogLevel.Info,
                    $"已确认背包界面（图片置信度 {confidence:P1}）");
                await ClickTemplateAsync(screenshot, AutomationTemplate.BackpackEnhance,
                    "背包右下角“强化”", cancellationToken);
                using (await WaitForScreenAsync(
                           AutomationGameScreen.EnhanceEquipment, _options.UiTimeout, cancellationToken))
                {
                    Report(AutoEnhancementLogLevel.Success, "已进入强化装备界面");
                }
                return;

            case AutomationGameScreen.AutoRegisterPopup:
                throw new InvalidOperationException("检测到自动登记弹窗，请先手动关闭弹窗后再开始");

            default:
                throw new InvalidOperationException(
                    $"无法确认当前游戏界面（最佳图片置信度 {confidence:P1}），请回到背包装备列表后重试");
        }
    }

    private async Task EnhanceToTargetAsync(
        Bitmap enhancementScreenshot,
        int targetLevel,
        CancellationToken cancellationToken)
    {
        await ClickTemplateAsync(enhancementScreenshot, AutomationTemplate.AutoRegister,
            "右下角“自动登记”", cancellationToken);

        using var popup = await WaitForScreenAsync(
            AutomationGameScreen.AutoRegisterPopup, _options.UiTimeout, cancellationToken);
        Report(AutoEnhancementLogLevel.Success, "已确认强化等级选择弹窗");

        var targetTemplate = AutomationScreenMatcher.TargetTemplateForLevel(targetLevel);
        await ClickTemplateAsync(popup, targetTemplate, $"+{targetLevel} 阶段", cancellationToken);
        using var registered = await WaitForRegisteredMaterialsAsync(_options.UiTimeout, cancellationToken);
        Report(AutoEnhancementLogLevel.Success, $"游戏已自动放置 +{targetLevel} 所需强化材料");

        await ClickTemplateAsync(registered, AutomationTemplate.ReadyEnhance,
            "绿色“强化”", cancellationToken);
        Report(AutoEnhancementLogLevel.Action,
            $"已点击强化，至少等待 {_options.AnimationMinimumWait.TotalSeconds:0.#} 秒动画");
        await Task.Delay(_options.AnimationMinimumWait, cancellationToken);

        using var completed = await WaitForAnimationCompletionAsync(_options.UiTimeout, cancellationToken);
    }

    private async Task DisposeRejectedEquipmentAsync(Bitmap screenshot, CancellationToken cancellationToken)
    {
        var isSell = _options.DisposalMethod == EquipmentDisposalMethod.Sell;
        var actionTemplate = isSell ? AutomationTemplate.Sell : AutomationTemplate.Extract;
        var confirmationScreen = isSell
            ? AutomationGameScreen.SellConfirmation
            : AutomationGameScreen.ExtractConfirmation;
        var confirmationButton = isSell
            ? AutomationTemplate.SellConfirmButton
            : AutomationTemplate.ExtractConfirmButton;
        var iconName = isSell ? "左下角垃圾桶（出售）" : "左下角方块图标（分解/萃取）";

        Report(AutoEnhancementLogLevel.Warning,
            $"当前装备不值得继续，准备{DisposalDisplayName}并自动完成二次确认");
        await ClickTemplateAsync(screenshot, actionTemplate, iconName, cancellationToken);

        using var confirmation = await WaitForScreenAsync(
            confirmationScreen, _options.UiTimeout, cancellationToken);
        Report(AutoEnhancementLogLevel.Success,
            $"已确认{DisposalDisplayName}弹窗，准备点击右侧确认按钮");
        await ClickTemplateAsync(confirmation, confirmationButton,
            isSell ? "出售弹窗右侧“确认”" : "分解弹窗右侧“萃取”", cancellationToken);

        using var backpack = await WaitForScreenAsync(
            AutomationGameScreen.Backpack, _options.UiTimeout, cancellationToken);
    }

    private async Task ReturnToBackpackAndSelectFirstEquipmentAsync(CancellationToken cancellationToken)
    {
        Report(AutoEnhancementLogLevel.Action,
            "当前装备符合保留条件，设置为继续运行：发送安卓返回键");
        await Task.Run(() => AdbHelper.PressBack(_serial), cancellationToken);

        using var backpack = await WaitForScreenAsync(
            AutomationGameScreen.Backpack, _options.UiTimeout, cancellationToken);
        var x = (int)Math.Round(backpack.Width * 115D / AutomationScreenMatcher.ReferenceWidth);
        var y = (int)Math.Round(backpack.Height * 130D / AutomationScreenMatcher.ReferenceHeight);
        Report(AutoEnhancementLogLevel.Action,
            $"已确认返回背包，点击左上角第一件装备 ({x}, {y})");
        await Task.Run(() => AdbHelper.Tap(_serial, x, y), cancellationToken);
        await Task.Delay(350, cancellationToken);

        using var selected = await CaptureAsync(cancellationToken);
        var screen = _matcher.DetectScreen(selected, out var confidence);
        if (screen != AutomationGameScreen.Backpack)
        {
            throw new InvalidOperationException(
                $"点击背包左上角第一件装备后界面校验失败：实际 {DescribeScreen(screen)}（{confidence:P1}）");
        }
    }

    private async Task ClickTemplateAsync(
        Bitmap screenshot,
        AutomationTemplate template,
        string displayName,
        CancellationToken cancellationToken)
    {
        var match = _matcher.Find(screenshot, template);
        if (!match.IsMatch())
        {
            throw new InvalidOperationException(
                $"未找到{displayName}按钮（图片置信度 {match.Confidence:P1}，要求 {AutomationScreenMatcher.DefaultConfidenceThreshold:P0}）");
        }

        Report(AutoEnhancementLogLevel.Action,
            $"图片确认 {displayName}（{match.Confidence:P1}），点击 ({match.Center.X}, {match.Center.Y})");
        await Task.Run(() => AdbHelper.Tap(_serial, match.Center.X, match.Center.Y), cancellationToken);
    }

    private async Task<Bitmap> WaitForScreenAsync(
        AutomationGameScreen expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var lastScreen = AutomationGameScreen.Unknown;
        var lastConfidence = 0.0;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screenshot = await CaptureAsync(cancellationToken);
            lastScreen = _matcher.DetectScreen(screenshot, out lastConfidence);
            if (lastScreen == expected)
                return screenshot;
            screenshot.Dispose();
            await Task.Delay(350, cancellationToken);
        }

        throw new TimeoutException(
            $"等待{DescribeScreen(expected)}超时；最后检测到 {DescribeScreen(lastScreen)}（{lastConfidence:P1}）");
    }

    private async Task<Bitmap> WaitForRegisteredMaterialsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screenshot = await CaptureAsync(cancellationToken);
            var screen = _matcher.DetectScreen(screenshot, out _);
            if (screen == AutomationGameScreen.EnhanceEquipment
                && _matcher.HasRegisteredMaterials(screenshot))
            {
                return screenshot;
            }
            screenshot.Dispose();
            await Task.Delay(350, cancellationToken);
        }

        throw new TimeoutException("选择强化等级后未检测到已登记的强化材料，可能是材料不足");
    }

    private async Task<Bitmap> WaitForAnimationCompletionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var screenshot = await CaptureAsync(cancellationToken);
            var screen = _matcher.DetectScreen(screenshot, out _);
            if (screen == AutomationGameScreen.EnhancementRewardPopup)
            {
                Report(AutoEnhancementLogLevel.Warning,
                    "检测到强化暴击后的经验溢出奖励弹窗，准备点击关闭");
                try
                {
                    await ClickTemplateAsync(screenshot, AutomationTemplate.RewardClose,
                        "奖励弹窗“点击关闭”", cancellationToken);
                }
                finally
                {
                    screenshot.Dispose();
                }

                Report(AutoEnhancementLogLevel.Success,
                    "已关闭经验溢出奖励弹窗，继续等待强化界面恢复");
                await Task.Delay(350, cancellationToken);
                continue;
            }

            var register = _matcher.Find(screenshot, AutomationTemplate.AutoRegister);
            if (screen == AutomationGameScreen.EnhanceEquipment
                && register.IsMatch()
                && !_matcher.HasRegisteredMaterials(screenshot))
            {
                return screenshot;
            }
            screenshot.Dispose();
            await Task.Delay(450, cancellationToken);
        }

        throw new TimeoutException("等待强化动画结束超时，未重新检测到可操作的强化界面");
    }

    private async Task<Bitmap> CaptureAsync(CancellationToken cancellationToken)
        => await Task.Run(() => AdbHelper.ScreenshotPng(_serial), cancellationToken);

    private AutoEnhancementResult FinishForValuableEquipment(string message)
    {
        _processed++;
        Report(AutoEnhancementLogLevel.Success, message);
        return new AutoEnhancementResult(GetSummary(), true, message);
    }

    public AutoEnhancementSummary GetSummary()
        => new(
            _processed,
            _enhanced,
            _sold,
            _extracted,
            _reforgeEquipment.ToArray());

    private void AddReforgeEquipment(EquipmentInfo info)
    {
        var part = EquipmentRules.DetectPart(info.Quality) switch
        {
            EquipmentPart.Weapon => "武器",
            EquipmentPart.Helm => "头盔",
            EquipmentPart.Armor => "铠甲",
            EquipmentPart.Necklace => "项链",
            EquipmentPart.Ring => "戒指",
            EquipmentPart.Boots => "鞋子",
            _ => "未知部位",
        };
        var subStats = info.SubStats
            .Select(stat => $"{stat.Name}{stat.Value}")
            .ToArray();
        _reforgeEquipment.Add(new ReforgeEquipmentSummary(info.SetName, part, subStats));
    }

    private static void EnsureValidEquipmentInfo(EquipmentInfo info)
    {
        if (info.Level is <= 0 or > 100
            || (info.EnhanceLevel != 0 && info.EnhanceLevel is not (3 or 6 or 9 or 12 or 15))
            || string.IsNullOrWhiteSpace(info.Quality)
            || string.IsNullOrWhiteSpace(info.MainStatName)
            || string.IsNullOrWhiteSpace(info.MainStatValue)
            || string.IsNullOrWhiteSpace(info.SetName)
            || info.SubStats.Count is < 1 or > 4
            || !double.IsFinite(info.Score)
            || info.Score <= 0
            || info.RawText.Contains("[OCR 失败:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"装备 OCR 结果不完整，已停止：等级 {info.Level}，+{info.EnhanceLevel}，品质“{info.Quality}”，主属性“{info.MainStatName} {info.MainStatValue}”，副属性 {info.SubStats.Count} 条，套装“{info.SetName}”");
        }
    }

    private static string DescribeEquipment(EquipmentInfo info)
    {
        var subStats = string.Join("，", info.SubStats.Select(stat => $"{stat.Name}{stat.Value}"));
        return $"OCR：{info.Level}级 +{info.EnhanceLevel} {info.Quality}，{info.SetName}，主属性 {info.MainStatName}{info.MainStatValue}，副属性 [{subStats}]，民间分 {info.Score:0.##}";
    }

    private static string DescribeScreen(AutomationGameScreen screen) => screen switch
    {
        AutomationGameScreen.Backpack => "背包界面",
        AutomationGameScreen.EnhanceEquipment => "强化装备界面",
        AutomationGameScreen.AutoRegisterPopup => "强化等级选择弹窗",
        AutomationGameScreen.SellConfirmation => "出售确认弹窗",
        AutomationGameScreen.ExtractConfirmation => "分解确认弹窗",
        AutomationGameScreen.EnhancementRewardPopup => "强化经验溢出奖励弹窗",
        _ => "未知界面",
    };

    private void Report(AutoEnhancementLogLevel level, string message)
        => _progress?.Report(new AutoEnhancementProgress(level, message, _processed, _enhanced, _sold, _extracted));

    private string DisposalDisplayName
        => _options.DisposalMethod == EquipmentDisposalMethod.Sell ? "出售" : "分解";

    public void Dispose()
    {
        _ocrEngine.Dispose();
        _matcher.Dispose();
    }
}
