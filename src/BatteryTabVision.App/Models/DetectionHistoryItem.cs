using BatteryTabVision.Core.Models;

namespace BatteryTabVision.App.Models;

/// <summary>检测历史列表的单条记录，不可变数据对象。</summary>
public sealed class DetectionHistoryItem
{
    public required DateTime Timestamp { get; init; }
    public required bool IsOk { get; init; }
    public required double TabLengthMm { get; init; }
    public required double TabWidthMm { get; init; }
    public required long ElapsedMs { get; init; }

    /// <summary>M3b: 缺陷数量。</summary>
    public int DefectCount { get; init; }

    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss");
    public string Summary => DefectCount > 0
        ? $"L={TabLengthMm:F2}mm  W={TabWidthMm:F2}mm | {DefectCount}缺陷 ({ElapsedMs}ms)"
        : $"L={TabLengthMm:F2}mm  W={TabWidthMm:F2}mm ({ElapsedMs}ms)";

    /// <summary>从检测结果构建历史记录，提取 TabLength / TabWidth 测量值。</summary>
    public static DetectionHistoryItem FromResult(InspectionResult r)
    {
        var len = r.Measurements.FirstOrDefault(m => m.Name == "TabLength");
        var wid = r.Measurements.FirstOrDefault(m => m.Name == "TabWidth");
        return new()
        {
            Timestamp = DateTime.Now,
            IsOk = r.IsOk,
            TabLengthMm = len?.Value ?? 0,
            TabWidthMm = wid?.Value ?? 0,
            ElapsedMs = r.ElapsedMs,
            DefectCount = r.Defects.Count,
        };
    }

    /// <summary>从持久化记录构建历史条目（M3a 启动时从 SQLite 恢复）。</summary>
    public static DetectionHistoryItem FromRecord(InspectionRecord r) => new()
    {
        Timestamp = r.Timestamp,
        IsOk = r.IsOk,
        TabLengthMm = r.TabLengthMm ?? 0,
        TabWidthMm = r.TabWidthMm ?? 0,
        ElapsedMs = r.ElapsedMs,
        DefectCount = r.DefectCount,
    };
}
