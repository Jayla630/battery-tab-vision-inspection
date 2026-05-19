using BatteryTabVision.Core.Models;
using BatteryTabVision.Persistence.Services;
using Xunit;

namespace BatteryTabVision.Persistence.Tests;

public class ProfileConfigServiceTests
{
    private static string WriteTempConfig(string json)
    {
        var path = Path.GetTempFileName() + ".json";
        File.WriteAllText(path, json);
        return path;
    }

    private const string ValidJson = """
    {
      "Profiles": [
        {
          "ProductModel": "TabA-12x5",
          "BinaryThreshold": 128,
          "MinContourArea": 1000,
          "PixelsPerMm": 20.0,
          "LengthLowerLimit": 11.8,
          "LengthUpperLimit": 12.2,
          "WidthLowerLimit": 4.8,
          "WidthUpperLimit": 5.2
        },
        {
          "ProductModel": "TabB-15x6",
          "BinaryThreshold": 110,
          "MinContourArea": 1200,
          "PixelsPerMm": 20.0,
          "LengthLowerLimit": 14.7,
          "LengthUpperLimit": 15.3,
          "WidthLowerLimit": 5.8,
          "WidthUpperLimit": 6.2
        }
      ]
    }
    """;

    [Fact]
    public void ProfileConfigService_LoadsCorrectProfile()
    {
        var path = WriteTempConfig(ValidJson);
        try
        {
            var service = new ProfileConfigService(path);
            var config = service.LoadProfile("TabB-15x6");

            Assert.NotNull(config);
            Assert.Equal("TabB-15x6", config!.ProductModel);
            Assert.Equal(110, (int)config.Parameters["BinaryThreshold"]);
            Assert.Equal(1200.0, (double)config.Parameters["MinContourArea"]);
            Assert.Equal(14.7, (double)config.Parameters["LengthLowerLimit"]);
            Assert.Equal(15.3, (double)config.Parameters["LengthUpperLimit"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ProfileConfigService_UnknownModel_ReturnsNull()
    {
        var path = WriteTempConfig(ValidJson);
        try
        {
            var service = new ProfileConfigService(path);
            var config = service.LoadProfile("NonExistent");

            Assert.Null(config);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ProfileConfigService_FileNotFound_ThrowsDescriptive()
    {
        var ex = Assert.Throws<FileNotFoundException>(
            () => new ProfileConfigService(@"Z:\nonexistent\path\config.json"));

        Assert.Contains(@"Z:\nonexistent\path\config.json", ex.Message);
    }

    [Fact]
    public void ProfileConfigService_AvailableModels_ReturnsAllModels()
    {
        var path = WriteTempConfig(ValidJson);
        try
        {
            var service = new ProfileConfigService(path);

            Assert.Equal(2, service.AvailableModels.Count);
            Assert.Contains("TabA-12x5", service.AvailableModels);
            Assert.Contains("TabB-15x6", service.AvailableModels);
        }
        finally { File.Delete(path); }
    }
}
