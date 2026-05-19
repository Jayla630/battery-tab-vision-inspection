using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;
using BatteryTabVision.Persistence.Models;
using FreeSql;

namespace BatteryTabVision.Persistence.Services;

/// <summary>基于 FreeSql 的检测记录仓储实现。</summary>
public sealed class InspectionRepository : IInspectionRepository
{
    private readonly IFreeSql _fsql;

    public InspectionRepository(IFreeSql fsql)
    {
        _fsql = fsql;
    }

    public async Task SaveAsync(InspectionRecord record, CancellationToken ct = default)
    {
        var entity = ToEntity(record);
        await _fsql.Insert(entity).ExecuteAffrowsAsync();
    }

    public async Task<IReadOnlyList<InspectionRecord>> GetRecentAsync(
        int count = 50, CancellationToken ct = default)
    {
        var entities = await _fsql.Select<InspectionRecordEntity>()
            .OrderByDescending(e => e.Id)
            .Take(count)
            .ToListAsync(ct);
        return entities.Select(ToRecord).ToList();
    }

    private static InspectionRecordEntity ToEntity(InspectionRecord r) => new()
    {
        Timestamp = r.Timestamp,
        SerialNo = r.SerialNo,
        ProductModel = r.ProductModel,
        EngineName = r.EngineName,
        IsOk = r.IsOk,
        ElapsedMs = r.ElapsedMs,
        TabLengthMm = r.TabLengthMm,
        TabWidthMm = r.TabWidthMm,
        DefectCount = r.DefectCount,
        DefectTypesJson = r.DefectTypesJson,
        OriginalImagePath = r.OriginalImagePath,
        AnnotatedImagePath = r.AnnotatedImagePath,
        AlgorithmParamsJson = r.AlgorithmParamsJson,
    };

    private static InspectionRecord ToRecord(InspectionRecordEntity e) => new()
    {
        ProductModel = e.ProductModel,
        EngineName = e.EngineName,
        AlgorithmParamsJson = e.AlgorithmParamsJson,
        Timestamp = e.Timestamp,
        SerialNo = e.SerialNo,
        IsOk = e.IsOk,
        ElapsedMs = e.ElapsedMs,
        TabLengthMm = e.TabLengthMm,
        TabWidthMm = e.TabWidthMm,
        DefectCount = e.DefectCount,
        DefectTypesJson = e.DefectTypesJson,
        OriginalImagePath = e.OriginalImagePath,
        AnnotatedImagePath = e.AnnotatedImagePath,
    };
}
