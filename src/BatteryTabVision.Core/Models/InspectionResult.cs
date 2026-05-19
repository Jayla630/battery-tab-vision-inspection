namespace BatteryTabVision.Core.Models;

/// <summary>
/// 一次检测的不可变结果记录。
/// 由引擎产出后禁止任何业务代码修改其属性。
/// </summary>
public sealed class InspectionResult
{
    /// <summary>工件通过全部检查时为 true；任何失败或错误时为 false。</summary>
    public required bool IsOk { get; init; }

    /// <summary>产出本结果的引擎名称。</summary>
    public required string EngineName { get; init; }

    /// <summary>本次检测耗用的实际时间（毫秒）。</summary>
    public long ElapsedMs { get; init; }

    /// <summary>本次检测产出的全部尺寸测量值。</summary>
    public IReadOnlyList<Measurement> Measurements { get; init; } = Array.Empty<Measurement>();

    /// <summary>本次检测发现的全部缺陷。</summary>
    public IReadOnlyList<Defect> Defects { get; init; } = Array.Empty<Defect>();

    /// <summary>标注结果图的落盘路径，用于追溯；未落盘时为 null。</summary>
    public string? AnnotatedImagePath { get; init; }

    /// <summary><see cref="IsOk"/> 为 false 时的错误信息；成功时为 null。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>本结果完成时的时间戳。</summary>
    public DateTime FinishedAt { get; init; } = DateTime.Now;
}
