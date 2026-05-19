using FreeSql.DataAnnotations;

namespace BatteryTabVision.Persistence.Models;

/// <summary>FreeSql 实体，映射到 inspection_records 表。</summary>
[Table(Name = "inspection_records")]
public class InspectionRecordEntity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? SerialNo { get; set; }
    public string ProductModel { get; set; } = "";
    public string EngineName { get; set; } = "";
    public bool IsOk { get; set; }
    public long ElapsedMs { get; set; }
    public double? TabLengthMm { get; set; }
    public double? TabWidthMm { get; set; }
    public int DefectCount { get; set; }
    public string? DefectTypesJson { get; set; }
    public string? OriginalImagePath { get; set; }
    public string? AnnotatedImagePath { get; set; }
    public string AlgorithmParamsJson { get; set; } = "";
}
