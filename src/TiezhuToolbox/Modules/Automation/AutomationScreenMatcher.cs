using OpenCvSharp;

namespace TiezhuToolbox.Modules.Automation;

/// <summary>自动强化流程中可确认的游戏界面。</summary>
public enum AutomationGameScreen
{
    Unknown,
    Backpack,
    EnhanceEquipment,
    AutoRegisterPopup,
    SellConfirmation,
    ExtractConfirmation,
    EnhancementRewardPopup,
}

/// <summary>内置的游戏 UI 图片模板。</summary>
public enum AutomationTemplate
{
    BackpackTitle,
    EnhanceTitle,
    BackpackEnhance,
    AutoRegister,
    Target15,
    Target12,
    Target9,
    Target6,
    Target3,
    ReadyEnhance,
    Sell,
    Extract,
    SellConfirmTitle,
    ExtractConfirmTitle,
    SellConfirmButton,
    ExtractConfirmButton,
    RewardTitle,
    RewardClose,
}

/// <summary>模板匹配结果；坐标已经换算到原始 ADB 截图尺寸。</summary>
public readonly record struct AutomationTemplateMatch(
    AutomationTemplate Template,
    Rectangle Bounds,
    double Confidence)
{
    public System.Drawing.Point Center => new(Bounds.Left + Bounds.Width / 2, Bounds.Top + Bounds.Height / 2);
    public bool IsMatch(double threshold = AutomationScreenMatcher.DefaultConfidenceThreshold)
        => Confidence >= threshold;
}

/// <summary>
/// 用用户提供的背包/强化界面截图制作的内置模板确认游戏 UI。
/// 所有输入先归一到 796×448，再只在预期区域内匹配，避免把装备图标误认为按钮。
/// </summary>
public sealed class AutomationScreenMatcher : IDisposable
{
    public const int ReferenceWidth = 796;
    public const int ReferenceHeight = 448;
    public const double DefaultConfidenceThreshold = 0.76;

    private sealed record TemplateDefinition(string Base64, Rect SearchRegion);

    private static readonly IReadOnlyDictionary<AutomationTemplate, TemplateDefinition> Definitions =
        new Dictionary<AutomationTemplate, TemplateDefinition>
        {
            [AutomationTemplate.BackpackTitle] = new(TemplateData.BackpackTitle, new Rect(0, 0, 150, 44)),
            [AutomationTemplate.EnhanceTitle] = new(TemplateData.EnhanceTitle, new Rect(0, 0, 170, 44)),
            [AutomationTemplate.BackpackEnhance] = new(TemplateData.BackpackEnhance, new Rect(620, 380, 176, 68)),
            [AutomationTemplate.AutoRegister] = new(TemplateData.AutoRegister, new Rect(535, 365, 261, 83)),
            // 五个阶段按钮文字和轮廓非常相似，必须限制在各自的纵向行内匹配。
            // 否则 +3 模板在画面特效或缩放变化时可能错误命中 +9 行。
            [AutomationTemplate.Target15] = new(TemplateData.Target15, new Rect(555, 185, 241, 69)),
            [AutomationTemplate.Target12] = new(TemplateData.Target12, new Rect(555, 220, 241, 69)),
            [AutomationTemplate.Target9] = new(TemplateData.Target9, new Rect(555, 255, 241, 70)),
            [AutomationTemplate.Target6] = new(TemplateData.Target6, new Rect(555, 290, 241, 70)),
            [AutomationTemplate.Target3] = new(TemplateData.Target3, new Rect(555, 325, 241, 75)),
            [AutomationTemplate.ReadyEnhance] = new(TemplateData.ReadyEnhance, new Rect(395, 365, 201, 83)),
            [AutomationTemplate.Sell] = new(TemplateData.Trash, new Rect(80, 365, 120, 83)),
            [AutomationTemplate.Extract] = new(TemplateData.Extract, new Rect(170, 365, 110, 83)),
            [AutomationTemplate.SellConfirmTitle] = new(TemplateData.SellConfirmTitle, new Rect(330, 80, 135, 70)),
            [AutomationTemplate.ExtractConfirmTitle] = new(TemplateData.ExtractConfirmTitle, new Rect(320, 80, 175, 70)),
            [AutomationTemplate.SellConfirmButton] = new(TemplateData.SellConfirmButton, new Rect(365, 285, 210, 95)),
            [AutomationTemplate.ExtractConfirmButton] = new(TemplateData.ExtractConfirmButton, new Rect(365, 285, 210, 95)),
            [AutomationTemplate.RewardTitle] = new(TemplateData.RewardTitle, new Rect(300, 65, 200, 100)),
            [AutomationTemplate.RewardClose] = new(TemplateData.RewardClose, new Rect(300, 275, 200, 120)),
        };

    private readonly Dictionary<AutomationTemplate, Mat> _templates = new();

    public AutomationScreenMatcher()
    {
        foreach (var (id, definition) in Definitions)
        {
            var bytes = Convert.FromBase64String(definition.Base64);
            var template = Cv2.ImDecode(bytes, ImreadModes.Grayscale);
            if (template.Empty())
                throw new InvalidOperationException($"无法载入自动强化 UI 模板：{id}");
            _templates[id] = template;
        }
    }

    public AutomationGameScreen DetectScreen(Bitmap screenshot, out double confidence)
    {
        var reward = Find(screenshot, AutomationTemplate.RewardTitle);
        if (reward.IsMatch())
        {
            confidence = reward.Confidence;
            return AutomationGameScreen.EnhancementRewardPopup;
        }

        // 确认弹窗会保留并压暗底层强化界面，因此必须先于普通页面判断。
        var sellConfirmation = Find(screenshot, AutomationTemplate.SellConfirmTitle);
        var extractConfirmation = Find(screenshot, AutomationTemplate.ExtractConfirmTitle);
        if (sellConfirmation.IsMatch() || extractConfirmation.IsMatch())
        {
            if (sellConfirmation.Confidence >= extractConfirmation.Confidence)
            {
                confidence = sellConfirmation.Confidence;
                return AutomationGameScreen.SellConfirmation;
            }

            confidence = extractConfirmation.Confidence;
            return AutomationGameScreen.ExtractConfirmation;
        }

        var backpack = Find(screenshot, AutomationTemplate.BackpackTitle);
        var enhance = Find(screenshot, AutomationTemplate.EnhanceTitle);
        var popupConfidence = new[]
        {
            AutomationTemplate.Target3,
            AutomationTemplate.Target6,
            AutomationTemplate.Target9,
            AutomationTemplate.Target12,
            AutomationTemplate.Target15,
        }.Max(template => Find(screenshot, template).Confidence);

        if (enhance.IsMatch() && popupConfidence >= DefaultConfidenceThreshold)
        {
            confidence = Math.Min(enhance.Confidence, popupConfidence);
            return AutomationGameScreen.AutoRegisterPopup;
        }

        if (enhance.IsMatch())
        {
            confidence = enhance.Confidence;
            return AutomationGameScreen.EnhanceEquipment;
        }

        if (backpack.IsMatch())
        {
            confidence = backpack.Confidence;
            return AutomationGameScreen.Backpack;
        }

        confidence = Math.Max(backpack.Confidence, enhance.Confidence);
        return AutomationGameScreen.Unknown;
    }

    public AutomationTemplateMatch Find(Bitmap screenshot, AutomationTemplate id)
    {
        ValidateAspectRatio(screenshot);
        using var source = DecodeBitmap(screenshot, ImreadModes.Grayscale);
        using var normalized = new Mat();
        Cv2.Resize(source, normalized, new OpenCvSharp.Size(ReferenceWidth, ReferenceHeight),
            interpolation: InterpolationFlags.Area);

        var definition = Definitions[id];
        var search = ClampRect(definition.SearchRegion, normalized.Width, normalized.Height);
        using var roi = new Mat(normalized, search);
        var template = _templates[id];
        if (roi.Width < template.Width || roi.Height < template.Height)
            return new AutomationTemplateMatch(id, Rectangle.Empty, 0);

        using var result = new Mat();
        Cv2.MatchTemplate(roi, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxValue, out _, out var maxLocation);

        var referenceBounds = new Rect(
            search.X + maxLocation.X,
            search.Y + maxLocation.Y,
            template.Width,
            template.Height);
        var scaleX = screenshot.Width / (double)ReferenceWidth;
        var scaleY = screenshot.Height / (double)ReferenceHeight;
        var actualBounds = new Rectangle(
            (int)Math.Round(referenceBounds.X * scaleX),
            (int)Math.Round(referenceBounds.Y * scaleY),
            Math.Max(1, (int)Math.Round(referenceBounds.Width * scaleX)),
            Math.Max(1, (int)Math.Round(referenceBounds.Height * scaleY)));
        return new AutomationTemplateMatch(id, actualBounds, maxValue);
    }

    /// <summary>检查第一个材料槽是否出现了高饱和度材料图标。</summary>
    public bool HasRegisteredMaterials(Bitmap screenshot)
    {
        ValidateAspectRatio(screenshot);
        using var source = DecodeBitmap(screenshot, ImreadModes.Color);
        using var normalized = new Mat();
        Cv2.Resize(source, normalized, new OpenCvSharp.Size(ReferenceWidth, ReferenceHeight),
            interpolation: InterpolationFlags.Area);

        // 原图第一个材料槽 (544,545)-(628,657)，这里按 1/2 缩放。
        var slot = new Rect(272, 272, 42, 57);
        using var roi = new Mat(normalized, slot);
        using var hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);
        using var mask = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 70, 110), new Scalar(179, 255, 255), mask);
        var coloredRatio = Cv2.CountNonZero(mask) / (double)(mask.Width * mask.Height);
        return coloredRatio >= 0.05;
    }

    public static AutomationTemplate TargetTemplateForLevel(int targetLevel) => targetLevel switch
    {
        3 => AutomationTemplate.Target3,
        6 => AutomationTemplate.Target6,
        9 => AutomationTemplate.Target9,
        12 => AutomationTemplate.Target12,
        15 => AutomationTemplate.Target15,
        _ => throw new ArgumentOutOfRangeException(nameof(targetLevel), targetLevel, "强化目标必须是 3/6/9/12/15"),
    };

    public static int? NextTargetLevel(int currentLevel) => currentLevel switch
    {
        < 3 => 3,
        < 6 => 6,
        < 9 => 9,
        < 12 => 12,
        < 15 => 15,
        _ => null,
    };

    private static Mat DecodeBitmap(Bitmap bitmap, ImreadModes mode)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Cv2.ImDecode(stream.ToArray(), mode);
    }

    private static void ValidateAspectRatio(Bitmap screenshot)
    {
        if (screenshot.Width <= 0 || screenshot.Height <= 0)
            throw new ArgumentException("截图尺寸无效", nameof(screenshot));

        var expected = ReferenceWidth / (double)ReferenceHeight;
        var actual = screenshot.Width / (double)screenshot.Height;
        if (Math.Abs(actual / expected - 1) > 0.035)
            throw new InvalidOperationException(
                $"游戏画面比例为 {screenshot.Width}×{screenshot.Height}，与模板的 16:9 比例不一致");
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        var right = Math.Clamp(rect.Right, x + 1, width);
        var bottom = Math.Clamp(rect.Bottom, y + 1, height);
        return new Rect(x, y, right - x, bottom - y);
    }

    public void Dispose()
    {
        foreach (var template in _templates.Values)
            template.Dispose();
        _templates.Clear();
    }

    private static class TemplateData
    {
        internal const string BackpackTitle = "iVBORw0KGgoAAAANSUhEUgAAACkAAAAaCAAAAAAP5SopAAACWUlEQVR42p2TT0hUURTGv3Pv+9PMe+rYbCRrErRSJipD09AgaOHKFhFUQhEotAgSKtNoVcsgCGoVUSK0qV2tKjcFtWhRtEjLtKwxUywnc0be+O59p8W8mclFMHQ391z48d1zv+8eclHmEiibZC6TNIAiSgD9deB/3E5EYDAAEgDAmtaS0iqWHMAHCL6SAEvbF0REJNaSgRaU2z+Y7Y+8lnWbfhLoUu/TX6yU0j7LQp8ABdFzHVo3JHInkzWde+eOfAsa+mJD300GU/bJlMUFEmw1bV8J4hle3NfpTc9vnVF98aA/vPb2mZImy/RAsqbmYJOIvr019ePOwLOeU+PXM4IJ/paLtRFPcEiKlY67FRVS6YWljb3SbnzUe9WqPi7APDb47mzoXKj5+1Wibk4ma9OJOGZHhnuWvdqoL/T6zscfZcFXx3Ecx7Vio5msYvZZB6lWbG4eTTVXb4he0Yd2Zh9WO67jukYYQDTpPWAmYr99T+LN/IKKn8/IXJuAG8n4RMW3g+HFu/LZxBSzaQWRowKsJ8bbMJap0iUSELlPBAC63gVYmXMnUqY+vavvMFmVCgCMQr+BeyCsFACQN/nZkNjRuvzhwtfhdVzSZDl9k4Fg1evuBkAsbNPWl+/FZo4NVYGKJAH2xDUAdlduVjLI9xIjK0TaD+zdmGQukARSi+lKCK/xRgKzX4hy97fVh18xPfzSZAAUzUuiZem9waCWdufFcwKCqrzjDF5iyQDICRtdJRMAryoYNgBoDu2DkY+zSIYDJQgchK2HKKOUOwAEa7bSdPF/THHZ5B9n4veaDtHnLgAAAABJRU5ErkJggg==";
        internal const string EnhanceTitle = "iVBORw0KGgoAAAANSUhEUgAAAEoAAAAaCAAAAAA1NZVaAAAEqklEQVR42q1Va2wUVRQ+587sTGdmt4uUhxVbwBaatAVFkEelSITlIfIoIGIMQaPUIgQtolZiIwQRBRQjFJOK8Q8iBCVYEOIDiChIhRB5CAVseRXcbbtb2u5jdufOPf6YbmgTRSR+/+499375zndPvos6dALCHUO+JZGzptujYrcq4n9Sym5bP3ZiRfynBpEBECEDACDhnCVggpzLDAQIBMBkryhsFwIAdWgeHdstCwBV0xGaggBgCRljiuIcNSEFGZeAS0ICIgCSPSGOQJJ0k0pyAQCK7MmDhvVo8o0bPHRIQbd6m6E1rvi0WhK5LgMwoIK+f8wvOdxsFr5zsRYBgVmvfnQ5fk/3nnK4w8vouq4b0hoiaiwNEBFRoJClGloVPTyBlqLHMBSlx6GG0Vv5qD591lF55oB03YtjghRpDF69cqzA5dHbIQMAkGv7n7Gp4zM8eyrVtnlFXQTG+z9wtf5BwRCA+jUHd65ebgZLBzO3KC121c6py17RtbGaY0F3FgXR+QWlo+s+PiZsWVEVTQUA4MMzDlxREJBFJ+0viW+oGDSIN58728QCZ2suhXMrR/Dup+YfMcJlp9Sbxuu6rhuu0cdq2qj0NyIiqnlIMjz7RTHMFuWy5LsefxK6PXHB9g8DeE08Cy4Yc5m2TK6xTpr+OUx3e4x2OMMghgy+dKb29911WfmHGy4ElGhJoRUDQM5nfdhz8bbpy/NNAXpJv+H4dB5W3kUfbOoVyBkAfqtPc9jWOqmSFvNpkndHuDVBZtuN9fKoqxR7hs22174Riy7UlQW/bpy0q3GVSa1NYR7y5a2qPB6jk+sPiHjdrpXTPHpHVSBJi6a2Dm1dO7Lo09oVedJTvU7n2EKynveeL6tS8bu2poRqFSnfnFAExUKlzwGAMGUravTt+3jN6FZGN1XJRU02kTiaWU4zM/0/aCNnvcVfmvsLRTblye50z+s2xWIWp2hLSygY2zX2vUQ8FIqTCIUi/JOxqe1ega7ruq55cvsts4lCEWoL2F+5XVDG64jofZdqpH9fkbvg5YqLFL9GX5TVU6UPssSJSePX2VsmTthM88BtGIbhdrtlRxu/YF2CHW2p/fOq03rsjaRGEPcdv/vNBu6NdB3YpaLJN6L3uaXhr3vmpB95+5qSynM2gJs9VgBe0JTkuLd7ZctWJn62W1q6rOq+gYWf2zbhvq3TSSXLSlX9i14AQQvVIdvmQs38K5plY0u1yEq7doryvbbolAyYeGSJ/1EsnpuRzdZKbM6BzYCYwkyxsBBFb+/pneeqy31LxocmBl7svf7HL08wueUnbg+t/5m65HcIG03TNF2aFog2HPRHGs98u3n1yht7DXjFnolpay4Hg8GGbTkow0ZK7PZp2pTtIXoXs05ybltxy+a8ZYrL3W47tg9YWrp5PcNu5FbcsmeIPSK7sKoZwcsAIGyqwHNnHDoY1chU7809X8ey7ycEACApUJ1wMpEwScVtlDkwRIYQgxTglsqQOACAzAiAJ5gqCWDC4i4ZuJVMVqZgMmOTVIhACAQABAwEIBI5+evsIiMiAEBAomQcI4ATDI4w7XYi/V9K+Def1x0xJRUy+N/wF4G9IWfXum87AAAAAElFTkSuQmCC";
        internal const string BackpackEnhance = "iVBORw0KGgoAAAANSUhEUgAAAFQAAAAfCAAAAABcn7WyAAAEPElEQVR42qWWT2hcVRTGv+/c++ZPMkmatEEaFQkWiSCUFHFhdSH4Zy26EUGwCF10oQhFxEUt4s6NmyoKCoIUEaGIpVj8AwouiouCC4VuJJUUQxNT00kyb947n4v73swktlDIYebx/tz3u9895zt3htMAAChG7DWKgumkQmXj7UzaE5Lsb3X76XQaADrTsSh8j1ALWbG+MVA6uc+7e0QCAHrNmbAOILSBiZmi59g7lCjQ8R5gQLbPc91xQnn7R3L1i6mGYFAn9O68Rgbj7blSbuNARDbWLykKhP6vY3hDAOvPLXDV7bIY3+hHj9k2QHBIYBohAkDpDC7ATHSPfbNdM41OTrFoZ3kE5FRCDUeKCSlOmW+3IpDfNJ+YXdrf7ZNw7kTWbwuQExGQQDGN4OCYtCv7aOHG1083exO/v7Z585VHXz/7zvcTW2iqFqZdkqVBRwkAIUrcmUnzd6ePPTL/4Z9HF2evTBwJmmuheHLjUqOighpZYs23SphUA3eGHzgyt3L1mWOLy2v5oaNfqPB/H39voRjtTiQ7cKg17lC2G0vPTtkfv/z2xOGvllrtl8evBPgLp89+PoaUc42+R0AgAUXcykj1HfPQOHf+5MGe7ntuZfn5XN3i5N2fnKFVPGK3wyUCBuw2M2lGkmbGWF568bPHfrjQPbM+sfrpyuHjncap9zv7ALAKI4feJUkCoZV1iqHbzIw0GgmShPmv37Xn/aHZe+69eHHt2bsennnrXPONxR/b1QCCZjQDWCXXYrdvaa1JmBkBMxqT2hCssX65+ObEt6tfrvbG2uGDl7asEx5cQAyDIM1owSytjxCiGMw0XELdWwRAim8uPrD08fz+EwdfveSNsrd2+njj0JkGTSmtUjK1atuaUREATIlCpCSPVIqXywvNcN6L7acO3PiJ/bfvd9v4uYPgEJMnve6lQck5OT7X8zoHI0JV1XArylSScYxlRJH3AI5BkAMQpHRIXAessbwRAQuQpQ2EhlosQRKGVlVROqLQbBFSKQkOhxJQklDpsJDaNKEIEIMyMl0E0GAEgYZASJQzyAW5oBRwQFLKKcCIUjGvm41JpCU7kTCYgdUj1usdDa81K21RGRwx5JvT7klZpdEII4LJiGSUZFoKhEtwweVwuFBKXk3jdLK1usUIrU62e0CSlgKpBUBjYCACmAoIgXLIXShdkMPkUpqJCmqWqx4juHV9rlEgQQysu5SGYGasMmCDjc0ll7u5Qy66SS45ZVIWrm4GRJDXs4Oew0AygEklaWSgWepCWnKaIDlKoXSX3E3uLlkpSo5G/OtvkRGgX/O5jpcymqUuNUMi0iwYR6HwEnIVkrvL5XKXBxdI5EvLsvS3R47x2amxaKgamQykMdACA1kboP51h0q5u8qEdIe7y/Ptf651U7WnAUiOdidWtkrf0Y1n95ZbGR6C4KguPO9uKpAE/gO0Kz3KF8X4IwAAAABJRU5ErkJggg==";
        internal const string AutoRegister = "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";
        internal const string Target15 = "iVBORw0KGgoAAAANSUhEUgAAAJIAAAAgCAAAAADcEn4sAAADOElEQVR42s1YTWgUdxT/vf/MziTZj9lN3GxSI9EEk7BRoaSpoBSkFFrITbCXit8UEVEQBMWzevCqB28ebFpKQkKLFAqWprSWlOZDbdQ1BjW6iWk2H7o7u9mdnXk9zBoinvJP2cw7DO+9ufz4ve8/AYAS1gXWQRgAgZf1/KINgAAY1XbGdMqJhUAAiFyDSg7Nr8y/Bgio01JZeEP80dwMgFCjgGdE2RyEEOE5h7yCiOy5CAk/ZUoJ5gFhZESVqDDhJWFTF6LgKUiwFBVrqX7iUh8BQ4DZdaBU3GCJjGBS1wSI3zY5AM5biMDK7+pFJblWxwBYq5vNQcT8hPRMZTw0myjEO/oyxLFNVZFx3XCmJ2RCoIKkKoMYaPxy25V/sPFMKKf9drPtmIkLHxzeqN2wK/c3zC/on07VL1w0iSUgyXDUWjUK3nNUM0JAtOXqY20RwSeDHzUcuH/9+MFvlOqBezNdj67t3hs0pViS4ejDmmEg8ufdkwRo2ZFXAJjqqr4u3k7+/JXea+9pz0V/tF7oXT2vV0+TBCQRgC4MZH+wmxQGNNoX/venSQC+3s72qdY7fwnnl5GWLzri/kJ+CWUJnG9vS9QXm/4uRT4XYiy1u/YyY6h5sqOSjcRIUN0VH1toG1oyCvny5JL16+Bnof58utQDHlx6aO480Whp+XA7luAIYntsPF0MdTd0JFCeXHKeYXtxDCCAAUqlgKQVsKpP4eXHaQAcrLA3RW7vOO+7c1+CJKn0Fjw+7XYmQeCdtcPZz5Vkc6ZvYmvkOZjR2iSe3Zt+cHaq2ykTS3AwUlIKNhA7tM8xvk/FrdEtRwaSEAKPJ3sGO8/VXK0//Xu/RMUpAUt+7uYnEzlM3J172j9g69bEltlbNgLJKcufSH+S/faPv1MbnmdW24krVNQF/6ftqzSaFAFoCghSYwEwatW14WB3nWcGF13TBlAAmMBEMquA5IxbefWsWEH43V9SgMCCFW+tcAqLfMBbkPwFYSoavHOhQFeywjJrwB7BRIxouigwp9d65WpiqlfmQYAaU9/kltYhSG7C0LKuVhjWK9v1+sPCXrdQ0bIqnEVz+b6BUKjMjzjvG8zulP4P7jo9J+NWojoAAAAASUVORK5CYII=";
        internal const string Target12 = "iVBORw0KGgoAAAANSUhEUgAAAJIAAAAgCAAAAADcEn4sAAADKUlEQVR42s2YXUgUURTH/+fMHV1nbXbVNFvCJKVayyIoyoh6KAgi6C2CIMoXnwp6LYIgooKIoJd66THEtCh66svoC8qKECsrK1PLrXQrd9fV3dl7etjt+6kxhjkvc+8ZuPPj3Pmfc+4lADDDRQSvTfIPkp+uzGcHAAEoCzuJlHjKQ6DCxxkAiEBAUamKfwEImGl+SsMfZlVOxgCEavLIPjAC19pgDo8KiT+QhPRomDiIFHxCBAiSZHEgBT+ZpIqZJ32FhKyhprJrBClIWQCGSCHLEEiECOJmbVJTEYj8mvP0rx6BOx4AUESuwyOBSGwcCNaaA3GUNtofn2Qal3aMkURqrLLnJSF59zLnBsmlMkiAui0Nh3owe8ccjp98Gt2WMPbWbJ9hnnZKtlaPjEbXDkQSB5Mu8gu7iRE1LCXC2gMLy21gXcWxQ8ZGWH0Xh2pbHu2rb7aM8K22M/zsSFup7SbjKVcxWlzxAAhef7KLQJ0XRnA/Cm1Egjucm4NXtqqO3Or52crzuXdqw9mv/w7lAoltChgVOnlJzzEEeA1Ur+wCQRttTQ1D0Tv3SHc+mruhaYnlpNMuBO0Cydw0t0pVDbeOkAkALFazdQWku+pjOiD2cPc0c9WCnnj9fSeUzZAnSNlrt9fb7dkE5WUusnnR0bfQRbq8ARPQTOI87h3PlrXPWv4M3ihOD2FE+gACBBBsXHfqAeCU73TeLhsDILalovblxj3qZo9XvzdLb5AgADGB1rTc6I1kP3Gioz9a3g8R1M923nR/eLV7uFU8ihI0uvOD3ISD4iZrwX4zdlh0T932a8NgxouBc10r9oaOz9x5t90bxX2vJYL3J14i035PgVPp2EPDvnQV6BrF57sfjJq+zkFeNs/66mLx6mn/qSM08pWJCVAGCuX43y1UpabGIQARABHJ5acagFMog+yq9Kop9d2C3yq+/PFKu1qTxfBXC6eEJ0v9hRTMcNIo8suZCSAU8zg7yekQv5zjBJUJhxE3q/1zjotwHASoGeZYekK83qIftwKFMUEF7ExM533BMGlPg/F7G08FB+e+jP9Msczk+TXOX1OdH30DWHI34sNd3fYAAAAASUVORK5CYII=";
        internal const string Target9 = "iVBORw0KGgoAAAANSUhEUgAAAJIAAAAgCAAAAADcEn4sAAAC+UlEQVR42s2Y30sUURTHv+fO7K92dV3x12pmKKlYGKIEiVmBoEKEgS+SRr1EUe+99RdE0FM99CKVjyUkFT4EhiaESiromuWDumnZ/tRZd8fdOT04Jvh4jXUOcxnuhRk+M/fL+Z5zCQBsPhsh28EAmXcAIDD0SBq7q/l5qS2Ns0hDAIhMJIAIBALsxxzRMEAgv7KRhDXCVaivA8g7QSBrEBFEhRdCeP8wsTWQmIyQVwg3J2ARIoCxRS7hTMBSkXAKkbIWki7UQ+4a7SYYggFTkQQwEVj2vaQePuEBYAbIHGzOpUMl2QRADMBR6V1bMZTTx5PToYJrY3PCyDvlKVhOlCEyKytSxbOjSz4qGO5bvU2Xd77l3/df+B6601g5H0dbt8cR7Sovbp0LSX2sU5VFIvW8EqNLXS9f5HR84cYh93Kn71Fp54/IOX1o0qh/8rlpdRVySEJWQ5mLZcy1S+/W3qAqk/ar7bUjwWHb7fJkzdXrNwK/4+H2apbThKS87blONSdfZ0UYNmcuSHd9cjWM1GwOasrsoK+76p5aHInJyltO2UU9+eUFzeOBlpuLLYVp2BYrMptudqiTEHW5KwsdwVCVM5xNJMbGgKtvakLfKm1rDq7tMLmobikFZtsOL7wX6fqJqbNfdUnrlNy4VBCJjSDQ/5pLH4bBPTkDFRoEDNVnr64c/tjXEf1A2fxLAEgZ/UnIObOe7NaW7BieEVeGARhF1a5YYMVe3vBcQ5aROD0GYl9vYVp9FvOLpdiD8DhAysZ8qv/k3aq34Y6aVwG5nZM3FGLG6uNaEViENqp5Z8eSWEgZxjRRSfzpDKZb3ZL2SSXa5v8oB3nPZASMf3YjE17HIW3XtHw2qRgwdgmZpEsM9XB1977l84HyQLoWYMGKtUo4hUXSYy0kT0poisMqPRNAcIqESG8WgK3SxzEK4xmBiOIXVunjRBkiIEApdsS3t7N+JmAO0N4FmzM3+cvYXXXlKcbRSogJAGWi29g/vFCO4jDnwMRMcn8Bg8EmIOv2RiYAAAAASUVORK5CYII=";
        internal const string Target6 = "iVBORw0KGgoAAAANSUhEUgAAAJIAAAAgCAAAAADcEn4sAAAC8klEQVR42s1YS2sTURg9381Mm0liHq3G1jam2voo+KgLXUhFEBRUUBAUKgqiC3f6E9y4EheKC11XcCdS1FpdiE/qImIwlhBQa2p8JE1LzGMmkzTzuZhWweWNpHPgDpdZXA73nHu+714CgLaQQlg2MED2tz5fB0AAVvqNis6t5UH2oMUJgQDV4ynMAQTqFrMmnAF3uP4DQChiU3UACCIahBD+OSZ2BiUmKx8QwgcdDmEEMCrkcflNA06C4hKi5ihGqAulSdUIAIMIFmxHEoiZCCy7LinN5xwAZoDswWDI8wEAhWQDwN6U7nVWOkvbeqvv8uFjrxPCCm32daaNXi7EK7KUmjixTLzrvIe/Xa2dLkYL5XMD625ksOfA54p61KxG8ynJbHH56jU5xYeVAnwXS5ef/vziHRr3fjvkvdJ1eHp+pzkRF1uuT+76npELcEV2l3hh+Pkn9Puvleglggs96v7A/ezTTWdvGhuF1Z3Ii9n9uVRLhWsLamogbESsA5t5YoxFVXum7Xi2qXy3pCbGgsc3XlR68gXIKafIOTs80rG2c/er9rWzDwZOpmfUj1HoGrcpcdDW0NfUweliRJtvpb0ZuVHtTCxWOZi7lVYiQ2laoQxOm2BW6zz1UGlsT8R2vq1L2ltSuFoWxvxP/CpWsGCosE5od/p1CLDa4R3qe/Tk1KH8Y9l0EdIZ+yJDlNSO9R7ekAKNX/qyZQpAIzQQ7XifKazZm6vJ5qX8iZsEaGZ8ZJ/79ZsVYqZyIf8GIFc+aY6uvxC9N3dk8PZUq6OSGIy7yb7cB931vOyPTZpI6pYVJ1r1414SH/a0yS7cVSn9j3aQl2gSWX/LjQQC7c2WXbvm8yKrxYpLYCbpFkNpsu/+42H+pz2Q7k1YWAocBZclTK+zKHlroizcTrkzAQRN6KJR7gQ75R7HWPmr4YIZ9OqWU4zUg1kQIFZrRcNo+ZsALd0m7DkBqtuvZ9n+5w4JXm7FQECjUF2iCZDA8j3l/JNxvwEh7ysWfH2fbAAAAABJRU5ErkJggg==";
        internal const string Target3 = "iVBORw0KGgoAAAANSUhEUgAAAJIAAAAgCAAAAADcEn4sAAADGUlEQVR42s1Y20sUURj/fWdm9qLr6rpqi6WUIWZCkVnRBYryJYiCxIqCIOihXnvotT+gp956M4gkyoe0K0U3JBClKMsuZpilqbuju+mso7sz8/Wwa1coOLts+zEz58x5mPmd7/ud70YA4A6oyAexphMACEB5UTxuci7/TambUvP0jDSvbyYCEKgS4STyQ1xL7HEGAlVpuP9fCFRdAqH4dRDnByQmnioWopBN5AkigBFHgXDHkVdiuIVI5BckS6iZWY0AMEAEJ81IAsBEYNnvkpqp8VMDA8TExKkVzmSfKsk6AGIAvjr/6LClrqsyn4ZDrd3PhRNc7QsOz1ch+tSQhSSvIMEcPFlnFl7tLDkUW6Ebx6uqYx+xrXnQwAFjfuXEO0nfoviSUvwmbbsSQ1lte0fFjm6l8Xbh+B7tbMXeEX1D8t6AVn+uZ+PEqBQijyqrJU5uTgzh8zkb/U0C9jJ3s++W/rj+6HmzllHZP63qzZNvc2o4T8DrCoRsndav2PlAD9Gc956v8eGq+OWY1t/lb204rYQmpySjgirH7OCRkurAxmfXXdua1DCgva92mW7W1AHQmuCnwd1vYhXer7mkNyPS5j3W12c5c22Xthz8EKbSgrrhBMBakl91eay1g31bexOS9BZyO0lEPpmxSJQxoz9RltrcsrynxADB0YL+Tfvcdw6fmbhLOXYCpDwaJ6zd3Du9yzMmcONFUctNAE5pjWes70txRX27dFiQPnFWL4itxuakp/NdOY0mTkz0gEnorxMXa09Vdkztb7jwEjn1S6lgFu4ZGrh23yZrxGXdmAOiQyz08RrlSvfwMxGdlPNLFIrPZiMf5MWBwED6KSPF7gzD7mLIT0NJXaDFd0kuZZR3/wj5/Ft+IJ2bsOD8KOF+kNsR877sFxqZKL5wQRjkzWrNRETyoAgFZArbCIIp6wqSAkWMshlbwYK/yHSyWFlnsj+t0omAAFpSMDuXnZ4ALeqH+W9n7teWwPeegOr1G5PpZXdAdbKlpLTFnH/5gV/SBGJiEOzoAr53A0hkEZLqOP9QE/4A/JMr+wYIuDS5Tfz6hgAAAABJRU5ErkJggg==";
        internal const string ReadyEnhance = "iVBORw0KGgoAAAANSUhEUgAAAFIAAAAoCAAAAABI/vLgAAAEV0lEQVR42q1WTWhcZRQ9537fTCYzMY0xVdO/IBa0GKULKYK0UsV/KxSxWKk7lf7suu7KhdviQq1QKGh3SkFQRKiIqAt1owitVpEk1VqqoU3idJKZefe4eD+ZmSQidC4zw5v3vne+c889977HSdxwUACsvTDfIADE6zeEJgCEADIMD8//5QSi3zBJpT9JK4xUfk8IQ99C7Xpl3NBPSED12jpXfyHVGmF/WULtWO0vS8Ct5v2GVOxz4oADce2eACEWxqPSLlnTmUVEril1ohBbDjACQDPK45q7d4JH690kvxhutsWrYxUxmQXBDXNxcDbrvv+O2LVJx8bNAwdx/pMXtrTpb787cH338cOb9+9jaCWl/wO5athvn44/eMfw6enSK4+/Z8lDQzPbxiw0bh2etk5vrwbJ1Tg6Eb/78YHtKk14dfjP0L5t1/c/PdMOi+tPXDkS1C1iL2w0rqYjrPH064ONk3ffu3fm14+s+chd79OsPv7G9iNmOTf1FibLb1UhSeIWHnquct9o+6aN2ysTh6CI5M6T9x/9rEwzFgF2ByRbI2gY2vnEPjvjZ2eeWv/spgVfCpvPjL76IQAwXRMCwZwpSQJk6ksV/PMjgWHq290T+PpCMrW0bfDs1NEtr72EL976ZevIuc4aCgJdzOc7jOAklzVRp+IMm49vrMfhhdr5w9MTp26/Wr20Ny4cfPLF0ClfOtIzdQVU5y+a0cyMZJFydsy5xsDp54/VT5xlvVIpf/XwqYFywMi6ZXGCkVYIkSevaL0ucoEgW5M7Nmwa2rNj6OVqZf87Vm7PtbYeuz6451ywrrUGSCAQIIkEYux1QQAgWmv0AL/cVZv9+Mrf9zz6weLl2ZFvPn8M9vObDCrWGiTAmZqeFGjgjpW9DwhQqHFxgK3FxdbY6B8ar88llarYqId8ASEIAiQJogSgem0qhswC2QgDofSq5mFLoNXYvBh1ibHkc5BVMpxsOEoQTJ65RaRiKeeonGteyHJexlhKVJIQS3A4ZYBSkoDgIGQQmNU913LF0FKurvJXlHSZZQNZggfBEQQPElWUp7QmpDqQ1bGFgPR2E2SSQEhK9TIWkKvMfgko8tPyHEurIQCeQkmpnA4iAHGg+9muntZQVgfvGJBS/nU55BIkAnAQbXazVDcW4XnW6iBeHLgjgZskuQzmstDs0LIQU92fZUhlmubekeRM65LQIZkC5hnL+YjsGKwQQM8LnrFaHhCen3Q3yiUXlcgBlOcaFssdbwddSma6pnVQnnPeLBCgtsnc5ZScicktXhbiALIxupxm18xSTlKAPHe3py3pcnc3d2diAGuz14hYWWHI9DYKggrZMvPkm0iSHEGJJyZPzKkEQ/ULrYBcyx4fpb1QQAGCp2l4ejoBlAQX3ZSQHpyhdOWHhQjEsRUMiyIrTxNQhgTl1OVC4u6eyFsuNZcWpmfaEQB3dk3L4uEjgJl9i/+FM7vGUCoBmv/MtsxE4F+BrsPs/+DAmwAAAABJRU5ErkJggg==";
        internal const string Trash = "iVBORw0KGgoAAAANSUhEUgAAACkAAAAoCAAAAABGV4yPAAAB3ElEQVR42s1Uy47TQBCs6plEcXBeBAgsrAQSX8AFiSPc+WGEhIS48AHLcQULioiyysaO7cSPaQ5hV7Fj4xwpaS4zNTXd1d1DAzjb7wpqoVS4NHYC0ACTgUZZPZNCMZ7eBIQFz2QRK5pBGU57C1IemysQrH8cAAhnz4OV6U+ulPgXCCnyh6HcDwpBC9RsirGYGIpWKsKeFDucAEYUbYnyzlh7/PbfhKubtponYAlocXvlDrZ0WRXovh46E3zdVoVLmuo/6riO59T1Xu6YLSLWM+lmb88tdKsAXwny7x+XovsgVLWk+cyfAxAo4QD642W9pvLi+oWhKvcru5xT6+NkMnmDsN/NEq+TJn66/CEHTCl5Zjo3Hxbe9Zff/fnnlS33g1QqEV8GjH5FsvkZVbyv9hENIVYgli3MvSdHBapjNuI/YyrgVKEtGRHsiIoRpTWVs1I1dZvP3j9xT989yJ/7syItMc0oPGDubM/LgrSbBnk3+nZxQDUDW6rl+tOIt7Up1klzz4NxXBmqpjkCWZnRQ03q0QjXualC0VN8Nyrs45RPZOQk9k+RtN5GVr17ru37gk6y0GhxlmRojkABxXg6L2h0MN1t4rwpZ9J4Q1kkpIHaSb+oN4BKkAjXjvgDl0PEef/sk0MAAAAASUVORK5CYII=";
        internal const string Extract = "iVBORw0KGgoAAAANSUhEUgAAAC8AAAAvCAAAAABWTMxwAAADSElEQVR42o1VXW8bVRA9M3e96/VXbMdOXAeQGtQgNRUgJGiREKrEAxLisQ/8RSQkEFKlFIlW5K1EFdBKlL6Q0IQSO/6IndixvXvv8LDeeL32pp23nXtm5uycuXPJAgCAEDGaeQTzNoVDGLGIAE8yHzbFi5h4gbCiAESXXwHe2GmbY3wuTcR4I396TBYgqpzzh3o+f8iAmMEunXUv8WLXdGtkkGAEEGWr5j8T5t8YNcBREjE+AAy94zcIgGKz4h4xLeaVwMIM/bXJhCAMKp0uQCMdnIaR7hcFAEtahrQsIO4ZKgsAi/ImtIRM3MMXPhuBBTJCCX2ZqxagrMXiJAIQz2gFMyEzfSkaQhqOaG0A5ikqcm7Fu006++FKpjg5aPR7w6DM/HzOT7Lo6+8f/FGqFmu3zWn35HlLSXyeo8Lq1Pbbvx7L+SE4675159Ot7/q0dP4D06VbduNObueE0yk7nz6m+mqPJREvW3eNu2or9ay6Xs65jQf37+Xj/GfKsP/R1ylNMsbmloim33f66JcX76NABAQYa8sdO74P0j6J/fR7L+V5FUgyf+rula+tkYAMerueJTivxubk8r4T2O/csn/MXKtvHx7Rl38eK8Po1GMCcGTccXTxxTf+Xw/vD57IaA+AD6GUSeIjOHhRuen80Mp98lQ9a7KY7dulcX48mwYBFDn2WbhqrMJe5d31eu0Yz1/6bN67t+6k/m6HChcGhhChJzQajn4z11d29tYHAxLC4QC9FhL1IuMVO2b3kaf99AigF/sbX+13VLK+aN2s/bTLPHFKr1iIJ5lXjymhPwCEmtUPJGtIt0sQ0vzZ5sM2zesV+V+AdCNzozscI1/81/byd80vAzWDFgaGYvNGJ9+ubn581jaF3Ode9p8npJL2eUhPN5tuLb9WXs2mH73kxcUSwwsxRvuwi5WfW+AlW8NaXEzE4jWbYJKleFnYKAIikriXwn4u3VaSsL+Y/JSNNzDjsAbANGZXXg8nZIwmgGG6RQAUWgJeuHAa8KceqvK6CsZsTAYMgBTEqV+0J1dFEKUr1lHwfilAVCWjz3QClomUy+edICEpACJOJpXIHCLehRe+vyp0Xtkdmn/fIw8+rlbPCqUTvJH9Dxpdbk/pAmcRAAAAAElFTkSuQmCC";
        internal const string SellConfirmTitle = "iVBORw0KGgoAAAANSUhEUgAAADAAAAAZCAAAAABSyvKlAAADEElEQVR42l2US2hdVRSGv3+dfW+S+0wqkYi22tpiJFFx4CAqKk5EoaJEtASpSrEgjqriSDoQJ4JURCfqQDNQkCIithRrBuITB4LWQQelrY9CabWYXNPk3vPYy8E594Lu0dprr/3vvR7/rw4gAHAA1//M0bYMDK7qxEuH+wjAhOPR5AKvvPYfhCzrl0gmgCyNxLoKIVShJGMgiDFKpHe80DgplBdRyNLdT5+6OLd8+3cb5kgVbLvd7nTHb/7g9ZnWFG/7j5Otyeady09OdCYbN5z03dyz5gdqrWaz2Rhvt9qdTkCg/m1Lg/f+riuJZnjxyN5rDkP64OzmU/c119sHZgHXkZUABACJOMDdMRy8FqNr86b9TDwMsHU/AJePy50gd0V3k0wGmMncIOsc3Ln2zrri0uyR7wOodywAhLKagkHfYoH3UxsUyGsvPuTNPYX8ShbmhCcnPkUOoay6kdx/y9j6DqYWc8t2xqT/6PNItQSMUBduW7vnkRNAjtZoHALguuWqMcXpn+9uvLVhxb75la+CK/nldHCE2shVtF66NxFxZrL/uwNrb3zcmmx+suOy8PEkzR1sbc8PY16NBtSOf/RnsN6rj51ZSoV31+vpb9fXihOFxRuv+OO88HD2XEJVVtng8ZdfO1i39R6DM2mSXvXutie+HUfZ0U2L3amfvgkOG90LDgQBbjO17Z0ibCaorhBnbm3Mflm3OP4KAIuLAHy4b8whIFwqPI+xiA6xMPd0Isp6veyL3KppVnrhiA1fEEgWlCRCNQ8hMYFlvTA/pITybOVYy5HCkCeDv5APyC8VsBrdle/aTnPIoNhsPHC4L3eFqs9x9tlom3M+/Uxh2dU1JXioXVr6J7gDyere5woBxABAEW1hAYBtb5aIF6WULYfi8EfTlmcMywr22V27MoHJI6CN97+e0K9Hl+YZrdXPN8aQow7Iyae3RDQi7uAcRtG+VmV+wtU/m5mXFwRueaYyWi5cdTkqsqF6CFcdHKTuSEAADSUjVryqeCzksTRDKUMlvLt8KDel4vgoB5VI/i+PpVnEnGt9qwAAAABJRU5ErkJggg==";
        internal const string ExtractConfirmTitle = "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";
        internal const string SellConfirmButton = "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";
        internal const string ExtractConfirmButton = "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";
        internal const string RewardTitle = "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";
        internal const string RewardClose = "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";
    }
}
