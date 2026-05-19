using BatteryTabVision.Core.Abstractions;
using Prism.Ioc;
using Prism.Modularity;

namespace BatteryTabVision.Engines.Halcon;

/// <summary>
/// Prism 模块：将 HalconVisionAlgorithm 以具名注册到 DI 容器，
/// 供 VisionAlgorithmFactory 按 "Halcon" 名称解析。
/// </summary>
public class HalconEngineModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<IVisionAlgorithm, HalconVisionAlgorithm>("Halcon");
    }

    public void OnInitialized(IContainerProvider containerProvider) { }
}
