using OpenCvSharp;

namespace BatteryTabVision.Engines.OpenCv.Samples;

/// <summary>
/// 合成缺陷类型枚举。
/// </summary>
public enum SyntheticDefect
{
    /// <summary>无缺陷（正常图像）。</summary>
    None,
    /// <summary>注入毛刺（Burr）。</summary>
    Burr,
    /// <summary>注入错位（Misalignment）。</summary>
    Misalignment,
    /// <summary>同时注入毛刺与错位。</summary>
    Both,
}

/// <summary>
/// 生成用于开发测试的合成极耳图像。
/// 合成图：白底（255）+ 居中黑色矩形（代表极耳）+ 可选高斯噪声 + 可选缺陷注入。
/// 优点：ground truth 由代码定义，测试确定可重复，无需真实样本图文件。
/// </summary>
public static class SyntheticTabImageGenerator
{
    /// <summary>
    /// 生成合成极耳图像，写入临时 PNG 文件，返回路径与像素尺寸 ground truth。
    /// </summary>
    /// <param name="imageWidth">图像宽度（像素）</param>
    /// <param name="imageHeight">图像高度（像素）</param>
    /// <param name="tabLengthPx">极耳矩形长度（像素）</param>
    /// <param name="tabWidthPx">极耳矩形宽度（像素）</param>
    /// <param name="noiseStdDev">高斯噪声标准差（0 = 无噪声）</param>
    /// <param name="saveToPath">指定保存路径；null 时写入 temp 目录</param>
    /// <returns>(临时文件路径, 极耳长度像素, 极耳宽度像素)</returns>
    public static (string path, double lengthPx, double widthPx) Generate(
        int imageWidth = 800,
        int imageHeight = 600,
        int tabLengthPx = 400,
        int tabWidthPx = 100,
        double noiseStdDev = 5.0,
        string? saveToPath = null)
    {
        return GenerateWithDefect(imageWidth, imageHeight, tabLengthPx, tabWidthPx,
            noiseStdDev, SyntheticDefect.None, 0, 0, 0, 0, saveToPath);
    }

    /// <summary>
    /// 生成带可选缺陷的合成极耳图像，写入 PNG 文件，返回路径与像素尺寸 ground truth。
    /// 毛刺通过在上边缘画填充三角形模拟；错位通过偏移极耳水平位置模拟。
    /// </summary>
    /// <param name="imageWidth">图像宽度（像素）</param>
    /// <param name="imageHeight">图像高度（像素）</param>
    /// <param name="tabLengthPx">极耳矩形长度（像素）</param>
    /// <param name="tabWidthPx">极耳矩形宽度（像素）</param>
    /// <param name="noiseStdDev">高斯噪声标准差（0 = 无噪声）</param>
    /// <param name="defect">注入的缺陷类型</param>
    /// <param name="burrCount">毛刺数量（仅 Burr/Both 有效）</param>
    /// <param name="burrHeightPx">毛刺突出高度（像素）</param>
    /// <param name="burrWidthPx">毛刺底部宽度（像素）</param>
    /// <param name="misalignmentPx">错位偏移量（像素，正值向右）</param>
    /// <param name="saveToPath">指定保存路径；null 时写入 temp 目录</param>
    /// <returns>(临时文件路径, 极耳长度像素, 极耳宽度像素)</returns>
    public static (string path, double lengthPx, double widthPx) GenerateWithDefect(
        int imageWidth = 800,
        int imageHeight = 600,
        int tabLengthPx = 400,
        int tabWidthPx = 100,
        double noiseStdDev = 5.0,
        SyntheticDefect defect = SyntheticDefect.None,
        int burrCount = 3,
        int burrHeightPx = 12,
        int burrWidthPx = 8,
        int misalignmentPx = 60,
        string? saveToPath = null)
    {
        using var img = new Mat(imageHeight, imageWidth, MatType.CV_8UC1, Scalar.All(255));

        int x = (imageWidth - tabLengthPx) / 2;
        int y = (imageHeight - tabWidthPx) / 2;


        if (defect is SyntheticDefect.Misalignment or SyntheticDefect.Both)
            x += misalignmentPx;


        Cv2.Rectangle(img, new Rect(x, y, tabLengthPx, tabWidthPx), Scalar.All(0), thickness: -1);


        if (defect is SyntheticDefect.Burr or SyntheticDefect.Both)
        {
            var rng = new Random(42);
            for (int i = 0; i < burrCount; i++)
            {
                int burrCenterX = rng.Next(x + burrWidthPx / 2, x + tabLengthPx - burrWidthPx / 2);
                int burrBaseY = y;
                int burrTipY = y - burrHeightPx;

                var pts = new[]
                {
                    new Point(burrCenterX - burrWidthPx / 2, burrBaseY),
                    new Point(burrCenterX + burrWidthPx / 2, burrBaseY),
                    new Point(burrCenterX, burrTipY),
                };
                Cv2.FillPoly(img, [pts], Scalar.All(0));
            }
        }

        if (noiseStdDev > 0)
        {
            using var noise = new Mat(imageHeight, imageWidth, MatType.CV_32FC1);
            Cv2.Randn(noise, mean: Scalar.All(0), stddev: Scalar.All(noiseStdDev));
            using var img32 = new Mat();
            img.ConvertTo(img32, MatType.CV_32FC1);
            Cv2.Add(img32, noise, img32);
            Cv2.Normalize(img32, img32, 0, 255, NormTypes.MinMax);
            img32.ConvertTo(img, MatType.CV_8UC1);
        }

        string path = saveToPath ?? Path.Combine(Path.GetTempPath(), $"btv_synth_{Guid.NewGuid():N}.png");
        Cv2.ImWrite(path, img);

        return (path, tabLengthPx, tabWidthPx);
    }
}
