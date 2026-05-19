using BatteryTabVision.Core.Models;

namespace BatteryTabVision.Core.Services;

/// <summary>检测结果持久化仓储。</summary>
public interface IInspectionRepository
{
    Task SaveAsync(InspectionRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<InspectionRecord>> GetRecentAsync(int count = 50, CancellationToken ct = default);
}
