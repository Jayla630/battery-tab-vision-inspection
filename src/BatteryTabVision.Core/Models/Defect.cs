using System.Drawing;

namespace BatteryTabVision.Core.Models;

/// <summary>
/// 描述在检测图像中发现的单个缺陷。
/// </summary>
public sealed class Defect
{
    /// <summary>缺陷分类："Burr"（毛刺）/ "Misalignment"（错位）/ "Wrinkle"（褶皱）/ "WeldDefect"（焊接异常）。</summary>
    public required string Type { get; init; }

    /// <summary>检测置信度，范围 0.0 ~ 1.0。</summary>
    public required double Confidence { get; init; }

    /// <summary>缺陷区域的像素坐标包围盒。</summary>
    public Rectangle BoundingBox { get; init; }

    /// <summary>缺陷的可选文字描述；不需要时为 null。</summary>
    public string? Description { get; init; }
}
