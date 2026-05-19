using System.Text.Json;
using BatteryTabVision.Core.Models;
using Xunit;

namespace BatteryTabVision.Core.Tests;

public class InspectionResultTests
{
    [Fact]
    public void Constructor_OkResult_HasExpectedValues()
    {
        var result = new InspectionResult
        {
            IsOk = true,
            EngineName = "OpenCvSharp",
            ElapsedMs = 42,
            Measurements =
            [
                new Measurement
                {
                    Name = "TabLength",
                    Value = 12.5,
                    Unit = "mm",
                    LowerLimit = 11.0,
                    UpperLimit = 14.0,
                    IsInTolerance = true,
                },
            ],
        };

        Assert.True(result.IsOk);
        Assert.Equal("OpenCvSharp", result.EngineName);
        Assert.Equal(42, result.ElapsedMs);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.Measurements);
    }

    [Fact]
    public void Constructor_FailResult_HasErrorMessage()
    {
        var result = new InspectionResult
        {
            IsOk = false,
            EngineName = "Halcon",
            ErrorMessage = "Model file not found",
        };

        Assert.False(result.IsOk);
        Assert.Equal("Model file not found", result.ErrorMessage);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesValues()
    {
        var original = new InspectionResult
        {
            IsOk = true,
            EngineName = "VisionMaster",
            ElapsedMs = 100,
            FinishedAt = new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc),
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<InspectionResult>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.IsOk, deserialized.IsOk);
        Assert.Equal(original.EngineName, deserialized.EngineName);
        Assert.Equal(original.ElapsedMs, deserialized.ElapsedMs);
    }

    [Fact]
    public void Measurements_DefaultIsEmpty()
    {
        var result = new InspectionResult { IsOk = true, EngineName = "Test" };

        Assert.Empty(result.Measurements);
        Assert.Empty(result.Defects);
    }
}

public class MeasurementTests
{
    [Fact]
    public void IsInTolerance_WithinBounds_ReturnsTrue()
    {
        var m = new Measurement
        {
            Name = "TabWidth",
            Value = 5.0,
            Unit = "mm",
            LowerLimit = 4.0,
            UpperLimit = 6.0,
            IsInTolerance = true,
        };

        Assert.True(m.IsInTolerance);
    }

    [Fact]
    public void IsInTolerance_OutOfBounds_ReturnsFalse()
    {
        var m = new Measurement
        {
            Name = "TabAngle",
            Value = 3.5,
            Unit = "deg",
            LowerLimit = 4.0,
            UpperLimit = 6.0,
            IsInTolerance = false,
        };

        Assert.False(m.IsInTolerance);
    }

    [Fact]
    public void LimitsAreOptional_NoLimitsSet()
    {
        var m = new Measurement
        {
            Name = "TabPitch",
            Value = 7.2,
            Unit = "mm",
            IsInTolerance = true,
        };

        Assert.Null(m.LowerLimit);
        Assert.Null(m.UpperLimit);
    }
}

public class VisionAlgorithmMockTests
{
    private sealed class MockVisionAlgorithm : BatteryTabVision.Core.Abstractions.IVisionAlgorithm
    {
        public string EngineName => "MockEngine";
        public bool IsInitialized { get; private set; }

        public Task InitializeAsync(AlgorithmConfig config, CancellationToken ct = default)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<InspectionResult> DetectAsync(InspectionImage image, CancellationToken ct = default)
        {
            var result = new InspectionResult
            {
                IsOk = true,
                EngineName = EngineName,
                ElapsedMs = 1,
            };
            return Task.FromResult(result);
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task MockEngine_InitializeAndDetect_Succeeds()
    {
        using var engine = new MockVisionAlgorithm();
        var config = new AlgorithmConfig { ProductModel = "TestModel" };
        var image = new InspectionImage { SourcePath = "test.bmp" };

        await engine.InitializeAsync(config);
        Assert.True(engine.IsInitialized);

        var result = await engine.DetectAsync(image);

        Assert.True(result.IsOk);
        Assert.Equal("MockEngine", result.EngineName);
    }
}
