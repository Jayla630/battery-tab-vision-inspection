using BatteryTabVision.Core.Abstractions;

namespace BatteryTabVision.Core.Services;

/// <summary>
/// 视觉算法引擎的运行时工厂。
/// 允许 ViewModel 在不重启应用的情况下按名称创建 / 切换引擎实例。
/// </summary>
public interface IVisionAlgorithmFactory
{
    /// <summary>当前环境可用的引擎名称列表，顺序即 UI 下拉框顺序。</summary>
    IReadOnlyList<string> AvailableEngines { get; }

    /// <summary>按名称创建引擎实例，不存在时抛 ArgumentException。</summary>
    IVisionAlgorithm Create(string engineName);
}
