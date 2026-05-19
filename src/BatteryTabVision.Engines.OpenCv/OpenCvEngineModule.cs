using BatteryTabVision.Core.Abstractions;
using Prism.Ioc;
using Prism.Modularity;

namespace BatteryTabVision.Engines.OpenCv;

/// <summary>
/// Prism 模块：将 OpenCvVisionAlgorithm 以具名注册到 DI 容器，
/// 供 VisionAlgorithmFactory 按 "OpenCV" 名称解析。
/// </summary>
public class OpenCvEngineModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<IVisionAlgorithm, OpenCvVisionAlgorithm>("OpenCV");
    }

    public void OnInitialized(IContainerProvider containerProvider) { }
}
