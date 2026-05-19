using BatteryTabVision.Core.Services;
using Prism.Commands;
using Prism.Navigation.Regions;

namespace BatteryTabVision.App.ViewModels;

public sealed partial class ProfileConfigViewModel : INavigationAware
{
    private readonly IRegionManager? _regionManager;

    public ProfileConfigViewModel(IProfileConfigService configService, IRegionManager regionManager)
        : this(configService)
    {
        _regionManager = regionManager;
        NavigateBackCommand = new DelegateCommand(() =>
            _regionManager!.RequestNavigate("ContentRegion", "DetectionView"));
    }

    public DelegateCommand? NavigateBackCommand { get; }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        RefreshProfileNames();
        if (ProfileNames.Count > 0)
            SelectedProfileName = ProfileNames[0];
        else
            SelectedProfileName = null;
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }
}
