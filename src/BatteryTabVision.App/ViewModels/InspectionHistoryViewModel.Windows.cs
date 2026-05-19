using Prism.Navigation.Regions;

namespace BatteryTabVision.App.ViewModels;

public partial class InspectionHistoryViewModel : INavigationAware
{
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        QueryCommand.Execute();
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }
}
