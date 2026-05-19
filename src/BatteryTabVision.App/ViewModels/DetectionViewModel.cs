using System.Collections.ObjectModel;
using System.Drawing;
using System.Text.Json;
using BatteryTabVision.App.Models;
using BatteryTabVision.Core.Abstractions;
using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;
using BatteryTabVision.Engines.OpenCv.Samples;
using Prism.Commands;
using Prism.Mvvm;
using System.IO;

namespace BatteryTabVision.App.ViewModels;

/// <summary>
/// 检测界面的 ViewModel。
/// 不直接依赖任何 WPF 类型：图像以文件路径暴露，View 层负责加载为 BitmapImage。
/// WPF 专属功能（DispatcherTimer、BindingOperations）通过 partial 方法隔离到
/// DetectionViewModel.Windows.cs，从而保持 net8.0 构建通过。
/// </summary>
public sealed partial class DetectionViewModel : BindableBase
{
    private readonly IVisionAlgorithmFactory _algorithmFactory;
    private readonly IProfileConfigService _configService;
    private readonly IInspectionRepository _repository;
    private IVisionAlgorithm _currentAlgorithm;
    private CancellationTokenSource? _detectCts;
    private readonly object _historyLock = new();

    private string? _currentImagePath;
    private string? _annotatedImagePath;
    private string _statusText = "Ready";
    private bool _isBusy;


    private string _selectedEngine = "OpenCV";


    private string? _selectedModel;


    private int _binaryThreshold = 128;
    private double _minContourArea = 1000.0;
    private double _pixelsPerMm = 20.0;
    private double _lengthLowerLimit = 19.5;
    private double _lengthUpperLimit = 20.5;
    private double _widthLowerLimit = 4.8;
    private double _widthUpperLimit = 5.2;
    private Rectangle _imageRoi = Rectangle.Empty;


    partial void InitializeThrottleTimer();
    partial void StartThrottle();
    partial void StopThrottleTimer();

    public DetectionViewModel(
        IVisionAlgorithmFactory algorithmFactory,
        IProfileConfigService configService,
        IInspectionRepository repository)
    {
        _algorithmFactory = algorithmFactory;
        _currentAlgorithm = _algorithmFactory.Create("OpenCV");
        _configService = configService;
        _repository = repository;
        InitializeThrottleTimer();

        LoadSampleCommand = new DelegateCommand(async () => await LoadSampleAsync(), () => !IsBusy);
        LoadBurrSampleCommand = new DelegateCommand(async () => await LoadBurrSampleAsync(), () => !IsBusy);
        RunDetectCommand = new DelegateCommand(async () => await RunDetectAsync(),
            () => !IsBusy && !string.IsNullOrEmpty(CurrentImagePath));


        _selectedModel = AvailableModels.FirstOrDefault();
        if (_selectedModel != null) LoadProfileIntoParams(_selectedModel);


        _ = LoadRecentHistoryAsync();
    }



    public IReadOnlyList<string> AvailableEngines => _algorithmFactory.AvailableEngines;

    public string SelectedEngine
    {
        get => _selectedEngine;
        set
        {
            if (!SetProperty(ref _selectedEngine, value)) return;
            try
            {
                _detectCts?.Cancel();
                var old = _currentAlgorithm;
                _currentAlgorithm = _algorithmFactory.Create(value);

                try { old?.Dispose(); } catch { /* best-effort */ }
                StatusText = $"已切换到 {value} 引擎";
                ScheduleDetect();
            }
            catch (Exception ex)
            {
                StatusText = $"引擎切换失败: {ex.Message}";
            }
        }
    }



    public IReadOnlyList<string> AvailableModels => _configService.AvailableModels;

    public string? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value) && value != null)
                LoadProfileIntoParams(value);
        }
    }



    public string? CurrentImagePath
    {
        get => _currentImagePath;
        private set { if (SetProperty(ref _currentImagePath, value)) RaiseCanExecuteForAllCommands(); }
    }

    public string? AnnotatedImagePath
    {
        get => _annotatedImagePath;
        private set => SetProperty(ref _annotatedImagePath, value);
    }

    /// <summary>实际绑定到 Image.Source 的路径：有标注图就显示标注图，否则显示原图</summary>
    public string? DisplayedImagePath => _annotatedImagePath ?? _currentImagePath;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) RaiseCanExecuteForAllCommands(); }
    }

    public DelegateCommand LoadSampleCommand { get; }
    public DelegateCommand LoadBurrSampleCommand { get; }
    public DelegateCommand RunDetectCommand { get; }



    public int BinaryThreshold
    {
        get => _binaryThreshold;
        set { if (SetProperty(ref _binaryThreshold, value)) ScheduleDetect(); }
    }

    public double MinContourArea
    {
        get => _minContourArea;
        set { if (SetProperty(ref _minContourArea, value)) ScheduleDetect(); }
    }

    public double PixelsPerMm
    {
        get => _pixelsPerMm;
        set { if (SetProperty(ref _pixelsPerMm, value)) ScheduleDetect(); }
    }

    public double LengthLowerLimit
    {
        get => _lengthLowerLimit;
        set { if (SetProperty(ref _lengthLowerLimit, value)) ScheduleDetect(); }
    }

    public double LengthUpperLimit
    {
        get => _lengthUpperLimit;
        set { if (SetProperty(ref _lengthUpperLimit, value)) ScheduleDetect(); }
    }

    public double WidthLowerLimit
    {
        get => _widthLowerLimit;
        set { if (SetProperty(ref _widthLowerLimit, value)) ScheduleDetect(); }
    }

    public double WidthUpperLimit
    {
        get => _widthUpperLimit;
        set { if (SetProperty(ref _widthUpperLimit, value)) ScheduleDetect(); }
    }



    /// <summary>当前图像坐标系下的 ROI 矩形；由代码后置从屏幕坐标反算写入。</summary>
    public Rectangle ImageRoi
    {
        get => _imageRoi;
        set { if (SetProperty(ref _imageRoi, value)) ScheduleDetect(); }
    }

    /// <summary>当前加载图像的像素宽度，供 View 做坐标转换。</summary>
    public int CurrentImageWidth { get; private set; }

    /// <summary>当前加载图像的像素高度，供 View 做坐标转换。</summary>
    public int CurrentImageHeight { get; private set; }



    /// <summary>检测历史（最新在 [0]），跨线程写入由 BindingOperations 保护。</summary>
    public ObservableCollection<DetectionHistoryItem> History { get; } = new();



    /// <summary>供单元测试直接触发检测，绕过 DispatcherTimer 防抖。</summary>
    internal Task TriggerDetectNowForTestAsync() => RunDetectAsync();



    private Task LoadSampleAsync()
    {
        IsBusy = true;
        try
        {
            var (path, _, _) = SyntheticTabImageGenerator.Generate();
            CurrentImagePath = path;
            CurrentImageWidth = 800;
            CurrentImageHeight = 600;
            AnnotatedImagePath = null;
            RaisePropertyChanged(nameof(DisplayedImagePath));
            StatusText = $"Loaded: {Path.GetFileName(path)}";
            return Task.CompletedTask;
        }
        finally { IsBusy = false; }
    }

    private Task LoadBurrSampleAsync()
    {
        IsBusy = true;
        try
        {
            var (path, _, _) = SyntheticTabImageGenerator.GenerateWithDefect(
                defect: SyntheticDefect.Burr,
                burrCount: 3,
                burrHeightPx: 15,
                burrWidthPx: 10,
                noiseStdDev: 5.0);
            CurrentImagePath = path;
            CurrentImageWidth = 800;
            CurrentImageHeight = 600;
            AnnotatedImagePath = null;
            RaisePropertyChanged(nameof(DisplayedImagePath));
            StatusText = $"Loaded: {Path.GetFileName(path)}";
            ScheduleDetect();
            return Task.CompletedTask;
        }
        finally { IsBusy = false; }
    }

    private async Task RunDetectAsync()
    {
        if (string.IsNullOrEmpty(CurrentImagePath)) return;


        _detectCts?.Cancel();
        _detectCts = new CancellationTokenSource();
        var token = _detectCts.Token;

        IsBusy = true;
        try
        {

            await _currentAlgorithm.InitializeAsync(BuildConfigFromCurrentParams(), token);

            var image = new InspectionImage { SourcePath = CurrentImagePath! };
            var result = await _currentAlgorithm.DetectAsync(image, token);

            if (token.IsCancellationRequested) return;

            AnnotatedImagePath = result.AnnotatedImagePath;
            RaisePropertyChanged(nameof(DisplayedImagePath));
            StatusText = BuildStatusText(result);
            History.Insert(0, DetectionHistoryItem.FromResult(result));


            await SaveRecordAsync(result, token);
        }
        catch (OperationCanceledException) { /* 正常取消，不处理 */ }
        finally { IsBusy = false; }
    }

    /// <summary>参数或 ROI 变化时调用，重置防抖计时器。</summary>
    private void ScheduleDetect()
    {
        if (string.IsNullOrEmpty(CurrentImagePath)) return;
        StartThrottle();
    }

    private void RaiseCanExecuteForAllCommands()
    {
        LoadSampleCommand.RaiseCanExecuteChanged();
        LoadBurrSampleCommand.RaiseCanExecuteChanged();
        RunDetectCommand.RaiseCanExecuteChanged();
    }


    private Dictionary<string, object> _defectParams = new();

    private AlgorithmConfig BuildConfigFromCurrentParams()
    {
        var parameters = new Dictionary<string, object>
        {
            ["BinaryThreshold"] = BinaryThreshold,
            ["MinContourArea"] = MinContourArea,
            ["PixelsPerMm"] = PixelsPerMm,
            ["LengthLowerLimit"] = LengthLowerLimit,
            ["LengthUpperLimit"] = LengthUpperLimit,
            ["WidthLowerLimit"] = WidthLowerLimit,
            ["WidthUpperLimit"] = WidthUpperLimit,
        };


        foreach (var kv in _defectParams)
            parameters[kv.Key] = kv.Value;

        return new AlgorithmConfig
        {
            ProductModel = SelectedModel ?? "Unknown",
            Parameters = parameters,
        };
    }



    private void LoadProfileIntoParams(string productModel)
    {
        var config = _configService.LoadProfile(productModel);
        if (config == null) return;

        StopThrottleTimer();
        BinaryThreshold = (int)config.Parameters["BinaryThreshold"];
        MinContourArea = (double)config.Parameters["MinContourArea"];
        PixelsPerMm = (double)config.Parameters["PixelsPerMm"];
        LengthLowerLimit = (double)config.Parameters["LengthLowerLimit"];
        LengthUpperLimit = (double)config.Parameters["LengthUpperLimit"];
        WidthLowerLimit = (double)config.Parameters["WidthLowerLimit"];
        WidthUpperLimit = (double)config.Parameters["WidthUpperLimit"];


        _defectParams = config.Parameters
            .Where(kv => kv.Key.StartsWith("Defect.", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        ScheduleDetect();
    }

    private async Task LoadRecentHistoryAsync()
    {
        try
        {
            var records = await _repository.GetRecentAsync(50);
            foreach (var r in records)
                History.Insert(0, DetectionHistoryItem.FromRecord(r));
        }
        catch (Exception ex)
        {
            StatusText = $"历史记录加载失败: {ex.Message}";
        }
    }

    private async Task SaveRecordAsync(InspectionResult result, CancellationToken token)
    {
        var len = result.Measurements.FirstOrDefault(m => m.Name == "TabLength");
        var wid = result.Measurements.FirstOrDefault(m => m.Name == "TabWidth");
        var record = new InspectionRecord
        {
            ProductModel = SelectedModel ?? "Unknown",
            EngineName = result.EngineName,
            IsOk = result.IsOk,
            ElapsedMs = result.ElapsedMs,
            TabLengthMm = len?.Value,
            TabWidthMm = wid?.Value,
            AnnotatedImagePath = result.AnnotatedImagePath,
            AlgorithmParamsJson = JsonSerializer.Serialize(
                BuildConfigFromCurrentParams().Parameters),

            DefectCount = result.Defects.Count,
            DefectTypesJson = result.Defects.Count > 0
                ? JsonSerializer.Serialize(
                    result.Defects.Select(d => d.Type).Distinct().ToList())
                : null,
        };
        await _repository.SaveAsync(record, token);
    }

    private static string BuildStatusText(InspectionResult r)
    {
        if (!r.IsOk && !string.IsNullOrEmpty(r.ErrorMessage))
            return $"ERROR | {r.ErrorMessage} | Engine: {r.EngineName}";
        var measurements = string.Join("  ", r.Measurements.Select(m => $"{m.Name}={m.Value:F2}{m.Unit}"));
        var status = r.IsOk ? "OK" : "NG";


        string defectPart = "";
        if (r.Defects.Count > 0)
        {
            var types = r.Defects
                .GroupBy(d => d.Type)
                .Select(g => $"{g.Key}×{g.Count()}");
            defectPart = $" | Defects: {string.Join(", ", types)}";
        }

        return $"{status} | {measurements}{defectPart} | {r.ElapsedMs}ms | Engine: {r.EngineName}";
    }
}
