using OpenCvSharp;
using System.Drawing;

namespace TiezhuToolbox.Modules.Ocr;

/// <summary>
/// 图像预处理器：按基准分辨率换算坐标并裁剪关键区域。
/// </summary>
public class ImagePreprocessor
{
    /// <summary>基准分辨率宽度（横屏）。</summary>
    public const int BaseWidth = 1280;

    /// <summary>基准分辨率高度（横屏）。</summary>
    public const int BaseHeight = 720;

    /// <summary>
    /// 计算输入图像相对于基准分辨率的缩放比例。
    /// </summary>
    public static (float scaleX, float scaleY) GetScale(int imageWidth, int imageHeight)
    {
        return ((float)imageWidth / BaseWidth, (float)imageHeight / BaseHeight);
    }

    /// <summary>
    /// 按基准坐标和缩放比例换算实际坐标。
    /// </summary>
    public static Rect ScaleRect(Rect baseRect, float scaleX, float scaleY)
    {
        return new Rect(
            (int)(baseRect.X * scaleX),
            (int)(baseRect.Y * scaleY),
            (int)(baseRect.Width * scaleX),
            (int)(baseRect.Height * scaleY));
    }

    /// <summary>
    /// 获取当前截图中的左侧装备信息区域。右边界通过面板竖向分隔线定位，
    /// 兼容纯 ADB 截图和带模拟器窗口边框、画面被横向截断的旧截图。
    /// </summary>
    public static Rect GetEquipmentPanelRect(Mat source)
    {
        var top = (int)Math.Round(source.Height * 0.09);
        var bottom = (int)Math.Round(source.Height * 0.90);
        var right = FindEquipmentPanelRight(source);
        return new Rect(0, top, right, Math.Max(0, bottom - top));
    }

    /// <summary>在画面左侧 25%～46% 范围内寻找贯穿装备面板的竖向分隔线。</summary>
    private static unsafe int FindEquipmentPanelRight(Mat source)
    {
        using var gray = new Mat();
        if (source.Channels() == 1)
            source.CopyTo(gray);
        else if (source.Channels() == 4)
            Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
        else
            Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

        var startX = Math.Clamp((int)(source.Width * 0.25), 0, source.Width - 2);
        var endX = Math.Clamp((int)(source.Width * 0.46), startX + 1, source.Width - 1);
        var startY = Math.Clamp((int)(source.Height * 0.12), 0, source.Height - 1);
        var endY = Math.Clamp((int)(source.Height * 0.90), startY + 1, source.Height);
        var step = (int)gray.Step();
        var data = (byte*)gray.Data;

        var bestX = Math.Clamp((int)(source.Width * 0.45), 1, source.Width);
        var bestAverage = 0.0;
        for (var x = startX; x < endX; x++)
        {
            long difference = 0;
            var samples = 0;
            for (var y = startY; y < endY; y += 2)
            {
                var row = data + y * step;
                difference += Math.Abs(row[x + 1] - row[x]);
                samples++;
            }

            var average = samples == 0 ? 0 : difference / (double)samples;
            if (average > bestAverage)
            {
                bestAverage = average;
                bestX = x;
            }
        }

        // 找不到明显分隔线时退回左侧 45%，避免裁掉旧版画面的属性数值列。
        return bestAverage >= 12
            ? Math.Min(source.Width, bestX + 4)
            : Math.Clamp((int)(source.Width * 0.45), 1, source.Width);
    }

    /// <summary>
    /// 裁剪指定区域并返回 Mat。
    /// </summary>
    public static Mat Crop(Mat source, Rect rect)
    {
        // 边界保护
        var x = Math.Max(0, rect.X);
        var y = Math.Max(0, rect.Y);
        var width = Math.Min(rect.Width, source.Width - x);
        var height = Math.Min(rect.Height, source.Height - y);

        if (width <= 0 || height <= 0)
            return new Mat();

        return new Mat(source, new Rect(x, y, width, height));
    }

    /// <summary>
    /// 预处理数字区域：灰度化、二值化、放大。
    /// </summary>
    public static Mat PreprocessDigitRegion(Mat region)
    {
        if (region.Empty())
            return region;

        // 灰度化
        var gray = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);

        // 放大 4 倍，提高模板匹配精度
        var scaled = new Mat();
        Cv2.Resize(gray, scaled, new OpenCvSharp.Size(region.Width * 4, region.Height * 4), interpolation: InterpolationFlags.Cubic);

        // 二值化：数字通常是浅色，背景深色
        var binary = new Mat();
        Cv2.Threshold(scaled, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // 反转：让数字为白色，背景为黑色
        Cv2.BitwiseNot(binary, binary);

        gray.Dispose();
        scaled.Dispose();

        return binary;
    }

    /// <summary>
    /// 将 Mat 转换为 Bitmap。
    /// </summary>
    public static Bitmap MatToBitmap(Mat mat)
    {
        if (mat.Empty())
            return new Bitmap(1, 1);

        // 确保是 8 位单通道或三通道
        Mat converted;
        if (mat.Channels() == 1)
        {
            converted = new Mat();
            Cv2.CvtColor(mat, converted, ColorConversionCodes.GRAY2BGR);
        }
        else if (mat.Channels() == 3)
        {
            converted = mat.Clone();
        }
        else
        {
            converted = new Mat();
            Cv2.CvtColor(mat, converted, ColorConversionCodes.BGRA2BGR);
        }

        var bitmap = new Bitmap(converted.Width, converted.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, converted.Width, converted.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        unsafe
        {
            byte* ptr = (byte*)bitmapData.Scan0;
            int stride = bitmapData.Stride;
            int matStride = (int)converted.Step();

            for (int y = 0; y < converted.Height; y++)
            {
                byte* bitmapRow = ptr + y * stride;
                byte* matRow = (byte*)converted.Data + y * matStride;

                for (int x = 0; x < converted.Width * 3; x++)
                {
                    bitmapRow[x] = matRow[x];
                }
            }
        }

        bitmap.UnlockBits(bitmapData);
        converted.Dispose();

        return bitmap;
    }
}
