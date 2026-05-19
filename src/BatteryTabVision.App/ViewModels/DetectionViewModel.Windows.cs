using System.Windows.Data;
using System.Windows.Threading;

namespace BatteryTabVision.App.ViewModels;

public sealed partial class DetectionViewModel
{
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
