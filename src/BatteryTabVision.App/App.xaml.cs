using System.IO;
using BatteryTabVision.App.Infrastructure;
using BatteryTabVision.App.Views;
using BatteryTabVision.Core.Abstractions;
using BatteryTabVision.Core.Services;
using BatteryTabVision.Engines.Halcon;
using BatteryTabVision.Engines.OpenCv;
using BatteryTabVision.Persistence;
using BatteryTabVision.Persistence.Services;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;

namespace BatteryTabVision.App;

/// <summary>
/// Prism 应用程序入口点。
/// CreateShell → RegisterTypes → ConfigureModuleCatalog → OnInitialized 按 Prism 生命周期顺序执行。
/// </summary>
public partial class App : PrismApplication
{
    protected override System.Windows.Window CreateShell()
        => Container.Resolve<MainShellView>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {

        containerRegistry.RegisterSingleton<IVisionAlgorithmFactory, VisionAlgorithmFactory>();


        var configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config", "algorithm-profiles.json");
        containerRegistry.RegisterSingleton<IProfileConfigService>(
            _ => new ProfileConfigService(configPath));

        var dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "data", "inspection.db");
        var fsql = PersistenceSetup.CreateFreeSql(dbPath);
        containerRegistry.RegisterInstance(fsql);
        containerRegistry.RegisterSingleton<IInspectionRepository, InspectionRepository>();


        containerRegistry.RegisterForNavigation<DetectionView, ViewModels.DetectionViewModel>();
        containerRegistry.RegisterForNavigation<ProfileConfigView, ViewModels.ProfileConfigViewModel>();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<OpenCvEngineModule>();
        moduleCatalog.AddModule<HalconEngineModule>();

    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RequestNavigate("ContentRegion", nameof(DetectionView));
    }
}
