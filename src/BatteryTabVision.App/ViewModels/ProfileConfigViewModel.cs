using System.Collections.ObjectModel;
using BatteryTabVision.Core.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace BatteryTabVision.App.ViewModels;

public sealed partial class ProfileConfigViewModel : BindableBase
{
    private readonly IProfileConfigService _configService;
    private bool _isLoading;

    public ProfileConfigViewModel(IProfileConfigService configService)
    {
        _configService = configService;

        SaveCommand = new DelegateCommand(Save, () => HasUnsavedChanges);
        DiscardCommand = new DelegateCommand(Discard, () => HasUnsavedChanges);
        AddProfileCommand = new DelegateCommand(AddProfile);
        DeleteProfileCommand = new DelegateCommand(DeleteProfile, () => SelectedProfileName != null);
        MoveUpCommand   = new DelegateCommand(MoveUp,   () => CanMoveUp())
            .ObservesProperty(() => SelectedProfileName);
        MoveDownCommand = new DelegateCommand(MoveDown, () => CanMoveDown())
            .ObservesProperty(() => SelectedProfileName);

        RefreshProfileNames();
    }

    public ObservableCollection<string> ProfileNames { get; } = new();

    private string? _selectedProfileName;
    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            if (SetProperty(ref _selectedProfileName, value) && value != null)
                LoadProfile(value);
            DeleteProfileCommand.RaiseCanExecuteChanged();
        }
    }

    private string _editingModelName = "";
    public string EditingModelName
    {
        get => _editingModelName;
        set { if (SetProperty(ref _editingModelName, value)) MarkChanged(); }
    }

    private int _binaryThreshold = 128;
    public int BinaryThreshold
    {
        get => _binaryThreshold;
        set { if (SetProperty(ref _binaryThreshold, value)) MarkChanged(); }
    }

    private double _minContourArea = 1000.0;
    public double MinContourArea
    {
        get => _minContourArea;
        set { if (SetProperty(ref _minContourArea, value)) MarkChanged(); }
    }

    private double _pixelsPerMm = 20.0;
    public double PixelsPerMm
    {
        get => _pixelsPerMm;
        set { if (SetProperty(ref _pixelsPerMm, value)) MarkChanged(); }
    }

    private double _lengthLowerLimit;
    public double LengthLowerLimit
    {
        get => _lengthLowerLimit;
        set { if (SetProperty(ref _lengthLowerLimit, value)) MarkChanged(); }
    }

    private double _lengthUpperLimit;
    public double LengthUpperLimit
    {
        get => _lengthUpperLimit;
        set { if (SetProperty(ref _lengthUpperLimit, value)) MarkChanged(); }
    }

    private double _widthLowerLimit;
    public double WidthLowerLimit
    {
        get => _widthLowerLimit;
        set { if (SetProperty(ref _widthLowerLimit, value)) MarkChanged(); }
    }

    private double _widthUpperLimit;
    public double WidthUpperLimit
    {
        get => _widthUpperLimit;
        set { if (SetProperty(ref _widthUpperLimit, value)) MarkChanged(); }
    }

    private double _burrThresholdPx = 3.0;
    public double BurrThresholdPx
    {
        get => _burrThresholdPx;
        set { if (SetProperty(ref _burrThresholdPx, value)) MarkChanged(); }
    }

    private int _minBurrClusterPoints = 3;
    public int MinBurrClusterPoints
    {
        get => _minBurrClusterPoints;
        set { if (SetProperty(ref _minBurrClusterPoints, value)) MarkChanged(); }
    }

    private bool _enableBurr = true;
    public bool EnableBurr
    {
        get => _enableBurr;
        set { if (SetProperty(ref _enableBurr, value)) MarkChanged(); }
    }

    private double _misalignmentThresholdMm = 1.0;
    public double MisalignmentThresholdMm
    {
        get => _misalignmentThresholdMm;
        set { if (SetProperty(ref _misalignmentThresholdMm, value)) MarkChanged(); }
    }

    private bool _enableMisalignment = true;
    public bool EnableMisalignment
    {
        get => _enableMisalignment;
        set { if (SetProperty(ref _enableMisalignment, value)) MarkChanged(); }
    }

    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set
        {
            if (SetProperty(ref _hasUnsavedChanges, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                DiscardCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public DelegateCommand SaveCommand { get; }
    public DelegateCommand DiscardCommand { get; }
    public DelegateCommand AddProfileCommand { get; }
    public DelegateCommand DeleteProfileCommand { get; }
    public DelegateCommand MoveUpCommand { get; }
    public DelegateCommand MoveDownCommand { get; }

    private void MarkChanged()
    {
        if (!_isLoading)
            HasUnsavedChanges = true;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditingModelName))
        {
            StatusMessage = "请输入型号名称";
            return;
        }

        _configService.UpsertProfile(EditingModelName, BuildParamsDict());
        RefreshProfileNames();
        SelectedProfileName = EditingModelName;
        HasUnsavedChanges = false;
        StatusMessage = "保存成功";
    }

    private void Discard()
    {
        if (SelectedProfileName != null)
            LoadProfile(SelectedProfileName);
        else
            ClearForm();
        HasUnsavedChanges = false;
        StatusMessage = "已取消修改";
    }

    private void AddProfile()
    {
        SelectedProfileName = null;
        ClearForm();
        HasUnsavedChanges = false;
        StatusMessage = "请输入新型号名称和参数";
    }

    private void DeleteProfile()
    {
        if (SelectedProfileName == null) return;

        var name = SelectedProfileName;
        _configService.DeleteProfile(name);
        RefreshProfileNames();
        ClearForm();
        SelectedProfileName = null;
        HasUnsavedChanges = false;
        StatusMessage = $"已删除型号: {name}";
    }

    private bool CanMoveUp()
    {
        if (string.IsNullOrEmpty(SelectedProfileName)) return false;
        return ProfileNames.IndexOf(SelectedProfileName) > 0;
    }

    private bool CanMoveDown()
    {
        if (string.IsNullOrEmpty(SelectedProfileName)) return false;
        int idx = ProfileNames.IndexOf(SelectedProfileName);
        return idx >= 0 && idx < ProfileNames.Count - 1;
    }

    private void MoveUp()
    {
        int idx = ProfileNames.IndexOf(SelectedProfileName!);
        if (idx <= 0) return;
        ProfileNames.Move(idx, idx - 1);
        _configService.ReorderProfiles(ProfileNames);
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
    }

    private void MoveDown()
    {
        int idx = ProfileNames.IndexOf(SelectedProfileName!);
        if (idx < 0 || idx >= ProfileNames.Count - 1) return;
        ProfileNames.Move(idx, idx + 1);
        _configService.ReorderProfiles(ProfileNames);
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
    }

    public void RefreshProfileNames()
    {
        ProfileNames.Clear();
        foreach (var name in _configService.AvailableModels)
            ProfileNames.Add(name);
    }

    private void ClearForm()
    {
        _isLoading = true;
        EditingModelName = "";
        BinaryThreshold = 128;
        MinContourArea = 1000.0;
        PixelsPerMm = 20.0;
        LengthLowerLimit = 0;
        LengthUpperLimit = 0;
        WidthLowerLimit = 0;
        WidthUpperLimit = 0;
        BurrThresholdPx = 3.0;
        MinBurrClusterPoints = 3;
        EnableBurr = true;
        MisalignmentThresholdMm = 1.0;
        EnableMisalignment = true;
        _isLoading = false;
    }

    private void LoadProfile(string name)
    {
        var config = _configService.LoadProfile(name);
        if (config == null) return;

        _isLoading = true;
        EditingModelName = config.ProductModel;

        var p = config.Parameters;
        BinaryThreshold = GetInt(p, "BinaryThreshold", 128);
        MinContourArea = GetDouble(p, "MinContourArea", 1000.0);
        PixelsPerMm = GetDouble(p, "PixelsPerMm", 20.0);
        LengthLowerLimit = GetDouble(p, "LengthLowerLimit", 0);
        LengthUpperLimit = GetDouble(p, "LengthUpperLimit", 0);
        WidthLowerLimit = GetDouble(p, "WidthLowerLimit", 0);
        WidthUpperLimit = GetDouble(p, "WidthUpperLimit", 0);
        BurrThresholdPx = GetDouble(p, "Defect.BurrThresholdPx", 3.0);
        MinBurrClusterPoints = GetInt(p, "Defect.MinBurrClusterPoints", 3);
        EnableBurr = GetBool(p, "Defect.EnableBurr", true);
        MisalignmentThresholdMm = GetDouble(p, "Defect.MisalignmentThresholdMm", 1.0);
        EnableMisalignment = GetBool(p, "Defect.EnableMisalignment", true);

        _isLoading = false;
        HasUnsavedChanges = false;
    }

    private Dictionary<string, object> BuildParamsDict()
    {
        return new Dictionary<string, object>
        {
            ["BinaryThreshold"] = BinaryThreshold,
            ["MinContourArea"] = MinContourArea,
            ["PixelsPerMm"] = PixelsPerMm,
            ["LengthLowerLimit"] = LengthLowerLimit,
            ["LengthUpperLimit"] = LengthUpperLimit,
            ["WidthLowerLimit"] = WidthLowerLimit,
            ["WidthUpperLimit"] = WidthUpperLimit,
            ["BurrThresholdPx"] = BurrThresholdPx,
            ["MinBurrClusterPoints"] = MinBurrClusterPoints,
            ["EnableBurr"] = EnableBurr,
            ["MisalignmentThresholdMm"] = MisalignmentThresholdMm,
            ["EnableMisalignment"] = EnableMisalignment,
        };
    }

    private static int GetInt(IDictionary<string, object> dict, string key, int fallback)
    {
        if (dict.TryGetValue(key, out var v))
        {
            if (v is int i) return i;
            if (v is double d) return (int)d;
            if (v is long l) return (int)l;
        }
        return fallback;
    }

    private static double GetDouble(IDictionary<string, object> dict, string key, double fallback)
    {
        if (dict.TryGetValue(key, out var v))
        {
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is long l) return l;
        }
        return fallback;
    }

    private static bool GetBool(IDictionary<string, object> dict, string key, bool fallback)
    {
        if (dict.TryGetValue(key, out var v) && v is bool b)
            return b;
        return fallback;
    }
}
