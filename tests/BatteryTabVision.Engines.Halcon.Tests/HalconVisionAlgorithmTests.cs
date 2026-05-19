using BatteryTabVision.Core.Abstractions;
using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;
using BatteryTabVision.Engines.OpenCv.Samples;
using Moq;
using Xunit;

namespace BatteryTabVision.Engines.Halcon.Tests;

/// <summary>
/// HalconVisionAlgorithm 测试套件。
/// HALCONROOT 未设置时自动跳过，CI 环境不报错。
/// </summary>
public class HalconVisionAlgorithmTests : IDisposable
{
    private const int TabLengthPx = 400;
    private const int TabWidthPx = 100;
    private const double PixelsPerMm = 20.0;
    private const double ExpectedLengthMm = TabLengthPx / PixelsPerMm;
    private const double ExpectedWidthMm = TabWidthPx / PixelsPerMm;

    private static readonly bool HalconAvailable =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HALCONROOT"));

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



    [SkippableFact]
    public async Task LifecycleTest()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        Assert.False(engine.IsInitialized, "Before Init: IsInitialized should be false");

        await engine.InitializeAsync(BaseConfig());
        Assert.True(engine.IsInitialized, "After Init: IsInitialized should be true");

        engine.Dispose();

        var (path, _, _) = GenImage();
        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });
        Assert.False(result.IsOk);
        Assert.NotNull(result.ErrorMessage);
    }



    [SkippableFact]
    public async Task InitWithoutBinaryThreshold_ThrowsArgumentException()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
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

    [SkippableFact]
    public async Task InitWithoutMinContourArea_ThrowsArgumentException()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
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

    [SkippableFact]
    public async Task InitWithoutPixelsPerMm_ThrowsArgumentException()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
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



    [SkippableFact]
    public async Task DetectWithoutInit_ReturnsErrorResult()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.False(result.IsOk);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal("Halcon", result.EngineName);
    }



    [SkippableFact]
    public async Task DetectOnInvalidPath_ReturnsErrorResult()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = "/nonexistent/image.png" });

        Assert.False(result.IsOk);
        Assert.NotNull(result.ErrorMessage);
    }



    [SkippableFact]
    public async Task SyntheticImage_LengthMeasurementWithin2Percent()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        var length = result.Measurements.First(m => m.Name == "TabLength");
        double error = Math.Abs(length.Value - ExpectedLengthMm) / ExpectedLengthMm;
        Assert.True(error < 0.02, $"Length error {error:P1} >= 2%. Measured={length.Value}mm, Expected={ExpectedLengthMm}mm");
    }

    [SkippableFact]
    public async Task SyntheticImage_WidthMeasurementWithin2Percent()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        var width = result.Measurements.First(m => m.Name == "TabWidth");
        double error = Math.Abs(width.Value - ExpectedWidthMm) / ExpectedWidthMm;
        Assert.True(error < 0.02, $"Width error {error:P1} >= 2%. Measured={width.Value}mm, Expected={ExpectedWidthMm}mm");
    }



    [SkippableFact]
    public async Task ToleranceInRange_ReturnsIsOkTrue()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(
            lengthLower: 19.5, lengthUpper: 20.5,
            widthLower: 4.8, widthUpper: 5.2));
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        Assert.All(result.Measurements, m =>
            Assert.True(m.IsInTolerance, $"Measurement '{m.Name}'={m.Value} should be in tolerance"));
    }



    [SkippableFact]
    public async Task ToleranceOutOfRange_ReturnsIsOkFalse()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(lengthLower: 25.0, lengthUpper: 30.0));
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.False(result.IsOk, "Expected IsOk=false when measurement is out of tolerance");
        var length = result.Measurements.First(m => m.Name == "TabLength");
        Assert.False(length.IsInTolerance, "TabLength.IsInTolerance should be false");
    }



    [SkippableFact]
    public async Task NoToleranceSpecified_IsOkAlwaysTrue()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true when no limits set. ErrorMessage: {result.ErrorMessage}");
    }



    [SkippableFact]
    public async Task AnnotatedImageFileExists_AfterSuccessfulDetect()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
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



    [SkippableFact]
    public async Task ElapsedMsIsPositive()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.ElapsedMs >= 0,
            $"ElapsedMs should be non-negative, got {result.ElapsedMs}");
    }



    [SkippableFact]
    public async Task HalconEngine_ReturnsCorrectEngineName()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage();

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.Equal("Halcon", result.EngineName);
    }



    [SkippableFact]
    public async Task MisalignedImage_WithHalcon_ReportsMisalignment()
    {
        Skip.IfNot(HalconAvailable, "HALCONROOT not set");

        using var engine = new HalconVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = SyntheticTabImageGenerator.GenerateWithDefect(
            tabLengthPx: TabLengthPx, tabWidthPx: TabWidthPx,
            defect: SyntheticDefect.Misalignment, misalignmentPx: 60, noiseStdDev: 5.0);
        _tempFiles.Add(path);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.Contains(result.Defects, d => d.Type == "Misalignment");
        Assert.False(result.IsOk, "Misaligned image should be NG");
    }
}
