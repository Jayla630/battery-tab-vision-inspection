using BatteryTabVision.Core.Models;

namespace BatteryTabVision.Core.Abstractions;

/// <summary>
/// 所有视觉检测算法引擎的统一抽象。
/// 使用完毕后必须调用 Dispose 释放引擎句柄和模型资源。
/// </summary>
public interface IVisionAlgorithm : IDisposable
{
    /// <summary>
    /// 引擎名称，用于追溯与日志，如 "OpenCvSharp" / "Halcon" / "VisionMaster"。
    /// </summary>
    string EngineName { get; }

    /// <summary>
    /// 引擎是否已完成初始化。
    /// 未初始化时禁止调用 <see cref="DetectAsync"/>。
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 初始化引擎（加载模型、配置参数、申请句柄等）。
    /// 重复调用必须幂等。
    /// </summary>
    /// <param name="config">算法配置，包含产品型号和参数字典。</param>
    /// <param name="ct">取消令牌。</param>
    Task InitializeAsync(AlgorithmConfig config, CancellationToken ct = default);

    /// <summary>
    /// 执行一次检测并返回结果。
    /// 线程安全：同一实例不保证并发安全，调用方需自行串行化。
    /// </summary>
    /// <param name="image">待检测图像。</param>
    /// <param name="ct">取消令牌。</param>
    Task<InspectionResult> DetectAsync(InspectionImage image, CancellationToken ct = default);
}
