using OpenCvSharp;
using System.Text.RegularExpressions;

namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// OCR 引擎：识别装备文本信息。
/// OCR 输入只保留左侧装备信息区域，并按属性、品质和套装文本各自定位。
/// </summary>
public class OcrEngine : IDisposable
{
    private static readonly string[] QualityWords = { "传说", "史诗", "英雄", "稀有", "良好", "普通", "一般" };
    private static readonly string[] TypeWords = { "武器", "头盔", "护甲", "铠甲", "项链", "戒指", "鞋子", "靴" };

    // 注意：长的放前面，避免"暴击伤害"被"暴击率"抢先模糊匹配
    private static readonly string[] StatKeywords = { "暴击伤害", "效果命中", "效果抗性", "攻击力", "生命值", "防御力", "暴击率", "速度" };

    // 行尾数值：5 / 8% / 16%(+8%) / 16% (+8%) / 50(+11)
    private static readonly Regex TrailingValueRegex = new(
        @"(\d+(?:\.\d+)?%?\s*(?:[（(]\s*\+\d+(?:\.\d+)?%?\s*[）)])?)\s*$",
        RegexOptions.Compiled);

    private readonly DigitTemplateMatcher _digitMatcher;
    private readonly string _paddleModelDir;
    private PaddleOcrEngine? _paddle;
    private string? _debugImagePath;

    private sealed record OcrWord(string Text, int X, int Y, int W, int H);

    private sealed class OcrLine
    {
        public List<OcrWord> Words { get; } = new();
        public int X { get; private set; }
        public int Y { get; private set; }
        public int W { get; private set; }
        public int H { get; private set; }
        public string Joined => string.Concat(Words.Select(w => w.Text));

        public void Recalc()
        {
            X = Words.Min(w => w.X);
            Y = Words.Min(w => w.Y);
            W = Words.Max(w => w.X + w.W) - X;
            H = Words.Max(w => w.Y + w.H) - Y;
        }
    }

    public OcrEngine(string templateDir)
    {
        _digitMatcher = new DigitTemplateMatcher(templateDir);
        _paddleModelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "PaddleOCR");
    }

    /// <summary>
    /// 从装备截图中识别装备信息。
    /// </summary>
    public Task<EquipmentInfo> RecognizeAsync(string imagePath)
        => Task.Run(() => Recognize(imagePath));

    private EquipmentInfo Recognize(string imagePath)
    {
        _debugImagePath = imagePath;
        using var mat = Cv2.ImRead(imagePath);
        if (mat.Empty())
            throw new ArgumentException("无法读取图片", nameof(imagePath));

        var info = new EquipmentInfo();

        try
        {
            // 预处理：只把左侧装备信息区域送入 OCR，排除顶部导航、中间强化区和右侧材料区。
            var panelRect = ImagePreprocessor.GetEquipmentPanelRect(mat);
            using var panel = ImagePreprocessor.Crop(mat, panelRect);

            // PaddleOCR 检测 + 识别面板文本（模型缺失等异常由外层 catch 记录到 RawText）
            var words = GetPaddle().Run(panel)
                .Select(tb => new OcrWord(
                    tb.Text,
                    tb.Box.X + panelRect.X,
                    tb.Box.Y + panelRect.Y,
                    tb.Box.Width,
                    tb.Box.Height))
                .ToList();

            var lines = GroupLines(words);
            info.RawText += string.Join(Environment.NewLine, lines.Select(l => l.Joined));
            info.RawText += $"{Environment.NewLine}[debug] panel=({panelRect.X},{panelRect.Y},{panelRect.Width},{panelRect.Height})";

            OcrLine? qualityLine = null;
            Rect? iconZone = null;
            var qualityTextRect = GetQualityTextRect(panelRect);

            // 属性行独立定位，不再检测或依赖游戏内的“装备分数”文本。
            var statLines = lines
                .Where(l => TrailingValueRegex.IsMatch(l.Joined)
                         && !l.Joined.Contains('/')
                         && MatchStatKeyword(l.Joined) != null)
                .OrderBy(l => l.Y)
                .ToList();

            var mainDone = false;
            foreach (var line in statLines)
            {
                var stat = MatchStatKeyword(line.Joined);
                if (stat == null)
                    continue;

                var value = TrailingValueRegex.Match(line.Joined).Groups[1].Value.Replace(" ", "");
                if (!mainDone)
                {
                    info.MainStatName = stat;
                    info.MainStatValue = value;
                    mainDone = true;
                }
                else
                {
                    var rollMatch = Regex.Match(line.Joined, @"[（(]\s*(\d+)\s*[）)]");
                    var sub = new SubStat
                    {
                        Name = stat,
                        RollCount = rollMatch.Success && int.TryParse(rollMatch.Groups[1].Value, out var rolls)
                            ? Math.Clamp(rolls, 0, 5)
                            : 0,
                    };
                    var enhanced = Regex.Match(value, @"^([^（(]+)[（(]([^）)]+)[）)]$");
                    if (enhanced.Success)
                    {
                        sub.Value = enhanced.Groups[1].Value;
                        sub.EnhanceValue = enhanced.Groups[2].Value;
                    }
                    else
                    {
                        sub.Value = value;
                    }
                    info.SubStats.Add(sub);
                }
            }

            // 套装行独立定位：位于属性块之后且带 n/m 件数，不依赖“装备分数”行。
            var lastStatBottom = statLines.Count > 0 ? statLines[^1].Y + statLines[^1].H : 0;
            var setLine = lines
                .Where(l => l.Y >= lastStatBottom && Regex.IsMatch(l.Joined, @"\d+\s*/\s*\d+"))
                .OrderBy(l => l.Y)
                .FirstOrDefault();
            if (setLine != null)
            {
                var setMatch = Regex.Match(setLine.Joined, @"([一-龥]{2,4})(?:套装|于装|讲装|讨装)");
                if (setMatch.Success)
                    info.SetName = setMatch.Groups[1].Value + "套装";
            }

            // 品质文本固定在装备图标右侧。先限制候选框位置，避免图标、等级数字与品质行拼接；
            // 如果整面板检测仍把图标和文字合成一个大框，则单独裁剪固定文字区重做一次 OCR。
            var firstStatY = statLines.Count > 0 ? statLines[0].Y : int.MaxValue;
            qualityLine = lines
                .Where(l => l.X >= qualityTextRect.X
                         && l.Y >= qualityTextRect.Y
                         && l.Y + l.H <= qualityTextRect.Bottom
                         && l.Y < firstStatY
                         && !string.IsNullOrEmpty(ExtractQuality(l.Joined)))
                .OrderByDescending(l => l.Y)
                .FirstOrDefault();
            qualityLine ??= RecognizeQualityInFixedRegion(mat, qualityTextRect, info);
            // 固定区域偶发检测失败时保留旧路径兜底，避免品质字段拖累其他识别结果。
            qualityLine ??= lines
                .Where(l => l.Y < firstStatY && !string.IsNullOrEmpty(ExtractQuality(l.Joined)))
                .OrderByDescending(l => l.Y)
                .FirstOrDefault();
            if (qualityLine != null)
                info.Quality = ExtractQuality(qualityLine.Joined);

            // 副属性后的 (n) 计数比右上角十几像素高的美术字徽章稳定，优先用它推导强化等级。
            // 传说装备初始有 4 条副属性，每次强化都会增加一次计数；英雄装备初始只有 3 条，
            // +12 时新增第四条不显示强化计数，所以四词条时需要额外补上这一次强化。
            var totalRolls = info.SubStats.Sum(s => s.RollCount);
            var enhanceByRolls = InferEnhanceLevelByRolls(info.Quality, info.SubStats.Count, totalRolls);
            if (enhanceByRolls is int inferredLevel)
            {
                info.EnhanceLevel = inferredLevel;
                info.RawText += $"{Environment.NewLine}[debug] enhance-by-rolls={totalRolls}, substats={info.SubStats.Count} -> +{info.EnhanceLevel}";
            }

            // 装备分数仅按副属性的民间算法计算，不读取截图中的游戏分数。
            info.Score = EquipmentScoreCalculator.Calculate(info.SubStats);

            // 小图标区域：品质行左侧到第一条属性上方，含等级和强化徽章。
            if (qualityLine != null)
            {
                var zoneY = Math.Max(panelRect.Y, qualityLine.Y - 3 * qualityLine.H);
                var zoneBottom = Math.Min(
                    panelRect.Bottom,
                    statLines.Count > 0 ? statLines[0].Y : qualityLine.Y + 6 * qualityLine.H);
                var zoneRight = Math.Min(panelRect.Right, qualityLine.X + 2 * qualityLine.H);
                if (zoneRight > panelRect.X && zoneBottom > zoneY)
                {
                    iconZone = new Rect(panelRect.X, zoneY, zoneRight - panelRect.X, zoneBottom - zoneY);
                    RecognizeLevels(mat, iconZone.Value, qualityLine, info);
                }
            }

            SaveDebugImage(imagePath, mat, panelRect, qualityTextRect, lines, iconZone);
        }
        catch (Exception ex)
        {
            info.RawText += $"{Environment.NewLine}[OCR 失败: {ex.Message}]";
        }

        return info;
    }

    /// <summary>
    /// 识别装备等级（图标左上角数字）和强化等级（右上角橙色徽章）。
    /// </summary>
    private void RecognizeLevels(Mat mat, Rect zone, OcrLine textAnchor, EquipmentInfo info)
    {
        using var zoneMat = ImagePreprocessor.Crop(mat, zone);
        if (zoneMat.Empty())
            return;

        // 方案一：PaddleOCR 直接检测图标区内的 "88" / "+3" 文本
        try
        {
            var boxes = GetPaddle().Run(zoneMat);
            foreach (var tb in boxes)
                info.RawText += $"{Environment.NewLine}[debug] zonebox '{tb.Text}' ({tb.Box.X},{tb.Box.Y},{tb.Box.Width},{tb.Box.Height})";

            var levelBox = boxes
                .Where(t => Regex.IsMatch(t.Text.Trim(), @"^\d{1,3}$"))
                .OrderBy(t => t.Text.Trim().Length == 2 ? 0 : 1)
                .ThenBy(t => t.Box.Y)
                .ThenBy(t => t.Box.X)
                .FirstOrDefault();
            if (levelBox != null && int.TryParse(levelBox.Text.Trim(), out var lvl2))
                info.Level = lvl2;

            var enhBox = boxes.FirstOrDefault(t => Regex.IsMatch(t.Text.Trim(), @"^\+\d{1,2}$"));
            if (info.EnhanceLevel == 0
                && enhBox != null
                && int.TryParse(enhBox.Text.Trim().TrimStart('+'), out var enh2)
                && IsValidEnhanceLevel(enh2))
                info.EnhanceLevel = enh2;
        }
        catch { /* 忽略，走徽章定位 */ }

        // 方案二：定位橙色徽章（在品质行上方），裁剪后做数字 OCR
        var anchorCenterY = textAnchor.Y - zone.Y + textAnchor.H / 2;
        var badge = FindOrangeBadge(zoneMat, anchorCenterY);
        var badgeText = string.Empty;
        if (info.EnhanceLevel == 0 && badge is Rect b)
        {
            using var badgeMat = ImagePreprocessor.Crop(zoneMat, b);

            // PaddleOCR 读徽章数字，读不出走下方模板兜底
            try
            {
                badgeText = GetPaddle().RecognizeLine(badgeMat);
            }
            catch { /* 忽略，走模板兜底 */ }

            var m = Regex.Match(badgeText, @"\+?\s*(\d{1,2})");
            if (m.Success
                && int.TryParse(m.Groups[1].Value, out var enh)
                && IsValidEnhanceLevel(enh))
                info.EnhanceLevel = enh;

            if (info.EnhanceLevel == 0)
            {
                var (v, conf) = _digitMatcher.RecognizeEnhanceLevel(badgeMat);
                if (conf > 0.5 && IsValidEnhanceLevel(v))
                    info.EnhanceLevel = v;
            }
        }
        info.RawText += $"{Environment.NewLine}[debug] zone=({zone.X},{zone.Y},{zone.Width},{zone.Height}) badge={(badge?.ToString() ?? "null")} badgeText='{badgeText}'";

        // 装备等级"88"：图标左上角绶带上的金色文字。
        // 有徽章（已强化）时取徽章同一水平带、徽章左侧；
        // 无徽章（未强化）时没有定位锚点，取图标区上条带（尺寸随区域按比例换算）。
        if (info.Level == 0)
        {
            Rect stripRect;
            if (badge is Rect bd)
            {
                stripRect = new Rect(
                    0,
                    Math.Max(0, bd.Y - 10),
                    Math.Max(0, bd.X - 8),
                    Math.Min(zoneMat.Height - Math.Max(0, bd.Y - 10), bd.Height + 35));
            }
            else
            {
                var bandY = zoneMat.Height / 6;
                stripRect = new Rect(
                    0,
                    bandY,
                    zoneMat.Width / 2,
                    Math.Min(zoneMat.Height - bandY, zoneMat.Height / 2));
            }
            using var strip = ImagePreprocessor.Crop(zoneMat, stripRect);
            if (!strip.Empty())
            {
                // "88"是金色文字：按颜色提取掩码，排除深色背景和紫色图标图案
                using var goldMask = new Mat();
                Cv2.InRange(strip, new Scalar(0, 120, 150), new Scalar(140, 220, 255), goldMask);

                // 连通域过滤：数字笔画是饱满的块，边框是细长线（高填充率/超大），剔除
                Cv2.FindContours(goldMask, out var goldContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                var digitContours = new List<(Rect r, double area)>();
                foreach (var c in goldContours)
                {
                    var r = Cv2.BoundingRect(c);
                    if (r.Height < 6 || r.Height > 30 || r.Width < 2 || r.Width > 25)
                        continue;
                    var area = Cv2.ContourArea(c);
                    var fill = area / (r.Width * (double)r.Height);
                    if (fill > 0.75)
                        continue; // 实心细条 = 图标边框
                    if (fill < 0.15)
                        continue; // 稀疏碎屑 = 绶带花纹噪点
                    digitContours.Add((r, area));
                }

                var digitRects = new List<Rect>();
                if (digitContours.Count > 0)
                {
                    // 数字共享同一基线且笔画饱满：以像素质量最大（而非外接框最大）的连通域为基准
                    // （绶带花纹噪点常外接框更大但像素少），剔除偏离基线的碎块，再取与基准相邻的连续段
                    var main = digitContours.OrderByDescending(d => d.area).First().r;
                    var mainCenterY = main.Y + main.Height / 2.0;
                    var cluster = digitContours
                        .Select(d => d.r)
                        .Where(r => r.Height >= main.Height * 0.6
                                 && Math.Abs(r.Y + r.Height / 2.0 - mainCenterY) <= main.Height * 0.5)
                        .OrderBy(r => r.X)
                        .ToList();

                    var mainIdx = cluster.FindIndex(r => r.Equals(main));
                    var kept = new List<Rect> { main };
                    for (var i = mainIdx - 1; i >= 0; i--)
                    {
                        var prevRight = kept.First().X;
                        if (prevRight - cluster[i].Right > main.Width * 1.5)
                            break;
                        kept.Insert(0, cluster[i]);
                    }
                    for (var i = mainIdx + 1; i < cluster.Count; i++)
                    {
                        var lastRight = kept.Last().Right;
                        if (cluster[i].X - lastRight > main.Width * 1.5)
                            break;
                        kept.Add(cluster[i]);
                    }
                    digitRects = kept;
                }

                if (digitRects.Count > 0)
                {
                    var x1 = Math.Max(0, digitRects.Min(r => r.X) - 3);
                    var y1 = Math.Max(0, digitRects.Min(r => r.Y) - 3);
                    var x2 = Math.Min(strip.Width, digitRects.Max(r => r.Right) + 3);
                    var y2 = Math.Min(strip.Height, digitRects.Max(r => r.Bottom) + 3);
                    using var digitCrop = ImagePreprocessor.Crop(goldMask, new Rect(x1, y1, x2 - x1, y2 - y1));
                    try
                    {
                        var dbgDir = System.IO.Path.GetDirectoryName(_debugImagePath) ?? ".";
                        var dbgName = System.IO.Path.GetFileNameWithoutExtension(_debugImagePath ?? "debug");
                        Cv2.ImWrite(System.IO.Path.Combine(dbgDir, $"{dbgName}_level.png"), digitCrop);
                    }
                    catch { }

                    // 反转为黑字白底并加白边，供整行 OCR
                    using var digitInv = new Mat();
                    Cv2.BitwiseNot(digitCrop, digitInv);
                    using var digitPadded = new Mat();
                    Cv2.CopyMakeBorder(digitInv, digitPadded, 10, 10, 14, 14, BorderTypes.Constant, Scalar.White);
                    using var digitBig = new Mat();
                    Cv2.Resize(digitPadded, digitBig, new OpenCvSharp.Size(digitPadded.Width * 4, digitPadded.Height * 4), interpolation: InterpolationFlags.Cubic);

                    // 方案 A：与标准数字掩码模板（85_mask/88_mask）做相关匹配，多模板竞争取最优
                    var (maskLabel, maskConf) = _digitMatcher.MatchBinaryCropBest(digitCrop);
                    info.RawText += $"{Environment.NewLine}[debug] strip mask best='{maskLabel}' conf={maskConf:F3}";
                    if (maskConf >= 0.65
                        && int.TryParse(maskLabel.Replace("_mask", ""), out var maskLevel)
                        && maskLevel is >= 1 and <= 99)
                        info.Level = maskLevel;

                    // 方案 B：PaddleOCR 直读原始彩色数字条。
                    // 等级是图标上的金色美术字，二值化掩码会丢笔画特征（"85" 被读成 "5"），
                    // 实测彩色原图识别最稳定。裁剪比 digitCrop 稍大以保留上下文。
                    if (info.Level == 0)
                    {
                        var cx1 = Math.Max(0, digitRects.Min(r => r.X) - 8);
                        var cy1 = Math.Max(0, digitRects.Min(r => r.Y) - 6);
                        var cx2 = Math.Min(strip.Width, digitRects.Max(r => r.Right) + 12);
                        var cy2 = Math.Min(strip.Height, digitRects.Max(r => r.Bottom) + 10);
                        using var stripColor = ImagePreprocessor.Crop(strip, new Rect(cx1, cy1, cx2 - cx1, cy2 - cy1));
                        try
                        {
                            var colorText = GetPaddle().RecognizeLine(stripColor);
                            info.RawText += $"{Environment.NewLine}[debug] strip paddle-color='{colorText}'";
                            var cm = Regex.Match(colorText.Trim(), @"^(\d{1,3})\D{0,2}$");
                            if (cm.Success && int.TryParse(cm.Groups[1].Value, out var clvl) && clvl is >= 1 and <= 99)
                                info.Level = clvl;
                        }
                        catch { /* 忽略 */ }
                    }

                    // 方案 C：PaddleOCR 整行识别（二值掩码放大图）
                    if (info.Level == 0)
                    {
                        try
                        {
                            var paddleText = GetPaddle().RecognizeLine(digitBig);
                            info.RawText += $"{Environment.NewLine}[debug] strip paddle='{paddleText}'";
                            var pm = Regex.Match(paddleText.Trim(), @"^\d{1,3}$");
                            if (pm.Success && int.TryParse(pm.Value, out var plvl))
                                info.Level = plvl;
                        }
                        catch { /* 忽略 */ }
                    }
                }
            }
        }

        // 方案三：模板兜底（当前只有 88 模板）
        if (info.Level != 0)
            return;

        using var topLeft = ImagePreprocessor.Crop(zoneMat, new Rect(0, 0, zoneMat.Width / 2, zoneMat.Height / 2));
        var (tv, tconf) = _digitMatcher.RecognizeLevel(topLeft);
        if (tconf > 0.5)
            info.Level = tv;
    }

    /// <summary>
    /// 在图标区域中查找橙色强化徽章，返回区域坐标系下的矩形。
    /// </summary>
    private static Rect? FindOrangeBadge(Mat zoneBgr, int anchorCenterY)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(zoneBgr, hsv, ColorConversionCodes.BGR2HSV);
        using var mask = new Mat();
        // 橙色徽章：色相 8~22，高饱和高亮
        Cv2.InRange(hsv, new Scalar(8, 120, 120), new Scalar(22, 255, 255), mask);

        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        Rect? best = null;
        double bestArea = 0;
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 80)
                continue;
            var r = Cv2.BoundingRect(contour);
            var aspect = r.Width / (double)Math.Max(1, r.Height);
            if (aspect < 0.8 || aspect > 2.2)
                continue;
            // 徽章在品质行上方，排除图标底部的橙色六边形标记
            if (r.Y + r.Height / 2 >= anchorCenterY)
                continue;
            if (area > bestArea)
            {
                bestArea = area;
                best = r;
            }
        }

        if (best is not Rect br)
            return null;

        var x = Math.Max(0, br.X - 2);
        var y = Math.Max(0, br.Y - 2);
        return new Rect(x, y,
            Math.Min(zoneBgr.Width - x, br.Width + 4),
            Math.Min(zoneBgr.Height - y, br.Height + 4));
    }

    private PaddleOcrEngine GetPaddle()
    {
        _paddle ??= new PaddleOcrEngine(_paddleModelDir);
        return _paddle;
    }

    private static bool IsValidEnhanceLevel(int level)
        => level is 3 or 6 or 9 or 12 or 15;

    /// <summary>
    /// 根据品质、当前副属性条数和已显示的强化次数推导强化等级。
    /// 返回 null 表示信息组合不足以可靠推导，应继续识别右上角强化徽章。
    /// </summary>
    private static int? InferEnhanceLevelByRolls(string quality, int subStatCount, int totalRolls)
    {
        if (quality.StartsWith("传说", StringComparison.Ordinal)
            && subStatCount == 4
            && totalRolls is >= 1 and <= 5)
        {
            return totalRolls * 3;
        }

        if (!quality.StartsWith("英雄", StringComparison.Ordinal))
            return null;

        // 英雄装备 +0～+9 始终只有三条副属性，每 3 级产生一次可见强化计数。
        if (subStatCount == 3 && totalRolls is >= 1 and <= 3)
            return totalRolls * 3;

        // +12 新增的第四条没有 (n)；+15 才会再产生一次可见强化计数。
        if (subStatCount == 4 && totalRolls is >= 3 and <= 4)
            return (totalRolls + 1) * 3;

        return null;
    }

    private static List<OcrLine> GroupLines(List<OcrWord> words)
    {
        var lines = new List<OcrLine>();
        foreach (var w in words.OrderBy(w => w.Y).ThenBy(w => w.X))
        {
            var line = lines.FirstOrDefault(l =>
                Math.Abs((l.Y + l.H / 2.0) - (w.Y + w.H / 2.0)) <= Math.Max(8, l.H * 0.6));
            if (line == null)
            {
                line = new OcrLine();
                lines.Add(line);
            }
            line.Words.Add(w);
            line.Recalc();
        }

        foreach (var l in lines)
            l.Words.Sort((a, b) => a.X.CompareTo(b.X));

        return lines.OrderBy(l => l.Y).ToList();
    }

    /// <summary>品质与装备名称的固定文字区（装备图标右侧）。</summary>
    private static Rect GetQualityTextRect(Rect panelRect)
    {
        var left = panelRect.X + (int)Math.Round(panelRect.Width * 0.335);
        var top = panelRect.Y + (int)Math.Round(panelRect.Height * 0.035);
        var bottom = panelRect.Y + (int)Math.Round(panelRect.Height * 0.19);
        return new Rect(left, top, panelRect.Right - left, Math.Max(1, bottom - top));
    }

    /// <summary>整面板 OCR 被图标干扰时，只识别固定的品质/名称文字区。</summary>
    private OcrLine? RecognizeQualityInFixedRegion(Mat mat, Rect region, EquipmentInfo info)
    {
        using var crop = ImagePreprocessor.Crop(mat, region);
        if (crop.Empty())
            return null;

        try
        {
            var words = GetPaddle().Run(crop)
                .Select(tb => new OcrWord(
                    tb.Text,
                    tb.Box.X + region.X,
                    tb.Box.Y + region.Y,
                    tb.Box.Width,
                    tb.Box.Height))
                .ToList();

            foreach (var word in words)
                info.RawText += $"{Environment.NewLine}[debug] qualitybox '{word.Text}' ({word.X},{word.Y},{word.W},{word.H})";

            return GroupLines(words)
                .Where(l => !string.IsNullOrEmpty(ExtractQuality(l.Joined)))
                .OrderBy(l => l.Y)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            info.RawText += $"{Environment.NewLine}[debug] quality-region-failed: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// 模糊匹配属性关键字：长度≥3 允许错 1 字，长度 2 要求全对。
    /// </summary>
    private static string? MatchStatKeyword(string text)
    {
        foreach (var keyword in StatKeywords)
        {
            var maxMiss = keyword.Length >= 3 ? 1 : 0;
            for (var i = 0; i + keyword.Length <= text.Length; i++)
            {
                var miss = 0;
                for (var j = 0; j < keyword.Length; j++)
                {
                    if (text[i + j] != keyword[j])
                        miss++;
                }
                if (miss <= maxMiss)
                    return keyword;
            }
        }
        return null;
    }

    private static string ExtractQuality(string text)
    {
        // 品质行左侧偶尔会把图标边缘误识别成一个汉字（如“公传说戒指”）。
        // 只容忍行首最多两个噪声字符，仍可避免匹配装备名中的“英雄戒指”等字样。
        var m = Regex.Match(text, $@"^.{{0,2}}?({string.Join("|", QualityWords)})({string.Join("|", TypeWords)})");
        return m.Success ? m.Groups[1].Value + m.Groups[2].Value : string.Empty;
    }

    private static void SaveDebugImage(
        string imagePath,
        Mat mat,
        Rect panelRect,
        Rect qualityTextRect,
        List<OcrLine> lines,
        Rect? iconZone)
    {
        try
        {
            using var debug = mat.Clone();
            Cv2.Rectangle(debug, panelRect, new Scalar(0, 255, 255), 2);
            Cv2.Rectangle(debug, qualityTextRect, new Scalar(255, 0, 255), 2);
            foreach (var l in lines)
                Cv2.Rectangle(debug, new Rect(l.X, l.Y, l.W, l.H), new Scalar(0, 255, 0), 1);
            if (iconZone is Rect z)
                Cv2.Rectangle(debug, z, new Scalar(255, 0, 0), 2);

            var dir = Path.GetDirectoryName(imagePath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(imagePath);
            Cv2.ImWrite(Path.Combine(dir, $"{name}_debug.png"), debug);
        }
        catch
        {
            // 调试图保存失败不影响识别
        }
    }

    public void Dispose()
    {
        _paddle?.Dispose();
        _digitMatcher.Dispose();
    }
}
