using System.Drawing;
using System.Text.Json;
using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;
using BatteryTabVision.Engines.OpenCv.Samples;
using Moq;
using Xunit;

namespace BatteryTabVision.Engines.OpenCv.Tests;

/// <summary>
/// M3b 缺陷检测（毛刺 + 错位）测试套件。
/// 使用合成缺陷注入图像验证 DetectBurrs / DetectMisalignment 逻辑。
/// </summary>
public class DefectDetectionTests : IDisposable
{
    private const int TabLengthPx = 400;
    private const int TabWidthPx = 100;
    private const double PixelsPerMm = 20.0;

    private readonly List<string> _tempFiles = [];

    private AlgorithmConfig BaseConfig(
        bool enableBurr = true,
        bool enableMisalignment = true,
        double burrThresholdPx = 3.0,
        int minBurrClusterPoints = 5,
        double misalignmentThresholdMm = 1.0) =>
        new()
        {
            ProductModel = "TestModel",
            Parameters = new Dictionary<string, object>
            {
                ["BinaryThreshold"] = 80,
                ["MinContourArea"] = 1000.0,
                ["PixelsPerMm"] = PixelsPerMm,
                ["Defect.EnableBurr"] = enableBurr,
                ["Defect.BurrThresholdPx"] = burrThresholdPx,
                ["Defect.MinBurrClusterPoints"] = minBurrClusterPoints,
                ["Defect.EnableMisalignment"] = enableMisalignment,
                ["Defect.MisalignmentThresholdMm"] = misalignmentThresholdMm,
            },
        };

    private (string path, double lengthPx, double widthPx) GenImage(
        SyntheticDefect defect = SyntheticDefect.None,
        int burrCount = 3, int burrHeightPx = 18,
        int misalignmentPx = 60,
        double noiseStdDev = 5.0)
    {
        var result = SyntheticTabImageGenerator.GenerateWithDefect(
            tabLengthPx: TabLengthPx, tabWidthPx: TabWidthPx, noiseStdDev: noiseStdDev,
            defect: defect, burrCount: burrCount, burrHeightPx: burrHeightPx,
            misalignmentPx: misalignmentPx);
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
    /// Clean image → no defects detected, IsOk=true.
    /// </summary>
    [Fact]
    public async Task CleanImage_NoDefectsDetected()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage(SyntheticDefect.None, noiseStdDev: 5.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.IsOk, $"Expected IsOk=true. ErrorMessage: {result.ErrorMessage}");
        Assert.Empty(result.Defects);
    }



    /// <summary>
    /// Burr injection with 3 burrs → at least 1 Burr defect detected, IsOk=false.
    /// </summary>
    [Fact]
    public async Task BurrImage_BurrDefectReported()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage(SyntheticDefect.Burr, burrCount: 3, burrHeightPx: 18, noiseStdDev: 2.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.False(result.IsOk, $"Burr image should be NG. Defects found: {result.Defects.Count}. ErrorMessage: {result.ErrorMessage}");
        Assert.Contains(result.Defects, d => d.Type == "Burr");
        var burr = result.Defects.First(d => d.Type == "Burr");
        Assert.True(burr.BoundingBox.Width > 0, "Burr bounding box should have positive width");
        Assert.True(burr.Confidence > 0);
    }



    /// <summary>
    /// misalignmentPx=60, PixelsPerMm=20 → offset 3mm >> threshold 1mm → detected.
    /// </summary>
    [Fact]
    public async Task MisalignedImage_MisalignmentDefectReported()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(misalignmentThresholdMm: 1.0));
        var (path, _, _) = GenImage(SyntheticDefect.Misalignment, misalignmentPx: 60, noiseStdDev: 5.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.Defects.Any(d => d.Type == "Misalignment"),
            "Misalignment defect should be detected");
        var misalign = result.Defects.First(d => d.Type == "Misalignment");
        Assert.NotNull(misalign.Description);
        Assert.True(misalign.Confidence > 0);
        Assert.False(result.IsOk, "Misaligned image should be NG");
    }



    /// <summary>
    /// misalignmentPx=10 → 0.5mm < threshold 1mm → no misalignment defect.
    /// </summary>
    [Fact]
    public async Task SlightlyMisaligned_WithinThreshold_IsOk()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(misalignmentThresholdMm: 1.0));
        var (path, _, _) = GenImage(SyntheticDefect.Misalignment, misalignmentPx: 10, noiseStdDev: 5.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.DoesNotContain(result.Defects, d => d.Type == "Misalignment");

        Assert.True(result.IsOk, $"Expected IsOk with small misalignment. ErrorMessage: {result.ErrorMessage}");
    }







    /// <summary>
    /// Burr image → dimensions still measured within 6% error.
    /// </summary>
    [Fact]
    public async Task BurrImage_DimensionsMeasuredCorrectly()
    {
        const double toleranceWithBurr = 0.06;

        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage(SyntheticDefect.Burr, burrCount: 3, burrHeightPx: 18, noiseStdDev: 2.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        double expectedLengthMm = TabLengthPx / PixelsPerMm;
        double expectedWidthMm = TabWidthPx / PixelsPerMm;

        var length = result.Measurements.First(m => m.Name == "TabLength");
        var width = result.Measurements.First(m => m.Name == "TabWidth");

        double lengthError = Math.Abs(length.Value - expectedLengthMm) / expectedLengthMm;
        double widthError = Math.Abs(width.Value - expectedWidthMm) / expectedWidthMm;

        Assert.True(lengthError < toleranceWithBurr,
            $"Length error {lengthError:P1} ≥ {toleranceWithBurr:P0}. Measured={length.Value}mm, Expected={expectedLengthMm}mm");
        Assert.True(widthError < toleranceWithBurr,
            $"Width error {widthError:P1} ≥ {toleranceWithBurr:P0}. Measured={width.Value}mm, Expected={expectedWidthMm}mm");
    }



    /// <summary>
    /// EnableBurr=false → even with burr injection, no Burr defect reported.
    /// </summary>
    [Fact]
    public async Task BurrDetectionDisabled_BurrNotReported()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(enableBurr: false));
        var (path, _, _) = GenImage(SyntheticDefect.Burr, burrCount: 3, burrHeightPx: 18, noiseStdDev: 2.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.DoesNotContain(result.Defects, d => d.Type == "Burr");
        Assert.True(result.IsOk, "With burr detection disabled, image should be OK");
    }



    /// <summary>
    /// Both defect types injected → both reported.
    /// </summary>
    [Fact]
    public async Task BothDefects_BothReported()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage(SyntheticDefect.Both, burrCount: 3, burrHeightPx: 18, misalignmentPx: 60, noiseStdDev: 2.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.Contains(result.Defects, d => d.Type == "Burr");
        Assert.Contains(result.Defects, d => d.Type == "Misalignment");
        Assert.False(result.IsOk);
    }



    /// <summary>
    /// Detection with burr → annotated image is created and file exists.
    /// </summary>
    [Fact]
    public async Task BurrImage_AnnotatedImageCreated()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage(SyntheticDefect.Burr, burrCount: 3, burrHeightPx: 18, noiseStdDev: 2.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.NotNull(result.AnnotatedImagePath);
        Assert.True(File.Exists(result.AnnotatedImagePath),
            $"Annotated image not found at: {result.AnnotatedImagePath}");
        if (result.AnnotatedImagePath is not null)
            _tempFiles.Add(result.AnnotatedImagePath);
    }



    /// <summary>
    /// Very high burrThresholdPx (e.g. 50px) → small burrs (18px) not detected.
    /// </summary>
    [Fact]
    public async Task HighBurrThreshold_SmallBurrsNotDetected()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(burrThresholdPx: 50.0));
        var (path, _, _) = GenImage(SyntheticDefect.Burr, burrCount: 3, burrHeightPx: 18, noiseStdDev: 2.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.DoesNotContain(result.Defects, d => d.Type == "Burr");
        Assert.True(result.IsOk);
    }



    /// <summary>
    /// EnableMisalignment=false → even with large offset, no misalignment defect.
    /// </summary>
    [Fact]
    public async Task MisalignmentDetectionDisabled_NotReported()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(enableMisalignment: false));
        var (path, _, _) = GenImage(SyntheticDefect.Misalignment, misalignmentPx: 60, noiseStdDev: 5.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.DoesNotContain(result.Defects, d => d.Type == "Misalignment");
        Assert.True(result.IsOk);
    }



    /// <summary>
    /// Multiple burrs on image → DefectCount matches.
    /// </summary>
    [Fact]
    public async Task MultipleBurrs_DefectCountCorrect()
    {
        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig(burrThresholdPx: 2.0, minBurrClusterPoints: 3));
        var (path, _, _) = GenImage(SyntheticDefect.Burr, burrCount: 5, burrHeightPx: 20, noiseStdDev: 1.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });

        Assert.True(result.Defects.Count >= 1,
            $"Expected at least 1 burr defect, got {result.Defects.Count}");
        Assert.All(result.Defects.Where(d => d.Type == "Burr"),
            d => Assert.True(d.BoundingBox.Width > 0));
    }



    /// <summary>
    /// Detection with defects → InspectionRecord contains DefectCount > 0 and DefectTypesJson.
    /// </summary>
    [Fact]
    public async Task DetectWithDefects_SavesDefectCountToDb()
    {

        var mockRepo = new Mock<IInspectionRepository>();
        InspectionRecord? savedRecord = null;
        mockRepo.Setup(r => r.SaveAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
            .Callback<InspectionRecord, CancellationToken>((r, _) => savedRecord = r)
            .Returns(Task.CompletedTask);

        using var engine = new OpenCvVisionAlgorithm();
        await engine.InitializeAsync(BaseConfig());
        var (path, _, _) = GenImage(SyntheticDefect.Burr, burrCount: 3, burrHeightPx: 18, noiseStdDev: 2.0);

        var result = await engine.DetectAsync(new InspectionImage { SourcePath = path });


        var record = new InspectionRecord
        {
            ProductModel = "Test",
            EngineName = result.EngineName,
            IsOk = result.IsOk,
            ElapsedMs = result.ElapsedMs,
            DefectCount = result.Defects.Count,
            DefectTypesJson = result.Defects.Count > 0
                ? JsonSerializer.Serialize(
                    result.Defects.Select(d => d.Type).Distinct().ToList())
                : null,
            AlgorithmParamsJson = "{}",
        };
        await mockRepo.Object.SaveAsync(record);


        Assert.NotNull(savedRecord);
        Assert.True(savedRecord!.DefectCount > 0, "DefectCount should be > 0 when defects present");
        Assert.NotNull(savedRecord.DefectTypesJson);
        var types = JsonSerializer.Deserialize<List<string>>(savedRecord.DefectTypesJson!);
        Assert.NotNull(types);
        Assert.Contains("Burr", types);
    }
}
