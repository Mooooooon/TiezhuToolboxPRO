using OpenCvSharp;
using System.Text.RegularExpressions;

namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// OCR 引擎：识别装备文本信息。
/// 使用 PaddleOCR 词坐标 + 结构锚点（装备分数行）定位，不依赖固定分辨率坐标。
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
            // 装备信息面板在窗口左侧（约占宽度 45%）
            var panelRect = new Rect(0, 0, (int)(mat.Width * 0.45), mat.Height);
            using var panel = ImagePreprocessor.Crop(mat, panelRect);

            // PaddleOCR 检测 + 识别面板文本（模型缺失等异常由外层 catch 记录到 RawText）
            var words = GetPaddle().Run(panel)
                .Select(tb => new OcrWord(tb.Text, tb.Box.X, tb.Box.Y, tb.Box.Width, tb.Box.Height))
                .ToList();

            var lines = GroupLines(words);
            info.RawText += string.Join(Environment.NewLine, lines.Select(l => l.Joined));

            OcrLine? nameLine = null;
            OcrLine? qualityLine = null;
            Rect? iconZone = null;

            // 结构锚点："装备分数"行（OCR 最稳定的一行，仅用作定位锚点，分数本身由副属性计算）
            var scoreLine = lines.FirstOrDefault(l => l.Joined.Contains("装备分数"));
            if (scoreLine != null)
            {
                // 套装行：分数行下方，含 n/m 计数
                var setLine = lines
                    .Where(l => l.Y > scoreLine.Y + scoreLine.H * 0.5 && Regex.IsMatch(l.Joined, @"\d+\s*/\s*\d+"))
                    .OrderBy(l => l.Y)
                    .FirstOrDefault();
                if (setLine != null)
                {
                    var setMatch = Regex.Match(setLine.Joined, @"([一-龥]{2,4})(?:套装|于装|讲装|讨装)");
                    if (setMatch.Success)
                        info.SetName = setMatch.Groups[1].Value + "套装";
                }

                // 属性行：分数行上方"以数值结尾且含属性关键字"的行，最上面的是主属性
                var statLines = lines
                    .Where(l => l.Y + l.H < scoreLine.Y + scoreLine.H * 0.5
                             && TrailingValueRegex.IsMatch(l.Joined)
                             && !l.Joined.Contains('/')
                             && MatchStatKeyword(l.Joined) != null)
                    .OrderBy(l => l.Y)
                    .ToList();

                var mainDone = false;
                foreach (var l in statLines)
                {
                    var stat = MatchStatKeyword(l.Joined);
                    if (stat == null)
                        continue;
                    var value = TrailingValueRegex.Match(l.Joined).Groups[1].Value.Replace(" ", "");
                    if (!mainDone)
                    {
                        info.MainStatName = stat;
                        info.MainStatValue = value;
                        mainDone = true;
                    }
                    else
                    {
                        var sub = new SubStat { Name = stat };
                        var em = Regex.Match(value, @"^([^（(]+)[（(]([^）)]+)[）)]$");
                        if (em.Success)
                        {
                            sub.Value = em.Groups[1].Value;
                            sub.EnhanceValue = em.Groups[2].Value;
                        }
                        else
                        {
                            sub.Value = value;
                        }
                        info.SubStats.Add(sub);
                    }
                }

                // 装备分数：按民间算法由副属性计算（强化增加值已在副属性总值内，不重复计）
                info.Score = EquipmentScoreCalculator.Calculate(info.SubStats);

                // 名称行：主属性行上方最近的中文行；品质行：名称行上方最近的中文行
                var aboveStats = statLines.Count > 0 ? statLines[0].Y : scoreLine.Y;
                nameLine = lines
                    .Where(l => l.Y + l.H <= aboveStats + l.H * 0.5 && HasChinese(l.Joined) && !IsInfoLine(l.Joined))
                    .OrderByDescending(l => l.Y)
                    .FirstOrDefault();
                if (nameLine != null)
                {
                    info.Name = ExtractName(nameLine.Joined);
                    // 品质行：名称行上方最近的一行含品质词（传说/史诗/…）
                    qualityLine = lines
                        .Where(l => l.Y + l.H <= nameLine.Y + l.H * 0.5
                                 && QualityWords.Any(q => l.Joined.Contains(q)))
                        .OrderByDescending(l => l.Y)
                        .FirstOrDefault();
                    if (qualityLine != null)
                        info.Quality = ExtractQuality(qualityLine.Joined);
                }
            }

            // 小图标区域：名称/品质行左侧（含等级"88"和强化徽章"+3"）
            if (nameLine != null)
            {
                var topRef = qualityLine ?? nameLine;
                var zoneY = Math.Max(0, topRef.Y - 3 * topRef.H);
                var zoneBottom = Math.Min(mat.Height, nameLine.Y + nameLine.H + nameLine.H / 2);
                // 名称/品质文本从同一列开始（图标右侧）。取两者较大的 X：
                // OCR 偶尔把图标噪声并入词框使 X 偏小，会导致区域右边界切掉徽章
                var textX = Math.Max(nameLine.X, qualityLine?.X ?? 0);
                var zoneRight = Math.Min(mat.Width, textX + 2 * nameLine.H);
                iconZone = new Rect(0, zoneY, zoneRight, zoneBottom - zoneY);
                RecognizeLevels(mat, iconZone.Value, topRef, info);
            }

            SaveDebugImage(imagePath, mat, lines, scoreLine, iconZone);
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
            if (enhBox != null && int.TryParse(enhBox.Text.Trim().TrimStart('+'), out var enh2))
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
            if (m.Success && int.TryParse(m.Groups[1].Value, out var enh))
                info.EnhanceLevel = enh;

            if (info.EnhanceLevel == 0)
            {
                var (v, conf) = _digitMatcher.RecognizeEnhanceLevel(badgeMat);
                if (conf > 0.5)
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
        var m = Regex.Match(text, $@"({string.Join("|", QualityWords)})({string.Join("|", TypeWords)})?");
        return m.Success ? m.Value : string.Empty;
    }

    private static string ExtractName(string text)
    {
        var runs = Regex.Matches(text, @"[一-龥]{2,}");
        return runs.Count == 0
            ? text
            : runs.OrderByDescending(m => m.Length).First().Value;
    }

    private static bool HasChinese(string text) => Regex.IsMatch(text, @"[一-龥]");

    private static bool IsInfoLine(string text)
        => QualityWords.Any(text.Contains) || StatKeywords.Any(text.Contains)
           || text.Contains("装备分数") || text.Contains("套装");

    private static void SaveDebugImage(string imagePath, Mat mat, List<OcrLine> lines, OcrLine? anchor, Rect? iconZone)
    {
        try
        {
            using var debug = mat.Clone();
            foreach (var l in lines)
                Cv2.Rectangle(debug, new Rect(l.X, l.Y, l.W, l.H), new Scalar(0, 255, 0), 1);
            if (anchor != null)
                Cv2.Rectangle(debug, new Rect(anchor.X, anchor.Y, anchor.W, anchor.H), new Scalar(0, 0, 255), 2);
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
