using System.Collections.ObjectModel;
using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace BatteryTabVision.App.ViewModels;

public partial class InspectionHistoryViewModel : BindableBase
{
    private readonly IInspectionRepository _repository;
    private readonly IRegionManager _regionManager;

    private ObservableCollection<InspectionRecord> _records = new();
    public ObservableCollection<InspectionRecord> Records
    {
        get => _records;
        set => SetProperty(ref _records, value);
    }

    private string _selectedFilter = "All";
    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                QueryCommand.Execute();
            }
        }
    }

    private DateTime? _fromDate;
    public DateTime? FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    private DateTime? _toDate;
    public DateTime? ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public DelegateCommand QueryCommand { get; }
    public DelegateCommand ResetCommand { get; }
    public DelegateCommand NavigateBackCommand { get; }

    public InspectionHistoryViewModel(IInspectionRepository repository, IRegionManager regionManager)
    {
        _repository = repository;
        _regionManager = regionManager;

        QueryCommand = new DelegateCommand(async () => await QueryAsync());
        ResetCommand = new DelegateCommand(async () =>
        {
            FromDate = null;
            ToDate = null;
            SelectedFilter = "All";
            await QueryAsync();
        });
        NavigateBackCommand = new DelegateCommand(() => 
            _regionManager.RequestNavigate("ContentRegion", "DetectionView"));

        QueryCommand.Execute();
    }

    private async Task QueryAsync()
    {
        IsLoading = true;
        try
        {
            bool? isOk = SelectedFilter switch
            {
                "OK" => true,
                "NG" => false,
                _ => null
            };

            var list = await _repository.QueryAsync(isOk, FromDate, ToDate);
            Records = new ObservableCollection<InspectionRecord>(list);
            StatusText = $"共 {Records.Count} 条记录";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
