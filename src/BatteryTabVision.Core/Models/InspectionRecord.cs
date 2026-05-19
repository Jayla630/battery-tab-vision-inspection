namespace BatteryTabVision.Core.Models;

/// <summary>检测记录实体，供持久化层使用。FreeSql 通过约定映射此 POCO。</summary>
public class InspectionRecord
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? SerialNo { get; set; }
    public required string ProductModel { get; set; }
    public required string EngineName { get; set; }
    public bool IsOk { get; set; }
    public long ElapsedMs { get; set; }

    /// <summary>极耳长度 (mm)。</summary>
    public double? TabLengthMm { get; set; }

    /// <summary>极耳宽度 (mm)。</summary>
    public double? TabWidthMm { get; set; }

    /// <summary>缺陷数量（M3b 填充，M3a 留空）。</summary>
    public int DefectCount { get; set; }

    /// <summary>缺陷类型 JSON 数组（M3b 填充，M3a 留空）。</summary>
    public string? DefectTypesJson { get; set; }

    /// <summary>原始图像路径，用于追溯。</summary>
    public string? OriginalImagePath { get; set; }

    /// <summary>标注图像路径，用于追溯。</summary>
    public string? AnnotatedImagePath { get; set; }

    /// <summary>检测时使用的参数快照 (JSON)，用于还原检测状态。</summary>
    public required string AlgorithmParamsJson { get; set; }
}
