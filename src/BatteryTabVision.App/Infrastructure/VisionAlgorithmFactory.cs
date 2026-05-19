using BatteryTabVision.Core.Abstractions;
using BatteryTabVision.Core.Services;
using Prism.Ioc;

namespace BatteryTabVision.App.Infrastructure;

/// <summary>
/// <see cref="IVisionAlgorithmFactory"/> 的 DryIoc 实现。
/// 从 DI 容器按具名注册解析引擎实例。
/// </summary>
public sealed class VisionAlgorithmFactory : IVisionAlgorithmFactory
{
    private readonly IContainerProvider _container;

    public VisionAlgorithmFactory(IContainerProvider container)
        => _container = container;

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableEngines => ["OpenCV", "Halcon"];

    /// <inheritdoc/>
    public IVisionAlgorithm Create(string engineName)
    {
        try
        {
            return _container.Resolve<IVisionAlgorithm>(engineName);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"无法创建引擎 '{engineName}': {ex.Message}", ex);
        }
    }
}
