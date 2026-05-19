namespace BatteryTabVision.Core.Models;

/// <summary>
/// 初始化引擎时传入的算法配置。
/// </summary>
public sealed class AlgorithmConfig
{
    /// <summary>产品型号标识符，用于区分不同产品的参数集。</summary>
    public required string ProductModel { get; init; }

    /// <summary>引擎专属键值参数（阈值、ROI 偏移量等）。</summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>模型文件路径（如 Halcon 形状模板）；不需要时为 null。</summary>
    public string? ModelFilePath { get; init; }
}
