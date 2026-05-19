using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using BatteryTabVision.Core.Abstractions;
using BatteryTabVision.Core.Models;
using HalconDotNet;

namespace BatteryTabVision.Engines.Halcon;

/// <summary>
/// <see cref="IVisionAlgorithm"/> 的 Halcon 实现。
/// 流水线：图像加载 → 灰度化 → 阈值分割 → 开运算去噪 → 连通域选最大区域 →
/// SmallestRectangle2 测量 → 错位检测 → 公差判定 → 标注图落盘。
/// </summary>
public sealed class HalconVisionAlgorithm : IVisionAlgorithm
{
    /// <inheritdoc/>
    public string EngineName => "Halcon";

    /// <inheritdoc/>
    public bool IsInitialized { get; private set; }

    private int _binaryThreshold;
    private double _minContourArea;
    private double _pixelsPerMm;
    private double? _lengthLower;
    private double? _lengthUpper;
    private double? _widthLower;
    private double? _widthUpper;
    private bool _enableMisalignment = true;
    private double _misalignmentThresholdMm = 1.0;
    private bool _enableBurrDetection = true;
    private int _minBurrClusterPoints = 3;

    private bool _disposed;

    /// <inheritdoc/>
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

        _enableMisalignment = GetOptional<bool>(config, "Defect.EnableMisalignment") ?? true;
        _misalignmentThresholdMm = GetOptional<double>(config, "Defect.MisalignmentThresholdMm") ?? 1.0;
        _enableBurrDetection = GetOptional<bool>(config, "Defect.EnableBurr") ?? true;
        _minBurrClusterPoints = GetOptional<int>(config, "Defect.MinBurrClusterPoints") ?? 3;

        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<InspectionResult> DetectAsync(InspectionImage image, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (_disposed)
                return Task.FromResult(ErrorResult(sw, "Engine has been disposed."));

            if (!IsInitialized)
                return Task.FromResult(ErrorResult(sw, "Engine not initialized — call InitializeAsync first."));

            return Task.FromResult(RunHalconPipeline(image, sw));
        }
        catch (HOperatorException ex) when (IsLicenseError(ex))
        {
            return Task.FromResult(new InspectionResult
            {
                IsOk = false,
                ErrorMessage = $"Halcon 授权不可用: {ex.Message}",
                EngineName = EngineName,
                ElapsedMs = sw.ElapsedMilliseconds,
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



    private InspectionResult RunHalconPipeline(InspectionImage image, Stopwatch sw)
    {
        if (string.IsNullOrEmpty(image.SourcePath) || !File.Exists(image.SourcePath))
            return ErrorResult(sw, $"Image file not found: '{image.SourcePath}'.");

        using var hImage = new HImage(image.SourcePath);


        using var gray = hImage.CountChannels() > 1
            ? hImage.Rgb1ToGray()
            : hImage.CopyImage();


        using var binary = gray.Threshold(0.0, (double)_binaryThreshold);


        using var opened = binary.OpeningRectangle1(3, 3);


        using var components = opened.Connection();
        using var tabRegion = components.SelectShapeStd("max_area", 70.0);


        double area = tabRegion.Area;
        double centerRow = tabRegion.Row;
        double centerCol = tabRegion.Column;
        if (area < _minContourArea)
            return ErrorResult(sw, $"No tab region found (area={area} < {_minContourArea})");


        tabRegion.SmallestRectangle2(
            out HTuple _, out HTuple __,
            out HTuple phi,
            out HTuple len1, out HTuple len2);

        double lengthMm = Math.Round(2.0 * (double)len1 / _pixelsPerMm, 4);
        double widthMm = Math.Round(2.0 * (double)len2 / _pixelsPerMm, 4);
        var measurements = BuildMeasurements(lengthMm, widthMm);


        var defects = new List<Defect>();
        if (_enableBurrDetection)
            defects.AddRange(DetectBurrsHalcon(tabRegion, 11, _minBurrClusterPoints * 5));
        if (_enableMisalignment)
        {
            hImage.GetImageSize(out HTuple imgW, out HTuple imgH);
            double expRow = image.Roi != System.Drawing.Rectangle.Empty
                ? image.Roi.Y + image.Roi.Height / 2.0
                : (double)imgH / 2.0;
            double expCol = image.Roi != System.Drawing.Rectangle.Empty
                ? image.Roi.X + image.Roi.Width / 2.0
                : (double)imgW / 2.0;

            double distMm = Math.Sqrt(
                Math.Pow((double)centerRow - expRow, 2) +
                Math.Pow((double)centerCol - expCol, 2)) / _pixelsPerMm;

            if (distMm > _misalignmentThresholdMm)
                defects.Add(new Defect
                {
                    Type = "Misalignment",
                    Confidence = Math.Min(1.0, distMm / (_misalignmentThresholdMm * 2)),
                    Description = $"Δ{distMm:F1}mm",
                    BoundingBox = new System.Drawing.Rectangle(
                        (int)(double)centerCol - 10, (int)(double)centerRow - 10, 20, 20),
                });
        }

        bool isOk = measurements.All(m =>
            m.IsInTolerance || (m.LowerLimit == null && m.UpperLimit == null))
            && defects.Count == 0;


        string outPath = SaveAnnotatedImage(
            hImage,
            (double)centerRow, (double)centerCol,
            (double)phi, (double)len1, (double)len2,
            measurements, defects, isOk);

        return new InspectionResult
        {
            IsOk = isOk,
            Measurements = measurements,
            Defects = defects.AsReadOnly(),
            AnnotatedImagePath = outPath,
            EngineName = EngineName,
            ElapsedMs = sw.ElapsedMilliseconds,
            FinishedAt = DateTime.Now,
        };
    }



    private static string SaveAnnotatedImage(
        HImage hImage,
        double centerRow, double centerCol,
        double phi, double len1, double len2,
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<Defect> defects,
        bool isOk)
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "annotated");
        Directory.CreateDirectory(dir);
        var outPath = Path.Combine(dir, $"halcon_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        var tempPath = Path.ChangeExtension(outPath, ".tmp.png");

        HOperatorSet.WriteImage(hImage, "png", 0, tempPath);
        try
        {
            using var bitmap = new Bitmap(tempPath);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;


            double cosP = Math.Cos(phi), sinP = Math.Sin(phi);
            double[] dL = { len1, -len1, -len1, len1 };
            double[] dW = { len2, len2, -len2, -len2 };
            var corners = new PointF[4];
            for (int i = 0; i < 4; i++)
                corners[i] = new PointF(
                    (float)(centerCol + dL[i] * cosP - dW[i] * sinP),
                    (float)(centerRow + dL[i] * sinP + dW[i] * cosP));


            using var rectPen = new Pen(
                isOk ? Color.DeepSkyBlue : Color.Red, 2);
            g.DrawPolygon(rectPen, corners);


            using var defectPen = new Pen(Color.Orange, 2);
            using var defectBrush = new SolidBrush(Color.Orange);
            using var defectFont = new Font("Arial", 10, GraphicsUnit.Pixel);
            foreach (var d in defects)
            {
                g.DrawRectangle(defectPen,
                    d.BoundingBox.X, d.BoundingBox.Y,
                    d.BoundingBox.Width, d.BoundingBox.Height);
                g.DrawString(d.Type, defectFont, defectBrush,
                    d.BoundingBox.X, d.BoundingBox.Y - 14);
            }


            var len = measurements.FirstOrDefault(m => m.Name == "TabLength");
            var wid = measurements.FirstOrDefault(m => m.Name == "TabWidth");
            string txt = $"{(isOk ? "OK" : "NG")} | L={len?.Value:F2}mm W={wid?.Value:F2}mm | Halcon";
            using var statusFont = new Font("Arial", 18, FontStyle.Bold, GraphicsUnit.Pixel);
            using var statusBrush = new SolidBrush(
                isOk ? Color.Lime : Color.Red);
            g.DrawString(txt, statusFont, statusBrush, new PointF(8, 8));

            bitmap.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        return outPath;
    }



    private IReadOnlyList<Measurement> BuildMeasurements(double lengthMm, double widthMm)
    {
        return new[]
        {
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
        };
    }



    private InspectionResult ErrorResult(Stopwatch sw, string msg) => new()
    {
        IsOk = false,
        ErrorMessage = msg,
        EngineName = EngineName,
        ElapsedMs = sw.ElapsedMilliseconds,
        FinishedAt = DateTime.Now,
    };

    private static bool IsLicenseError(HOperatorException ex) =>
        ex.Message.Contains("2042") ||
        ex.Message.Contains("expired") ||
        ex.Message.Contains("license") ||
        ex.Message.Contains("License");

    private static bool CheckTolerance(double value, double? lower, double? upper)
    {
        if (lower is null && upper is null) return true;
        if (lower is not null && value < lower.Value) return false;
        if (upper is not null && value > upper.Value) return false;
        return true;
    }

    private static IReadOnlyList<Defect> DetectBurrsHalcon(
        HRegion tabRegion, int morphKernelSize, int minBurrAreaPx)
    {
        var defects = new List<Defect>();

        using var cleanRegion = tabRegion.OpeningRectangle1(morphKernelSize, morphKernelSize);

        using var burrRegion = tabRegion.Difference(cleanRegion);

        using var components = burrRegion.Connection();
        int count = (int)components.CountObj();

        for (int i = 1; i <= count; i++)
        {
            using var component = components.SelectObj(i);
            double area = component.Area;

            if (area < minBurrAreaPx)
                continue;

            component.SmallestRectangle1(
                out HTuple r1, out HTuple c1,
                out HTuple r2, out HTuple c2);

            defects.Add(new Defect
            {
                Type = "Burr",
                Confidence = 1.0,
                BoundingBox = new System.Drawing.Rectangle(
                    (int)(double)c1,
                    (int)(double)r1,
                    (int)((double)c2 - (double)c1),
                    (int)((double)r2 - (double)r1)),
                Description = $"Area={area:F0}px²"
            });
        }

        return defects;
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
}
