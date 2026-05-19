namespace BatteryTabVision.Core.Models;

/// <summary>
/// 检测过程中产生的单项尺寸测量值。
/// </summary>
public sealed class Measurement
{
    /// <summary>测量项名称，如 "TabLength" / "TabWidth" / "TabPitch" / "TabAngle"。</summary>
    public required string Name { get; init; }

    /// <summary>以指定单位表示的测量值。</summary>
    public required double Value { get; init; }

    /// <summary>测量单位："mm"（毫米）/ "deg"（度）/ "px"（像素）。</summary>
    public required string Unit { get; init; }

    /// <summary>公差下限；null 表示无下限约束。</summary>
    public double? LowerLimit { get; init; }

    /// <summary>公差上限；null 表示无上限约束。</summary>
    public double? UpperLimit { get; init; }

    /// <summary><see cref="Value"/> 在 <see cref="LowerLimit"/> 与 <see cref="UpperLimit"/> 范围内时为 true。</summary>
    public bool IsInTolerance { get; init; }
}
