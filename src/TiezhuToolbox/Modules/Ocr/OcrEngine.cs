using OpenCvSharp;
using System.Text;
using System.Text.RegularExpressions;
using Tesseract;
using Rect = OpenCvSharp.Rect;

namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// OCR 引擎：识别装备文本信息。
/// 使用 Tesseract 词坐标 + 结构锚点（装备分数行）定位，不依赖固定分辨率坐标。
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
    private readonly string _tessDataDir;
    private readonly string _paddleModelDir;
    private readonly object _engineLock = new();
    private readonly Dictionary<string, TesseractEngine> _engines = new();
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
        _tessDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
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

        if (!Directory.Exists(_tessDataDir))
        {
            info.RawText = $"[Tesseract 错误: tessdata 目录不存在: {_tessDataDir}]";
            return info;
        }

        try
        {
            // 装备信息面板在窗口左侧（约占宽度 45%）
            var panelRect = new Rect(0, 0, (int)(mat.Width * 0.45), mat.Height);
            using var panel = ImagePreprocessor.Crop(mat, panelRect);

            // 优先 PaddleOCR（中文游戏字体识别率高），失败时回退 Tesseract
            List<OcrWord> words;
            try
            {
                words = GetPaddle().Run(panel)
                    .Select(tb => new OcrWord(tb.Text, tb.Box.X, tb.Box.Y, tb.Box.Width, tb.Box.Height))
                    .ToList();
            }
            catch (Exception paddleEx)
            {
                using var preprocessed = ImagePreprocessor.PreprocessTextRegion(panel);
                var scaleBack = panel.Width / (double)preprocessed.Width;
                words = OcrWords(preprocessed, scaleBack, PageSegMode.SparseText, whitelist: null, bestModel: true);
                info.RawText = $"[PaddleOCR 不可用，回退 Tesseract: {paddleEx.Message}]{Environment.NewLine}";
            }

            var lines = GroupLines(words);
            info.RawText += string.Join(Environment.NewLine, lines.Select(l => l.Joined));

            OcrLine? nameLine = null;
            OcrLine? qualityLine = null;
            Rect? iconZone = null;

            // 结构锚点："装备分数"行（OCR 最稳定的一行）
            var scoreLine = lines.FirstOrDefault(l => l.Joined.Contains("装备分数"));
            if (scoreLine != null)
            {
                var sm = Regex.Match(scoreLine.Joined, @"装备分数\D*(\d+(?:[（(]\+\d+[）)])?)");
                if (sm.Success)
                    info.Score = sm.Groups[1].Value;

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
                var zoneRight = Math.Min(mat.Width, nameLine.X + 2 * nameLine.H);
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

            // 优先 PaddleOCR，失败回退 Tesseract 数字白名单
            try
            {
                badgeText = GetPaddle().RecognizeLine(badgeMat);
            }
            catch { /* 忽略，走 Tesseract */ }

            if (string.IsNullOrWhiteSpace(badgeText))
            {
                using var badgePre = PreprocessForDigits(badgeMat, 4);
                var badgeWords = OcrWords(badgePre, 1.0, PageSegMode.SingleLine, "+0123456789", bestModel: false);
                badgeText = string.Concat(badgeWords.Select(w => w.Text));
            }

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

        // 装备等级"88"：在徽章同一水平带上、徽章左侧（图标左上角绶带上的金色文字）
        if (info.Level == 0 && badge is Rect bd)
        {
            var stripRect = new Rect(
                0,
                Math.Max(0, bd.Y - 10),
                Math.Max(0, bd.X - 8),
                Math.Min(zoneMat.Height - Math.Max(0, bd.Y - 10), bd.Height + 35));
            using var strip = ImagePreprocessor.Crop(zoneMat, stripRect);
            if (!strip.Empty())
            {
                // "88"是金色文字：按颜色提取掩码，排除深色背景和紫色图标图案
                using var goldMask = new Mat();
                Cv2.InRange(strip, new Scalar(0, 120, 150), new Scalar(140, 220, 255), goldMask);

                // 连通域过滤：数字笔画是饱满的块，边框是细长线（高填充率/超大），剔除
                Cv2.FindContours(goldMask, out var goldContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                var digitRects = new List<Rect>();
                foreach (var c in goldContours)
                {
                    var r = Cv2.BoundingRect(c);
                    if (r.Height < 6 || r.Height > 30 || r.Width < 2 || r.Width > 25)
                        continue;
                    var fill = Cv2.ContourArea(c) / (r.Width * (double)r.Height);
                    if (fill > 0.75)
                        continue; // 实心细条 = 图标边框
                    digitRects.Add(r);
                }

                if (digitRects.Count > 0)
                {
                    // 数字共享同一基线且笔画饱满：以面积最大的连通域为基准（细边框线面积小），
                    // 剔除偏离基线的碎块，再取与基准相邻的连续段
                    var main = digitRects.OrderByDescending(r => r.Width * (long)r.Height).First();
                    var mainCenterY = main.Y + main.Height / 2.0;
                    var cluster = digitRects
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

                    // 方案 A：与标准"88"二值模板做相关匹配（字体固定，最稳定）
                    var maskConf = _digitMatcher.MatchBinaryCrop(digitCrop, "88_mask");
                    info.RawText += $"{Environment.NewLine}[debug] strip 88_mask conf={maskConf:F3}";
                    if (maskConf >= 0.65)
                        info.Level = 88;

                    // 方案 B：PaddleOCR 整行识别
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

                    // 方案 B：Tesseract 整行识别
                    if (info.Level == 0)
                    {
                        var lineWords = OcrWords(digitBig, 1.0, PageSegMode.SingleLine, "0123456789", bestModel: false);
                        var lineText = string.Concat(lineWords.Select(w => w.Text));
                        info.RawText += $"{Environment.NewLine}[debug] strip tess='{lineText}'";
                        var tm = Regex.Match(lineText.Trim(), @"^\d{1,3}$");
                        if (tm.Success && int.TryParse(tm.Value, out var tlvl))
                            info.Level = tlvl;
                    }

                    // 方案 C：逐字识别（按宽高比切段）
                    if (info.Level == 0)
                    {
                        var digitCount = Math.Clamp((int)Math.Round(digitCrop.Width / (double)Math.Max(1, digitCrop.Height)), 1, 3);
                        var sb = new System.Text.StringBuilder();
                        for (var i = 0; i < digitCount; i++)
                        {
                            var segW = digitCrop.Width / digitCount;
                            using var seg = ImagePreprocessor.Crop(digitCrop, new Rect(i * segW, 0, Math.Min(segW + 2, digitCrop.Width - i * segW), digitCrop.Height));
                            if (seg.Empty())
                                continue;
                            using var segInv = new Mat();
                            Cv2.BitwiseNot(seg, segInv);
                            using var segPadded = new Mat();
                            Cv2.CopyMakeBorder(segInv, segPadded, 8, 8, 8, 8, BorderTypes.Constant, Scalar.White);
                            using var segBig = new Mat();
                            Cv2.Resize(segPadded, segBig, new OpenCvSharp.Size(segPadded.Width * 6, segPadded.Height * 6), interpolation: InterpolationFlags.Cubic);
                            var charWords = OcrWords(segBig, 1.0, PageSegMode.SingleChar, "0123456789", bestModel: false);
                            var ch = charWords.Select(w => w.Text).FirstOrDefault(t => Regex.IsMatch(t, @"^\d$"));
                            sb.Append(ch ?? "?");
                        }

                        var stripText = sb.ToString();
                        info.RawText += $"{Environment.NewLine}[debug] strip chars='{stripText}'";
                        if (!stripText.Contains('?') && int.TryParse(stripText, out var slvl))
                            info.Level = slvl;
                    }
                }
            }
        }

        // 方案三：装备等级 Tesseract 数字 OCR（先把徽章涂黑避免干扰）
        if (info.Level != 0)
            return;

        using var gray = new Mat();
        Cv2.CvtColor(zoneMat, gray, ColorConversionCodes.BGR2GRAY);
        if (badge is Rect bb)
            Cv2.Rectangle(gray, bb, Scalar.Black, -1);

        using var zonePre = PreprocessForDigits(gray, 3);
        var digitWords = OcrWords(zonePre, 1.0, PageSegMode.SparseText, "0123456789", bestModel: false);
        var levelWord = digitWords
            .Where(w => Regex.IsMatch(w.Text, @"^\d{1,3}$") && w.H >= textAnchor.H * 0.4 && w.H <= textAnchor.H * 2.5)
            .OrderBy(w => w.Text.Length == 2 ? 0 : 1)
            .ThenBy(w => w.Y)
            .ThenBy(w => w.X)
            .FirstOrDefault();
        if (levelWord != null && int.TryParse(levelWord.Text, out var lvl))
            info.Level = lvl;

        // 模板兜底（当前只有 88 / +3 模板）
        if (info.Level == 0)
        {
            using var topLeft = ImagePreprocessor.Crop(zoneMat, new Rect(0, 0, zoneMat.Width / 2, zoneMat.Height / 2));
            var (v, conf) = _digitMatcher.RecognizeLevel(topLeft);
            if (conf > 0.5)
                info.Level = v;
        }
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

    /// <summary>
    /// 数字区域预处理：灰度、放大、Otsu 二值化，深底浅字时反转为白底黑字。
    /// </summary>
    private static Mat PreprocessForDigits(Mat src, int scale)
    {
        var gray = new Mat();
        if (src.Channels() == 1)
            src.CopyTo(gray);
        else
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        var scaled = new Mat();
        Cv2.Resize(gray, scaled, new OpenCvSharp.Size(src.Width * scale, src.Height * scale), interpolation: InterpolationFlags.Cubic);

        var binary = new Mat();
        Cv2.Threshold(scaled, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        if (Cv2.Mean(binary).Val0 < 127)
            Cv2.BitwiseNot(binary, binary);

        gray.Dispose();
        scaled.Dispose();
        return binary;
    }

    private List<OcrWord> OcrWords(Mat processed, double scaleBack, PageSegMode psm, string? whitelist, bool bestModel)
    {
        var result = new List<OcrWord>();
        if (processed.Empty())
            return result;

        lock (_engineLock)
        {
            var engine = GetEngine(bestModel);
            engine.SetVariable("tessedit_char_whitelist", whitelist ?? "");

            Cv2.ImEncode(".png", processed, out var buf);
            using var pix = Pix.LoadFromMemory(buf);
            using var page = engine.Process(pix, psm);
            using var iter = page.GetIterator();
            iter.Begin();
            do
            {
                var text = iter.GetText(PageIteratorLevel.Word)?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;
                if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var r))
                {
                    result.Add(new OcrWord(text,
                        (int)(r.X1 * scaleBack), (int)(r.Y1 * scaleBack),
                        (int)(r.Width * scaleBack), (int)(r.Height * scaleBack)));
                }
            }
            while (iter.Next(PageIteratorLevel.Word));
        }

        return result;
    }

    private PaddleOcrEngine GetPaddle()
    {
        _paddle ??= new PaddleOcrEngine(_paddleModelDir);
        return _paddle;
    }

    private TesseractEngine GetEngine(bool bestModel)
    {
        // 文本用高精度模型（tessdata_best），数字用快速模型即可
        var lang = bestModel && File.Exists(Path.Combine(_tessDataDir, "chi_sim_best.traineddata"))
            ? "chi_sim_best"
            : "chi_sim";

        if (!_engines.TryGetValue(lang, out var engine))
        {
            engine = new TesseractEngine(_tessDataDir, lang, EngineMode.Default);
            _engines[lang] = engine;
        }
        return engine;
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
        lock (_engineLock)
        {
            foreach (var engine in _engines.Values)
                engine.Dispose();
            _engines.Clear();
        }
        _paddle?.Dispose();
        _digitMatcher.Dispose();
    }
}
