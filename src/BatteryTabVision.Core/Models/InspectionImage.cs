using System.Drawing;

namespace BatteryTabVision.Core.Models;

/// <summary>
/// 代表一次提交给视觉检测引擎的图像。
/// 同时支持文件路径（开发期样本回放）和内存字节流（真机相机采集）两种来源。
/// </summary>
public sealed class InspectionImage
{
    /// <summary>源图像文件路径，用于开发阶段的样本回放。</summary>
    public required string SourcePath { get; init; }

    /// <summary>相机内存流的原始图像字节；使用文件路径模式时为 null。</summary>
    public byte[]? RawData { get; init; }

    /// <summary>图像采集或加载时的时间戳。</summary>
    public DateTime CaptureTime { get; init; } = DateTime.Now;

    /// <summary>工件或电芯条码，用于追溯；不可用时为 null。</summary>
    public string? SerialNumber { get; init; }

    /// <summary>M3b: 检测 ROI 矩形（像素坐标系），用于错位检测的预期位置计算。未设置时为 <see cref="Rectangle.Empty"/>。</summary>
    public Rectangle Roi { get; init; } = Rectangle.Empty;
}
