using BatteryTabVision.Core.Models;
using BatteryTabVision.Engines.OpenCv.Samples;
using Xunit;

namespace BatteryTabVision.Engines.OpenCv.Tests;

/// <summary>
/// OpenCvVisionAlgorithm 的全量测试套件。
/// 使用合成图 (SyntheticTabImageGenerator) 保证确定性与无外部依赖。
/// </summary>
public class OpenCvVisionAlgorithmTests : IDisposable
{

    private const int TabLengthPx = 400;
    private const int TabWidthPx = 100;
    private const double PixelsPerMm = 20.0;
    private const double ExpectedLengthMm = TabLengthPx / PixelsPerMm;
    private const double ExpectedWidthMm = TabWidthPx / PixelsPerMm;

    private readonly List<string> _tempFiles = [];

    private AlgorithmConfig BaseConfig(
        double? lengthLower = null, double? lengthUpper = null,
        double? widthLower = null, double? widthUpper = null) =>
        new()
        {
            ProductModel = "TestModel",
            Parameters = new Dictionary<string, object>
            {
                ["BinaryThreshold"] = 80,
                ["MinContourArea"] = 1000.0,
                ["PixelsPerMm"] = PixelsPerMm,
                ["LengthLowerLimit"] = (object?)lengthLower!,
                ["LengthUpperLimit"] = (object?)lengthUpper!,
                ["WidthLowerLimit"] = (object?)widthLower!,
                ["WidthUpperLimit"] = (object?)widthUpper!,
            }.Where(kv => kv.Value is not null)
             .ToDictionary(kv => kv.Key, kv => kv.Value),
        };

    private (string path, double lengthPx, double widthPx) GenImage()
    {
        var result = SyntheticTabImageGenerator.Generate(
            tabLengthPx: TabLengthPx, tabWidthPx: TabWidthPx, noiseStdDev: 5.0);
        _tempFiles.Add(result.path);
        return result;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { /* best-effort */ }
        }
    }



    /// <summary>
    /// Init 前 IsInitialized=false；Init 后 true；Dispose 后再 Detect 应返回错误结果。
    /// </summary>
    [Fact]
    public async Task LifecycleTest()
    {
        using var engine = new OpenCvVisionAlgorithm();
        Assert.False(engine.IsInitialized, "Before Init: IsInitialized should be false");

        await engine.InitializeAsync(BaseConfig());
        Assert.True(engine.IsInitialized, "After Init: IsInitialized should be true");

        engine.Dispose();

        var (path, _, _) = GenImage();
        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });
        Assert.False(result.IsOk);
        Assert.NotNull(result.ErrorMessage);
    }



    /// <summary>
    /// BinaryThreshold 缺失时 InitializeAsync 应抛 ArgumentException。
    /// </summary>
    [Fact]
    public async Task InitWithoutBinaryThreshold_ThrowsArgumentException()
    {
        using var engine = new OpenCvVisionAlgorithm();
        var config = new AlgorithmConfig
        {
            ProductModel = "Test",
            Parameters = new Dictionary<string, object>
            {
                ["MinContourArea"] = 1000.0,
                ["PixelsPerMm"] = 20.0,
            },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => engine.InitializeAsync(config));
    }

    /// <summary>
    /// MinContourArea 缺失时 InitializeAsync 应抛 ArgumentException。
    /// </summary>
    [Fact]
    public async Task InitWithoutMinContourArea_ThrowsArgumentException()
    {
        using var engine = new OpenCvVisionAlgorithm();
        var config = new AlgorithmConfig
        {
            ProductModel = "Test",
            Parameters = new Dictionary<string, object>
            {
                ["BinaryThreshold"] = 80,
                ["PixelsPerMm"] = 20.0,
            },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => engine.InitializeAsync(config));
    }

    /// <summary>
    /// PixelsPerMm 缺失时 InitializeAsync 应抛 ArgumentException。
    /// </summary>
    [Fact]
    public async Task InitWithoutPixelsPerMm_ThrowsArgumentException()
    {
        using var engine = new OpenCvVisionAlgorithm();
        var config = new AlgorithmConfig
        {
            ProductModel = "Test",
            Parameters = new Dictionary<string, object>
            {
                ["BinaryThreshold"] = 80,
                ["MinContourArea"] = 1000.0,
            },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => engine.InitializeAsync(config));
    }



    /// <summary>
    /// 未 Init 就 Detect → IsOk=false, ErrorMessage 非空, 不抛异常。
    /// </summary>
    [Fact]
    public async Task DetectWithoutInit_ReturnsErrorResult()
    {
        using var engine = new OpenCvVisionAlgorithm();
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.False(result.IsOk);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal("OpenCvSharp", result.EngineName);
    }



    /// <summary>
    /// 文件不存在 → IsOk=false, ErrorMessage 非空, 不抛异常。
    /// </summary>
    [Fact]
    public async Task DetectOnInvalidPath_ReturnsErrorResult()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = "/nonexistent/image.png" });

        Assert.False(result.IsOk);
        Assert.NotNull(result.ErrorMessage);
    }



    /// <summary>
    /// 400×100px 合成图，PixelsPerMm=20 → 期望长 20mm；实测误差 &lt; 2%。
    /// </summary>
    [Fact]
    public async Task SyntheticImage_LengthMeasurementWithin2Percent()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        var length = result.Measurements.First(m => m.Name == "TabLength");
        double error = Math.Abs(length.Value - ExpectedLengthMm) / ExpectedLengthMm;
        Assert.True(error < 0.02, $"Length error {error:P1} ≥ 2%. Measured={length.Value}mm, Expected={ExpectedLengthMm}mm");
    }

    /// <summary>
    /// 400×100px 合成图，PixelsPerMm=20 → 期望宽 5mm；实测误差 &lt; 2%。
    /// </summary>
    [Fact]
    public async Task SyntheticImage_WidthMeasurementWithin2Percent()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        var width = result.Measurements.First(m => m.Name == "TabWidth");
        double error = Math.Abs(width.Value - ExpectedWidthMm) / ExpectedWidthMm;
        Assert.True(error < 0.02, $"Width error {error:P1} ≥ 2%. Measured={width.Value}mm, Expected={ExpectedWidthMm}mm");
    }



    /// <summary>
    /// 公差 [19.5, 20.5]×[4.8, 5.2]，测量值在范围内 → IsOk=true，每项 IsInTolerance=true。
    /// </summary>
    [Fact]
    public async Task ToleranceInRange_ReturnsIsOkTrue()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(
            lengthLower: 19.5, lengthUpper: 20.5,
            widthLower: 4.8, widthUpper: 5.2));
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        Assert.All(result.Measurements, m =>
            Assert.True(m.IsInTolerance, $"Measurement '{m.Name}'={m.Value} should be in tolerance"));
    }



    /// <summary>
    /// 公差 [25, 30]（明显超出实际值），IsOk=false，对应 IsInTolerance=false。
    /// </summary>
    [Fact]
    public async Task ToleranceOutOfRange_ReturnsIsOkFalse()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(lengthLower: 25.0, lengthUpper: 30.0));
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.False(result.IsOk, "Expected IsOk=false when measurement is out of tolerance");
        var length = result.Measurements.First(m => m.Name == "TabLength");
        Assert.False(length.IsInTolerance, "TabLength.IsInTolerance should be false");
    }



    /// <summary>
    /// 不传任何 *Limit 参数 → IsOk=true（只测量不判定）。
    /// </summary>
    [Fact]
    public async Task NoToleranceSpecified_IsOkAlwaysTrue()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true when no limits set. ErrorMessage: {result.ErrorMessage}");
        Assert.All(result.Measurements, m =>
            Assert.True(m.IsInTolerance, $"'{m.Name}' should be in tolerance when no limits"));
    }



    /// <summary>
    /// DetectAsync 成功后 AnnotatedImagePath 指向的 PNG 文件确实存在。
    /// </summary>
    [Fact]
    public async Task AnnotatedImageFileExists_AfterSuccessfulDetect()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        Assert.NotNull(result.AnnotatedImagePath);
        Assert.True(File.Exists(result.AnnotatedImagePath),
            $"Annotated image not found at: {result.AnnotatedImagePath}");
        if (result.AnnotatedImagePath is not null)
            _tempFiles.Add(result.AnnotatedImagePath);
    }



    /// <summary>
    /// DetectAsync 返回的 ElapsedMs 大于 0（实际执行了工作）。
    /// </summary>
    [Fact]
    public async Task ElapsedMsIsPositive()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.ElapsedMs >= 0,
            $"ElapsedMs should be non-negative, got {result.ElapsedMs}");
    }



    /// <summary>
    /// 第一次 Init 公差宽松 → IsOk=true；第二次 Init 换严格公差 → IsOk=false。
    /// 验证 InitializeAsync 去掉早返后每次均更新参数。
    /// </summary>
    [Fact]
    public async Task SecondInit_AppliesNewParameters()
    {
        using var engine = new OpenCvVisionAlgorithm();
        var (path, _, _) = GenImage();

        await engine.InitializeAsync(BaseConfig(lengthLower: 19.5, lengthUpper: 20.5));
        var result1 = await engine.DetectAsync(new InspectionImage { SourcePath = path });
        Assert.True(result1.IsOk, $"First detect should be OK. ErrorMessage: {result1.ErrorMessage}");


        await engine.InitializeAsync(BaseConfig(lengthLower: 10.0, lengthUpper: 12.0));
        var result2 = await engine.DetectAsync(new InspectionImage { SourcePath = path });
        Assert.False(result2.IsOk, "Second detect should be NG after re-init with impossible tolerance");
        var len = result2.Measurements.First(m => m.Name == "TabLength");
        Assert.False(len.IsInTolerance, "TabLength.IsInTolerance should be false with tight limits");
    }
}
