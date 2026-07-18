using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// PaddleOCR 引擎（PP-OCRv4 ONNX）：文本检测 + 单行识别。
/// 对游戏界面中文小字的识别率显著优于 Tesseract。
/// </summary>
public class PaddleOcrEngine : IDisposable
{
    /// <summary>检测到的文本框。</summary>
    public sealed record TextBox(string Text, Rect Box, float Score);

    private const int DetLimitSideLen = 736;
    private const float DetThresh = 0.3f;
    private const float DetBoxThresh = 0.5f;
    private const int RecImageHeight = 48;
    private const int RecMaxWidth = 320;

    private readonly InferenceSession _det;
    private readonly InferenceSession _rec;
    private readonly string[] _keys;
    private readonly string _detInputName;
    private readonly string _recInputName;

    public PaddleOcrEngine(string modelDir)
    {
        var detPath = Path.Combine(modelDir, "ch_PP-OCRv4_det_infer.onnx");
        var recPath = Path.Combine(modelDir, "ch_PP-OCRv4_rec_infer.onnx");
        var dictPath = Path.Combine(modelDir, "ppocr_keys_v1.txt");
        if (!File.Exists(detPath) || !File.Exists(recPath) || !File.Exists(dictPath))
            throw new FileNotFoundException($"PaddleOCR 模型文件缺失: {modelDir}");

        _det = new InferenceSession(detPath);
        _rec = new InferenceSession(recPath);
        _detInputName = _det.InputMetadata.Keys.First();
        _recInputName = _rec.InputMetadata.Keys.First();

        // CTC 输出索引 0 为 blank，字符表从索引 1 开始
        var keys = new List<string> { string.Empty };
        keys.AddRange(File.ReadAllLines(dictPath));
        keys.Add(" ");
        _keys = keys.ToArray();
    }

    /// <summary>
    /// 检测并识别图像中的所有文本行。
    /// </summary>
    public List<TextBox> Run(Mat img)
    {
        var boxes = Detect(img);
        var results = new List<TextBox>(boxes.Count);
        foreach (var (box, score) in boxes)
        {
            using var crop = ImagePreprocessor.Crop(img, box);
            if (crop.Empty())
                continue;
            var text = RecognizeLine(crop);
            if (!string.IsNullOrWhiteSpace(text))
                results.Add(new TextBox(text, box, score));
        }
        return results;
    }

    /// <summary>
    /// DBNet 文本检测，返回 (外接矩形, 置信度)。
    /// </summary>
    private List<(Rect box, float score)> Detect(Mat img)
    {
        // 限制最长边，并补齐到 32 的倍数
        var ratio = 1.0f;
        var maxSide = Math.Max(img.Width, img.Height);
        if (maxSide > DetLimitSideLen)
            ratio = DetLimitSideLen / (float)maxSide;

        var resizeH = (int)Math.Ceiling(img.Height * ratio / 32.0) * 32;
        var resizeW = (int)Math.Ceiling(img.Width * ratio / 32.0) * 32;

        using var resized = new Mat();
        Cv2.Resize(img, resized, new OpenCvSharp.Size(resizeW, resizeH));

        var tensor = ToNormalizedTensor(resized);
        using var outputs = _det.Run(new[] { NamedOnnxValue.CreateFromTensor(_detInputName, tensor) });
        var pred = outputs[0].AsTensor<float>(); // [1, 1, H, W]
        var predData = pred.ToArray();

        // 概率图 -> 位图
        using var probMap = Mat.FromPixelData(resizeH, resizeW, MatType.CV_32FC1, predData);
        using var bitmap = new Mat();
        Cv2.Threshold(probMap, bitmap, DetThresh, 1.0, ThresholdTypes.Binary);
        using var bitmapU8 = new Mat();
        bitmap.ConvertTo(bitmapU8, MatType.CV_8UC1, 255.0);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2, 2));
        Cv2.Dilate(bitmapU8, bitmapU8, kernel);

        Cv2.FindContours(bitmapU8, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var scaleX = img.Width / (double)resizeW;
        var scaleY = img.Height / (double)resizeH;
        var results = new List<(Rect, float)>();

        foreach (var contour in contours)
        {
            var r = Cv2.BoundingRect(contour);
            if (r.Width < 4 || r.Height < 4)
                continue;

            // 平均置信度
            using var mask = new Mat(resizeH, resizeW, MatType.CV_8UC1, Scalar.Black);
            Cv2.DrawContours(mask, new[] { contour }, -1, Scalar.White, -1);
            var score = (float)Cv2.Mean(probMap, mask).Val0;
            if (score < DetBoxThresh)
                continue;

            // DB 收缩图会偏小，向外扩张（近似 unclip）
            var padX = (int)(r.Width * 0.10 + 2);
            var padY = (int)(r.Height * 0.20 + 2);
            var x = Math.Max(0, r.X - padX);
            var y = Math.Max(0, r.Y - padY);
            var w = Math.Min(resizeW - x, r.Width + 2 * padX);
            var h = Math.Min(resizeH - y, r.Height + 2 * padY);

            var box = new Rect(
                (int)(x * scaleX), (int)(y * scaleY),
                (int)(w * scaleX), (int)(h * scaleY));

            // 过滤过小文本（噪点）
            if (box.Height < 8 || box.Width < 8)
                continue;

            results.Add((box, score));
        }

        return results.OrderBy(b => b.Item1.Y).ThenBy(b => b.Item1.X).ToList();
    }

    /// <summary>
    /// CRNN 单行文本识别。
    /// </summary>
    public string RecognizeLine(Mat lineImg)
    {
        if (lineImg.Empty())
            return string.Empty;

        var ratio = lineImg.Width / (double)lineImg.Height;
        var resizeW = Math.Min((int)Math.Ceiling(RecImageHeight * ratio), RecMaxWidth);
        resizeW = Math.Max(resizeW, 8);

        using var resized = new Mat();
        Cv2.Resize(lineImg, resized, new OpenCvSharp.Size(resizeW, RecImageHeight));

        var tensor = ToNormalizedTensor(resized);
        using var outputs = _rec.Run(new[] { NamedOnnxValue.CreateFromTensor(_recInputName, tensor) });
        var pred = outputs[0].AsTensor<float>(); // [1, T, C]

        var dims = pred.Dimensions.ToArray();
        var t = dims[1];
        var c = dims[2];
        var data = pred.ToArray();

        // CTC 解码：去重并跳过 blank(0)
        var sb = new System.Text.StringBuilder();
        var prevIndex = -1;
        for (var i = 0; i < t; i++)
        {
            var maxIndex = 0;
            var maxValue = float.MinValue;
            var offset = i * c;
            for (var j = 0; j < c; j++)
            {
                if (data[offset + j] > maxValue)
                {
                    maxValue = data[offset + j];
                    maxIndex = j;
                }
            }
            if (maxIndex > 0 && maxIndex != prevIndex && maxIndex < _keys.Length)
                sb.Append(_keys[maxIndex]);
            prevIndex = maxIndex;
        }

        return sb.ToString();
    }

    /// <summary>
    /// BGR Mat -> NCHW float tensor，(x/255 - 0.5) / 0.5 归一化。
    /// 输入允许是灰度图或 BGRA（如二值掩码放大后的数字条），内部先转为 3 通道 BGR，
    /// 否则按 3 通道读取会越界导致 AccessViolationException。
    /// </summary>
    private static DenseTensor<float> ToNormalizedTensor(Mat bgr)
    {
        Mat? converted = null;
        if (bgr.Channels() != 3)
        {
            converted = new Mat();
            Cv2.CvtColor(bgr, converted,
                bgr.Channels() == 1 ? ColorConversionCodes.GRAY2BGR : ColorConversionCodes.BGRA2BGR);
            bgr = converted;
        }

        try
        {
            var h = bgr.Height;
            var w = bgr.Width;
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });

            unsafe
            {
                var src = (byte*)bgr.Data;
                var step = (int)bgr.Step();
                for (var y = 0; y < h; y++)
                {
                    var row = src + y * step;
                    for (var x = 0; x < w; x++)
                    {
                        var b = row[x * 3] / 255.0f;
                        var g = row[x * 3 + 1] / 255.0f;
                        var r = row[x * 3 + 2] / 255.0f;
                        tensor[0, 0, y, x] = (r - 0.5f) / 0.5f;
                        tensor[0, 1, y, x] = (g - 0.5f) / 0.5f;
                        tensor[0, 2, y, x] = (b - 0.5f) / 0.5f;
                    }
                }
            }

            return tensor;
        }
        finally
        {
            converted?.Dispose();
        }
    }

    public void Dispose()
    {
        _det.Dispose();
        _rec.Dispose();
    }
}
