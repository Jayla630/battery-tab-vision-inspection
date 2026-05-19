using System.Windows.Data;
using System.Windows.Threading;
using Prism.Navigation.Regions;

namespace BatteryTabVision.App.ViewModels;

public sealed partial class DetectionViewModel : INavigationAware
{
    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (string.IsNullOrEmpty(SelectedModel)) return;
        LoadProfileIntoParams(SelectedModel);
        ScheduleDetect();
    }

    private DispatcherTimer? _throttleTimer;

    partial void InitializeThrottleTimer()
    {
        BindingOperations.EnableCollectionSynchronization(History, _historyLock);
        _throttleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _throttleTimer.Tick += async (_, _) =>
        {
            _throttleTimer!.Stop();
            await RunDetectAsync();
        };
    }

    partial void StartThrottle()
    {
        _throttleTimer?.Stop();
        _throttleTimer?.Start();
    }

    partial void StopThrottleTimer()
    {
        _throttleTimer?.Stop();
    }
}
