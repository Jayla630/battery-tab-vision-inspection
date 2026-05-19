using System.Text.Json;
using System.Text.Json.Serialization;
using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;

namespace BatteryTabVision.Persistence.Services;

/// <summary>从 JSON 文件加载算法参数配置。</summary>
public sealed class ProfileConfigService : IProfileConfigService
{
    private readonly List<AlgorithmProfileEntry> _profiles;
    private readonly string _configFilePath;

    public ProfileConfigService(string configFilePath)
    {
        _configFilePath = configFilePath;

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
                ["Defect.BurrThresholdPx"] = entry.BurrThresholdPx,
                ["Defect.MinBurrClusterPoints"] = entry.MinBurrClusterPoints,
                ["Defect.EnableBurr"] = entry.EnableBurr,
                ["Defect.MisalignmentThresholdMm"] = entry.MisalignmentThresholdMm,
                ["Defect.EnableMisalignment"] = entry.EnableMisalignment,
            },
        };

        if (!string.IsNullOrWhiteSpace(entry.AnnotatedOutputDir))
            config.Parameters["AnnotatedOutputDir"] = entry.AnnotatedOutputDir;

        return config;
    }

    public void UpsertProfile(string productModel, IDictionary<string, object> parameters)
    {
        var entry = _profiles.FirstOrDefault(p => p.ProductModel == productModel);
        if (entry == null)
        {
            entry = new AlgorithmProfileEntry { ProductModel = productModel };
            _profiles.Add(entry);
        }

        entry.BinaryThreshold = GetInt(parameters, nameof(entry.BinaryThreshold), entry.BinaryThreshold);
        entry.MinContourArea = GetDouble(parameters, nameof(entry.MinContourArea), entry.MinContourArea);
        entry.PixelsPerMm = GetDouble(parameters, nameof(entry.PixelsPerMm), entry.PixelsPerMm);
        entry.LengthLowerLimit = GetDouble(parameters, nameof(entry.LengthLowerLimit), entry.LengthLowerLimit);
        entry.LengthUpperLimit = GetDouble(parameters, nameof(entry.LengthUpperLimit), entry.LengthUpperLimit);
        entry.WidthLowerLimit = GetDouble(parameters, nameof(entry.WidthLowerLimit), entry.WidthLowerLimit);
        entry.WidthUpperLimit = GetDouble(parameters, nameof(entry.WidthUpperLimit), entry.WidthUpperLimit);
        entry.BurrThresholdPx = GetDouble(parameters, nameof(entry.BurrThresholdPx), entry.BurrThresholdPx);
        entry.MinBurrClusterPoints = GetInt(parameters, nameof(entry.MinBurrClusterPoints), entry.MinBurrClusterPoints);
        entry.EnableBurr = GetBool(parameters, nameof(entry.EnableBurr), entry.EnableBurr);
        entry.MisalignmentThresholdMm = GetDouble(parameters, nameof(entry.MisalignmentThresholdMm), entry.MisalignmentThresholdMm);
        entry.EnableMisalignment = GetBool(parameters, nameof(entry.EnableMisalignment), entry.EnableMisalignment);

        SaveToFile();
    }

    public void DeleteProfile(string productModel)
    {
        var entry = _profiles.FirstOrDefault(p => p.ProductModel == productModel);
        if (entry != null)
        {
            _profiles.Remove(entry);
            SaveToFile();
        }
    }

    private void SaveToFile()
    {
        var config = new AlgorithmProfileConfig { Profiles = _profiles };
        var json = JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configFilePath, json);
    }

    private static int GetInt(IDictionary<string, object> dict, string key, int fallback)
    {
        if (dict.TryGetValue(key, out var v))
        {
            if (v is int i) return i;
            if (v is double d) return (int)d;
            if (v is long l) return (int)l;
        }
        return fallback;
    }

    private static double GetDouble(IDictionary<string, object> dict, string key, double fallback)
    {
        if (dict.TryGetValue(key, out var v))
        {
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is long l) return l;
        }
        return fallback;
    }

    private static bool GetBool(IDictionary<string, object> dict, string key, bool fallback)
    {
        if (dict.TryGetValue(key, out var v) && v is bool b)
            return b;
        return fallback;
    }
}

/// <summary>JSON 配置中的单个型号条目（仅 Persistence 内部使用）。</summary>
internal sealed class AlgorithmProfileEntry
{
    public string ProductModel { get; set; } = "";
    public int BinaryThreshold { get; set; } = 128;
    public double MinContourArea { get; set; } = 1000;
    public double PixelsPerMm { get; set; } = 20.0;
    public double LengthLowerLimit { get; set; }
    public double LengthUpperLimit { get; set; }
    public double WidthLowerLimit { get; set; }
    public double WidthUpperLimit { get; set; }

    [JsonPropertyName("Defect.BurrThresholdPx")]
    public double BurrThresholdPx { get; set; } = 3.0;

    [JsonPropertyName("Defect.MinBurrClusterPoints")]
    public int MinBurrClusterPoints { get; set; } = 3;

    [JsonPropertyName("Defect.EnableBurr")]
    public bool EnableBurr { get; set; } = true;

    [JsonPropertyName("Defect.MisalignmentThresholdMm")]
    public double MisalignmentThresholdMm { get; set; } = 1.0;

    [JsonPropertyName("Defect.EnableMisalignment")]
    public bool EnableMisalignment { get; set; } = true;

    public string? AnnotatedOutputDir { get; set; }
}

/// <summary>JSON 配置文件顶层结构（仅 Persistence 内部使用）。</summary>
internal sealed class AlgorithmProfileConfig
{
    public List<AlgorithmProfileEntry> Profiles { get; init; } = new();
}
