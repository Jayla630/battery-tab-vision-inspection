using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace BatteryTabVision.App.ViewModels;

/// <summary>主窗口壳的 ViewModel。</summary>
public partial class MainShellViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    private string _currentPage = "Detection";

    public string CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public DelegateCommand NavigateToDetectionCommand { get; }
    public DelegateCommand NavigateToProfileConfigCommand { get; }

    public MainShellViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
        
        NavigateToDetectionCommand = new DelegateCommand(() => Navigate("Detection"));
        NavigateToProfileConfigCommand = new DelegateCommand(() => Navigate("ProfileConfig"));
    }

    private void Navigate(string viewName)
    {
        CurrentPage = viewName;
        _regionManager.RequestNavigate("ContentRegion", viewName + "View");
    }
}
