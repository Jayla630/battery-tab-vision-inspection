using System.Diagnostics;
using System.Globalization;
using BatteryTabVision.Core.Abstractions;
using BatteryTabVision.Core.Models;
using OpenCvSharp;

namespace BatteryTabVision.Engines.OpenCv;

/// <summary>
/// <see cref="IVisionAlgorithm"/> 的 OpenCvSharp v1 实现。
/// 流水线：图像加载 → 灰度二值化 → 形态学去噪 → 轮廓提取 → MinAreaRect 测量 → 缺陷检测 → 公差判定 → 标注图落盘。
/// </summary>
public sealed class OpenCvVisionAlgorithm : IVisionAlgorithm
{
    /// <inheritdoc/>
    public string EngineName => "OpenCvSharp";

    /// <inheritdoc/>
    public bool IsInitialized { get; private set; }

    private int _binaryThreshold;
    private double _minContourArea;
    private double _pixelsPerMm;
    private double? _lengthLower;
    private double? _lengthUpper;
    private double? _widthLower;
    private double? _widthUpper;


    private bool _enableBurrDetection = true;
    private double _burrThresholdPx = 3.0;
    private int _minBurrClusterPoints = 5;
    private bool _enableMisalignmentDetection = true;
    private double _misalignmentThresholdMm = 1.0;


    private string _annotatedOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "annotated");

    private bool _disposed;

    /// <inheritdoc/>
    /// <remarks>
    /// 从 <see cref="AlgorithmConfig.Parameters"/> 中读取并验证必填键：
    /// BinaryThreshold (0-255)、MinContourArea (&gt;0)、PixelsPerMm (&gt;0)。
    /// M3b：可选加载 Defect.* 前缀的缺陷检测参数。
    /// 任一必填键缺失或值越界时抛 <see cref="ArgumentException"/>。
    /// 每次调用均更新参数，允许在运行时动态调节参数后重新初始化。
    /// </remarks>
    public Task InitializeAsync(AlgorithmConfig config, CancellationToken ct = default)
    {
        _binaryThreshold = GetRequired<int>(config, "BinaryThreshold");
        _minContourArea = GetRequired<double>(config, "MinContourArea");
        _pixelsPerMm = GetRequired<double>(config, "PixelsPerMm");

        if (_binaryThreshold < 0 || _binaryThreshold > 255)
            throw new ArgumentException("BinaryThreshold must be in range 0-255.", nameof(config));
        if (_minContourArea <= 0)
            throw new ArgumentException("MinContourArea must be positive.", nameof(config));
        if (_pixelsPerMm <= 0)
            throw new ArgumentException("PixelsPerMm must be positive.", nameof(config));

        _lengthLower = GetOptional<double>(config, "LengthLowerLimit");
        _lengthUpper = GetOptional<double>(config, "LengthUpperLimit");
        _widthLower = GetOptional<double>(config, "WidthLowerLimit");
        _widthUpper = GetOptional<double>(config, "WidthUpperLimit");


        _enableBurrDetection = GetOptional<bool>(config, "Defect.EnableBurr") ?? true;
        _burrThresholdPx = GetOptional<double>(config, "Defect.BurrThresholdPx") ?? 3.0;
        _minBurrClusterPoints = GetOptional<int>(config, "Defect.MinBurrClusterPoints") ?? 5;
        _enableMisalignmentDetection = GetOptional<bool>(config, "Defect.EnableMisalignment") ?? true;
        _misalignmentThresholdMm = GetOptional<double>(config, "Defect.MisalignmentThresholdMm") ?? 1.0;

        if (config.Parameters.TryGetValue("AnnotatedOutputDir", out var dir)
            && dir is string dirStr
            && !string.IsNullOrWhiteSpace(dirStr))
            _annotatedOutputDir = dirStr;

        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 不抛异常——运行时任何错误均捕获后写入 <see cref="InspectionResult.ErrorMessage"/>。
    /// 线程安全：同一实例不保证并发安全，调用方需自行串行化。
    /// </remarks>
    public Task<InspectionResult> DetectAsync(InspectionImage image, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_disposed)
                return Task.FromResult(ErrorResult(sw, "Engine has been disposed."));

            if (!IsInitialized)
                return Task.FromResult(ErrorResult(sw, "Engine not initialized — call InitializeAsync first."));

            using var src = LoadImage(image);
            if (src.Empty())
                return Task.FromResult(ErrorResult(sw, $"Image could not be loaded: '{image.SourcePath}'."));

            using var binary = Preprocess(src);
            var contour = FindTabContour(binary);

            if (contour is null)
                return Task.FromResult(ErrorResult(sw, "No tab contour found in image (no region with ContourArea > MinContourArea)."));


            var rect = Cv2.MinAreaRect(contour);
            var (lengthPx, widthPx) = MeasureWithMinAreaRect(rect);


            RotatedRect cleanRect;
            using (var cleanBinary = new Mat())
            {
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(11, 11));
                Cv2.MorphologyEx(binary, cleanBinary, MorphTypes.Open, kernel);
                var cleanContour = FindTabContour(cleanBinary);
                cleanRect = cleanContour is not null
                    ? Cv2.MinAreaRect(cleanContour)
                    : rect;
            }


            var defects = new List<Defect>();

            if (_enableBurrDetection)
                defects.AddRange(DetectBurrs(contour, cleanRect, _burrThresholdPx, _minBurrClusterPoints));

            if (_enableMisalignmentDetection)
            {
                var misalign = DetectMisalignment(
                    contour, new Size(src.Width, src.Height), image.Roi,
                    _misalignmentThresholdMm, _pixelsPerMm);
                if (misalign != null)
                    defects.Add(misalign);
            }


            if (defects.Any(d => d.Type == "Burr"))
            {
                var (lPx, wPx) = MeasureWithMinAreaRect(cleanRect);
                lengthPx = lPx;
                widthPx = wPx;
                rect = cleanRect;
            }

            double lengthMm = Math.Round(lengthPx / _pixelsPerMm, 4);
            double widthMm = Math.Round(widthPx / _pixelsPerMm, 4);

            var measurements = BuildMeasurements(lengthMm, widthMm);


            bool isOk = measurements.All(m =>
                m.IsInTolerance || (m.LowerLimit == null && m.UpperLimit == null))
                && defects.Count == 0;

            string annotatedPath = SaveAnnotatedImage(src, contour, rect, measurements, defects, isOk, _annotatedOutputDir);

            return Task.FromResult(new InspectionResult
            {
                IsOk = isOk,
                EngineName = EngineName,
                ElapsedMs = sw.ElapsedMilliseconds,
                Measurements = measurements,
                Defects = defects.AsReadOnly(),
                AnnotatedImagePath = annotatedPath,
                FinishedAt = DateTime.Now,
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(sw, ex.Message));
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _disposed = true;



    /// <summary>
    /// 加载图像：优先从 RawData 字节数组解码，否则从 SourcePath 读取文件。
    /// 统一输出灰度单通道 Mat；若文件不存在则返回空 Mat。
    /// </summary>
    private static Mat LoadImage(InspectionImage image)
    {
        if (image.RawData is { Length: > 0 })
            return Mat.FromImageData(image.RawData, ImreadModes.Grayscale);

        if (!File.Exists(image.SourcePath))
            return new Mat();

        return Cv2.ImRead(image.SourcePath, ImreadModes.Grayscale);
    }

    /// <summary>
    /// 预处理：BinaryInv 二值化（亮背景+暗极耳→白前景）→ 3×3 形态学开运算去噪。
    /// 注：BinaryInv 与 task card 中"Binary"的差异是因合成图为白底黑极耳；
    ///     真机可按实际背景亮度选择。
    /// </summary>
    private Mat Preprocess(Mat src)
    {
        using var gray = src.Channels() == 1 ? src.Clone() : new Mat();
        if (src.Channels() != 1)
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        var binary = new Mat();

        Cv2.Threshold(gray, binary, _binaryThreshold, 255, ThresholdTypes.BinaryInv);

        using var element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(binary, binary, MorphTypes.Open, element);

        return binary;
    }

    /// <summary>
    /// 轮廓检测：在二值图中查找所有外轮廓，返回面积最大且 &gt; MinContourArea 的那个；
    /// 找不到时返回 null。
    /// </summary>
    private Point[]? FindTabContour(Mat binary)
    {
        Cv2.FindContours(binary, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        Point[]? largest = null;
        double maxArea = _minContourArea;

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area > maxArea)
            {
                maxArea = area;
                largest = contour;
            }
        }

        return largest;
    }

    /// <summary>
    /// v1 测量：用 <see cref="Cv2.MinAreaRect"/> 拟合最小外接旋转矩形，
    /// 长边为 length，短边为 width（单位：像素）。
    /// 精度约 ±1 像素；M3 将用扫描线卡尺替换此方法以获得亚像素精度，
    /// 届时无需改动任何公共契约。
    /// </summary>
    private static (double lengthPx, double widthPx) MeasureWithMinAreaRect(RotatedRect rect)
    {
        double a = rect.Size.Width;
        double b = rect.Size.Height;
        return (Math.Max(a, b), Math.Min(a, b));
    }

    /// <summary>
    /// 根据像素测量值和公差配置构建 <see cref="Measurement"/> 列表。
    /// 无公差配置时 IsInTolerance 始终为 true。
    /// </summary>
    private IReadOnlyList<Measurement> BuildMeasurements(double lengthMm, double widthMm)
    {
        return
        [
            new Measurement
            {
                Name = "TabLength",
                Value = lengthMm,
                Unit = "mm",
                LowerLimit = _lengthLower,
                UpperLimit = _lengthUpper,
                IsInTolerance = CheckTolerance(lengthMm, _lengthLower, _lengthUpper),
            },
            new Measurement
            {
                Name = "TabWidth",
                Value = widthMm,
                Unit = "mm",
                LowerLimit = _widthLower,
                UpperLimit = _widthUpper,
                IsInTolerance = CheckTolerance(widthMm, _widthLower, _widthUpper),
            },
        ];
    }



    /// <summary>
    /// 通过比较原始轮廓点与干净参考矩形的偏差检测毛刺。
    /// 参考矩形来自大核开运算后的干净轮廓，不受毛刺影响。
    /// </summary>
    private static IReadOnlyList<Defect> DetectBurrs(
        Point[] contour,
        RotatedRect referenceRect,
        double burrThresholdPx,
        int minClusterPoints)
    {
        if (contour.Length == 0)
            return Array.Empty<Defect>();

        double halfW = referenceRect.Size.Width / 2.0;
        double halfH = referenceRect.Size.Height / 2.0;
        double angleRad = referenceRect.Angle * Math.PI / 180.0;
        double cosA = Math.Cos(angleRad);
        double sinA = Math.Sin(angleRad);
        double cx = referenceRect.Center.X;
        double cy = referenceRect.Center.Y;


        var distances = new double[contour.Length];
        for (int i = 0; i < contour.Length; i++)
        {
            double dx = contour[i].X - cx;
            double dy = contour[i].Y - cy;
            double lx = dx * cosA + dy * sinA;
            double ly = -dx * sinA + dy * cosA;

            double dxOut = Math.Max(0, Math.Abs(lx) - halfW);
            double dyOut = Math.Max(0, Math.Abs(ly) - halfH);
            distances[i] = Math.Sqrt(dxOut * dxOut + dyOut * dyOut);
        }


        var burrDefects = new List<Defect>();
        int runStart = -1;

        for (int i = 0; i <= contour.Length; i++)
        {
            bool isBurr = i < contour.Length && distances[i] > burrThresholdPx;

            if (isBurr && runStart < 0)
                runStart = i;
            else if (!isBurr && runStart >= 0)
            {
                int runLength = i - runStart;
                if (runLength >= minClusterPoints)
                {
                    var clusterPoints = contour[runStart..i];
                    Rect bbox = Cv2.BoundingRect(clusterPoints);

                    burrDefects.Add(new Defect
                    {
                        Type = "Burr",
                        BoundingBox = new System.Drawing.Rectangle(bbox.X, bbox.Y, bbox.Width, bbox.Height),
                        Confidence = 1.0,
                    });
                }
                runStart = -1;
            }
        }

        return burrDefects;
    }

    /// <summary>
    /// 检测极耳质心与预期位置的偏差。
    /// </summary>
    private static Defect? DetectMisalignment(
        Point[] contour,
        Size imageSize,
        System.Drawing.Rectangle imageRoi,
        double thresholdMm,
        double pixelsPerMm)
    {
        if (contour.Length == 0)
            return null;


        var moments = Cv2.Moments(contour);
        if (Math.Abs(moments.M00) < 1e-9)
            return null;
        double cx = moments.M10 / moments.M00;
        double cy = moments.M01 / moments.M00;


        double expectedX, expectedY;
        if (imageRoi != System.Drawing.Rectangle.Empty)
        {
            expectedX = imageRoi.X + imageRoi.Width / 2.0;
            expectedY = imageRoi.Y + imageRoi.Height / 2.0;
        }
        else
        {
            expectedX = imageSize.Width / 2.0;
            expectedY = imageSize.Height / 2.0;
        }


        double dx = cx - expectedX;
        double dy = cy - expectedY;
        double deltaPx = Math.Sqrt(dx * dx + dy * dy);
        double deltaMm = deltaPx / pixelsPerMm;


        if (deltaMm <= thresholdMm)
            return null;

        double dxMm = dx / pixelsPerMm;
        double dyMm = dy / pixelsPerMm;

        int boxSize = 20;
        int bx = (int)Math.Round(cx - boxSize / 2.0);
        int by = (int)Math.Round(cy - boxSize / 2.0);

        return new Defect
        {
            Type = "Misalignment",
            BoundingBox = new System.Drawing.Rectangle(bx, by, boxSize, boxSize),
            Confidence = Math.Min(1.0, deltaMm / (thresholdMm * 2)),
            Description = $"Δ{deltaMm:F1}mm (dx={dxMm:F1} dy={dyMm:F1})",
        };
    }



    /// <summary>
    /// 在原图副本上绘制轮廓、旋转外接矩形、缺陷标注和测量文字，保存为 PNG 落盘并返回路径。
    /// </summary>
    private static string SaveAnnotatedImage(
        Mat src, Point[] contour, RotatedRect rect,
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<Defect> defects,
        bool isOk,
        string outputDir)
    {
        using var display = new Mat();
        if (src.Channels() == 1)
            Cv2.CvtColor(src, display, ColorConversionCodes.GRAY2BGR);
        else
            src.CopyTo(display);


        Cv2.DrawContours(display, [contour], 0, Scalar.LimeGreen, 2);


        var vertices = Cv2.BoxPoints(rect);
        var pts = Array.ConvertAll(vertices, p => new Point((int)p.X, (int)p.Y));
        Cv2.Polylines(display, [pts], isClosed: true, Scalar.DodgerBlue, 2);


        foreach (var d in defects)
        {
            var bbox = d.BoundingBox;
            var color = new Scalar(0, 140, 255);
            Cv2.Rectangle(display,
                new Rect(bbox.X, bbox.Y, bbox.Width, bbox.Height),
                color, thickness: 2);
            Cv2.PutText(display, d.Type,
                new Point(bbox.X, bbox.Y - 4),
                HersheyFonts.HersheySimplex, 0.5, color, 1);
        }


        var len = measurements.FirstOrDefault(m => m.Name == "TabLength");
        var wid = measurements.FirstOrDefault(m => m.Name == "TabWidth");
        string statusText = isOk ? "OK" : "NG";
        string measureText = $"{statusText}  L={len?.Value:F2}mm  W={wid?.Value:F2}mm";
        Scalar textColor = isOk ? Scalar.LimeGreen : Scalar.Red;
        Cv2.PutText(display, measureText, new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.8, textColor, 2);

        Directory.CreateDirectory(outputDir);
        string path = Path.Combine(outputDir, $"btv_annotated_{Guid.NewGuid():N}.png");
        Cv2.ImWrite(path, display);
        return path;
    }



    private static T GetRequired<T>(AlgorithmConfig config, string key) where T : struct
    {
        if (!config.Parameters.TryGetValue(key, out var val) || val is null)
            throw new ArgumentException($"Required parameter '{key}' is missing.", nameof(config));
        return (T)Convert.ChangeType(val, typeof(T), CultureInfo.InvariantCulture);
    }

    private static T? GetOptional<T>(AlgorithmConfig config, string key) where T : struct
    {
        if (!config.Parameters.TryGetValue(key, out var val) || val is null)
            return null;
        return (T)Convert.ChangeType(val, typeof(T), CultureInfo.InvariantCulture);
    }

    private static bool CheckTolerance(double value, double? lower, double? upper)
    {
        if (lower is null && upper is null) return true;
        if (lower is not null && value < lower.Value) return false;
        if (upper is not null && value > upper.Value) return false;
        return true;
    }

    private InspectionResult ErrorResult(Stopwatch sw, string message) =>
        new()
        {
            IsOk = false,
            EngineName = EngineName,
            ElapsedMs = sw.ElapsedMilliseconds,
            ErrorMessage = message,
            FinishedAt = DateTime.Now,
        };
}
