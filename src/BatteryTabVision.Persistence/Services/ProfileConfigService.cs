using System.Text.Json;
using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;

namespace BatteryTabVision.Persistence.Services;

/// <summary>从 JSON 文件加载算法参数配置。</summary>
public sealed class ProfileConfigService : IProfileConfigService
{
    private readonly List<AlgorithmProfileEntry> _profiles;

    public ProfileConfigService(string configFilePath)
    {
        if (!File.Exists(configFilePath))
            throw new FileNotFoundException(
                $"算法配置文件不存在：{configFilePath}\n请确认 config/algorithm-profiles.json 已正确部署。",
                configFilePath);

        var json = File.ReadAllText(configFilePath);
        var config = JsonSerializer.Deserialize<AlgorithmProfileConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("配置文件格式错误");

        _profiles = config.Profiles;
    }

    public IReadOnlyList<string> AvailableModels =>
        _profiles.Select(p => p.ProductModel).ToList();

    public AlgorithmConfig? LoadProfile(string productModel)
    {
        var entry = _profiles.FirstOrDefault(p => p.ProductModel == productModel);
        if (entry == null) return null;

        var config = new AlgorithmConfig
        {
            ProductModel = entry.ProductModel,
            Parameters = new Dictionary<string, object>
            {
                ["BinaryThreshold"] = entry.BinaryThreshold,
                ["MinContourArea"] = entry.MinContourArea,
                ["PixelsPerMm"] = entry.PixelsPerMm,
                ["LengthLowerLimit"] = entry.LengthLowerLimit,
                ["LengthUpperLimit"] = entry.LengthUpperLimit,
                ["WidthLowerLimit"] = entry.WidthLowerLimit,
                ["WidthUpperLimit"] = entry.WidthUpperLimit,
            },
        };

        if (!string.IsNullOrWhiteSpace(entry.AnnotatedOutputDir))
            config.Parameters["AnnotatedOutputDir"] = entry.AnnotatedOutputDir;

        return config;
    }
}

/// <summary>JSON 配置中的单个型号条目（仅 Persistence 内部使用）。</summary>
internal sealed class AlgorithmProfileEntry
{
    public string ProductModel { get; init; } = "";
    public int BinaryThreshold { get; init; } = 128;
    public double MinContourArea { get; init; } = 1000;
    public double PixelsPerMm { get; init; } = 20.0;
    public double LengthLowerLimit { get; init; }
    public double LengthUpperLimit { get; init; }
    public double WidthLowerLimit { get; init; }
    public double WidthUpperLimit { get; init; }
    public string? AnnotatedOutputDir { get; init; }
}

/// <summary>JSON 配置文件顶层结构（仅 Persistence 内部使用）。</summary>
internal sealed class AlgorithmProfileConfig
{
    public List<AlgorithmProfileEntry> Profiles { get; init; } = new();
}
