using OpenCvSharp;

namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// 数字模板匹配器：识别装备等级和强化等级。
/// 使用整个数字区域作为模板，不再分割单个数字。
/// </summary>
public class DigitTemplateMatcher : IDisposable
{
    private readonly Dictionary<string, Mat> _templates = new();
    private readonly string _templateDir;

    public DigitTemplateMatcher(string templateDir)
    {
        _templateDir = templateDir;
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var digitsDir = Path.Combine(_templateDir, "digits");
        if (!Directory.Exists(digitsDir))
            return;

        // 加载所有 png 文件作为模板
        foreach (var file in Directory.GetFiles(digitsDir, "*.png"))
        {
            var label = Path.GetFileNameWithoutExtension(file);
            var mat = Cv2.ImRead(file, ImreadModes.Grayscale);
            if (!mat.Empty())
                _templates[label] = mat;
        }
    }

    /// <summary>
    /// 将二值图与指定模板做归一化相关匹配（模板缩放到输入尺寸）。
    /// </summary>
    public double MatchBinaryCrop(Mat binaryCrop, string label)
    {
        if (binaryCrop.Empty() || !_templates.TryGetValue(label, out var template))
            return 0;

        using var resizedTemplate = new Mat();
        Cv2.Resize(template, resizedTemplate, binaryCrop.Size());

        using var result = new Mat();
        Cv2.MatchTemplate(binaryCrop, resizedTemplate, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
        return maxVal;
    }

    /// <summary>
    /// 将二值图与所有 "_mask" 结尾的模板（整串数字掩码，如 85_mask/88_mask）逐一匹配，
    /// 返回置信度最高的 (标签, 置信度)。多模板竞争可避免单个 88 模板把 "85" 误判为 88。
    /// </summary>
    public (string label, double confidence) MatchBinaryCropBest(Mat binaryCrop)
    {
        var bestLabel = string.Empty;
        var bestConf = 0.0;
        foreach (var label in _templates.Keys)
        {
            if (!label.EndsWith("_mask", StringComparison.OrdinalIgnoreCase))
                continue;
            var conf = MatchBinaryCrop(binaryCrop, label);
            if (conf > bestConf)
            {
                bestConf = conf;
                bestLabel = label;
            }
        }
        return (bestLabel, bestConf);
    }

    /// <summary>
    /// 识别装备等级（如 88）。
    /// </summary>
    public (int value, double confidence) RecognizeLevel(Mat region)
    {
        if (region.Empty() || _templates.Count == 0)
            return (0, 0);

        var preprocessed = ImagePreprocessor.PreprocessDigitRegion(region);
        var (label, confidence) = MatchTemplate(preprocessed);
        preprocessed.Dispose();

        if (int.TryParse(label, out var value))
            return (value, confidence);

        return (0, 0);
    }

    /// <summary>
    /// 识别强化等级（如 +3）。
    /// </summary>
    public (int value, double confidence) RecognizeEnhanceLevel(Mat region)
    {
        if (region.Empty() || _templates.Count == 0)
            return (0, 0);

        var preprocessed = ImagePreprocessor.PreprocessDigitRegion(region);
        var (label, confidence) = MatchTemplate(preprocessed);
        preprocessed.Dispose();

        // 解析 "+3" 格式的标签
        if (label.StartsWith("+") && int.TryParse(label.Substring(1), out var value))
            return (value, confidence);

        // 尝试直接解析数字
        if (int.TryParse(label, out value))
            return (value, confidence);

        return (0, 0);
    }

    private (string label, double confidence) MatchTemplate(Mat image)
    {
        if (image.Empty())
            return (string.Empty, 0);

        double bestConf = 0;
        string bestLabel = string.Empty;

        foreach (var (label, template) in _templates)
        {
            // 调整模板尺寸以匹配输入
            var resizedTemplate = new Mat();
            Cv2.Resize(template, resizedTemplate, image.Size());

            var result = new Mat();
            Cv2.MatchTemplate(image, resizedTemplate, result, TemplateMatchModes.CCoeffNormed);

            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);

            if (maxVal > bestConf)
            {
                bestConf = maxVal;
                bestLabel = label;
            }

            resizedTemplate.Dispose();
            result.Dispose();
        }

        return (bestLabel, bestConf);
    }

    public void Dispose()
    {
        foreach (var template in _templates.Values)
            template.Dispose();
        _templates.Clear();
    }
}
