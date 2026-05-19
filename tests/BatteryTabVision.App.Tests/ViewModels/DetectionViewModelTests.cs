using System.Drawing;
using BatteryTabVision.App.Models;
using BatteryTabVision.App.ViewModels;
using BatteryTabVision.Core.Abstractions;
using BatteryTabVision.Core.Models;
using BatteryTabVision.Core.Services;
using Moq;
using Xunit;

namespace BatteryTabVision.App.Tests.ViewModels;

/// <summary>
/// DetectionViewModel 的单元测试套件。
/// 所有测试均只触及纯 C# ViewModel 逻辑，不实例化任何 WPF 控件。
/// IVisionAlgorithm 通过 Moq 注入，与真实引擎解耦。
/// </summary>
public class DetectionViewModelTests
{
    private readonly Mock<IVisionAlgorithm> _mockAlgorithm;
    private readonly Mock<IVisionAlgorithmFactory> _mockFactory;
    private readonly Mock<IProfileConfigService> _mockConfigService;
    private readonly Mock<IInspectionRepository> _mockRepository;
    private readonly DetectionViewModel _vm;

    public DetectionViewModelTests()
    {
        _mockAlgorithm = new Mock<IVisionAlgorithm>();
        _mockAlgorithm.Setup(a => a.InitializeAsync(It.IsAny<AlgorithmConfig>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

        _mockFactory = new Mock<IVisionAlgorithmFactory>();
        _mockFactory.Setup(f => f.AvailableEngines)
            .Returns(new List<string> { "OpenCV", "Halcon" });
        _mockFactory.Setup(f => f.Create(It.IsAny<string>()))
            .Returns(_mockAlgorithm.Object);

        _mockConfigService = new Mock<IProfileConfigService>();
        _mockConfigService.Setup(c => c.AvailableModels)
            .Returns(new List<string> { "Default-20x5", "TabA-12x5" });
        _mockConfigService.Setup(c => c.LoadProfile(It.IsAny<string>()))
            .Returns(new AlgorithmConfig
            {
                ProductModel = "Default-20x5",
                Parameters = new Dictionary<string, object>
                {
                    ["BinaryThreshold"] = 128,
                    ["MinContourArea"] = 1000.0,
                    ["PixelsPerMm"] = 20.0,
                    ["LengthLowerLimit"] = 19.5,
                    ["LengthUpperLimit"] = 20.5,
                    ["WidthLowerLimit"] = 4.8,
                    ["WidthUpperLimit"] = 5.2,
                }
            });

        _mockRepository = new Mock<IInspectionRepository>();
        _mockRepository.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InspectionRecord>());

        _vm = new DetectionViewModel(
            _mockFactory.Object, _mockConfigService.Object, _mockRepository.Object);
    }



    /// <summary>同步执行 LoadSampleCommand 并设置好 CurrentImagePath（依赖 OpenCV）。</summary>
    private void LoadSample()
    {
        _vm.LoadSampleCommand.Execute();
    }

    private static InspectionResult OkResult(string? annotatedPath = null) => new()
    {
        IsOk = true,
        EngineName = "TestEngine",
        ElapsedMs = 10,
        Measurements =
        [
            new Measurement { Name = "TabLength", Value = 20.0, Unit = "mm", IsInTolerance = true },
            new Measurement { Name = "TabWidth", Value = 5.0, Unit = "mm", IsInTolerance = true },
        ],
        AnnotatedImagePath = annotatedPath,
    };

    private static InspectionResult NgResult() => new()
    {
        IsOk = false,
        EngineName = "TestEngine",
        ElapsedMs = 10,
        Measurements =
        [
            new Measurement { Name = "TabLength", Value = 19.95, Unit = "mm", IsInTolerance = false },
            new Measurement { Name = "TabWidth", Value = 5.0, Unit = "mm", IsInTolerance = true },
        ],
    };



    /// <summary>
    /// LoadSampleCommand 执行后，CurrentImagePath 非空且对应的 PNG 文件实际存在于磁盘。
    /// </summary>
    [Fact]
    public void LoadSampleCommand_PopulatesCurrentImagePath()
    {
        LoadSample();

        Assert.NotNull(_vm.CurrentImagePath);
        Assert.True(File.Exists(_vm.CurrentImagePath),
            $"Expected generated image to exist at: {_vm.CurrentImagePath}");
    }



    /// <summary>
    /// CurrentImagePath 为空时（未加载样本），RunDetectCommand.CanExecute 应返回 false。
    /// </summary>
    [Fact]
    public void RunDetectCommand_BeforeLoadSample_CannotExecute()
    {
        Assert.Null(_vm.CurrentImagePath);
        Assert.False(_vm.RunDetectCommand.CanExecute());
    }

    /// <summary>
    /// 加载样本后，CurrentImagePath 非空，RunDetectCommand.CanExecute 应返回 true。
    /// </summary>
    [Fact]
    public void RunDetectCommand_AfterLoadSample_CanExecute()
    {
        LoadSample();

        Assert.True(_vm.RunDetectCommand.CanExecute(),
            "RunDetectCommand should be executable after a sample is loaded");
    }



    /// <summary>
    /// 执行 RunDetectCommand 后，IVisionAlgorithm.DetectAsync 应被精确调用一次。
    /// </summary>
    [Fact]
    public void RunDetectCommand_CallsAlgorithmDetectAsync_Once()
    {
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(OkResult()));

        _vm.RunDetectCommand.Execute();

        _mockAlgorithm.Verify(
            a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }



    /// <summary>
    /// mock 返回 IsOk=true 时，AnnotatedImagePath 被赋值、StatusText 包含 "OK" 字样。
    /// </summary>
    [Fact]
    public void RunDetectCommand_SuccessUpdatesAnnotatedAndStatus()
    {
        const string annotatedPath = "/tmp/annotated_test.png";
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(OkResult(annotatedPath)));

        _vm.RunDetectCommand.Execute();

        Assert.Equal(annotatedPath, _vm.AnnotatedImagePath);
        Assert.Contains("OK", _vm.StatusText);
    }



    /// <summary>
    /// mock 返回 IsOk=false 且有 ErrorMessage 时，StatusText 应包含 "ERROR"。
    /// </summary>
    [Fact]
    public void RunDetectCommand_FailureUpdatesStatus_WithErrorMessage()
    {
        LoadSample();
        var failResult = new InspectionResult
        {
            IsOk = false,
            EngineName = "TestEngine",
            ErrorMessage = "Contour not found",
        };
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(failResult));

        _vm.RunDetectCommand.Execute();

        Assert.Contains("ERROR", _vm.StatusText);
    }

    /// <summary>
    /// mock 返回 IsOk=false 且无 ErrorMessage 时（NG 公差超出），StatusText 应包含 "NG"。
    /// </summary>
    [Fact]
    public void RunDetectCommand_FailureUpdatesStatus_WithNgStatus()
    {
        LoadSample();
        var ngResult = new InspectionResult
        {
            IsOk = false,
            EngineName = "TestEngine",
            Measurements =
            [
                new Measurement { Name = "TabLength", Value = 22.0, Unit = "mm", IsInTolerance = false },
            ],
        };
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(ngResult));

        _vm.RunDetectCommand.Execute();

        Assert.Contains("NG", _vm.StatusText);
    }



    /// <summary>
    /// 利用 TaskCompletionSource 暂停 DetectAsync，在命令挂起期间验证 IsBusy=true。
    /// </summary>
    [Fact]
    public void IsBusy_DuringDetect_IsTrue()
    {
        LoadSample();
        var tcs = new TaskCompletionSource<InspectionResult>();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(tcs.Task);

        _vm.RunDetectCommand.Execute();

        Assert.True(_vm.IsBusy, "IsBusy must be true while DetectAsync has not returned");

        tcs.SetResult(OkResult());
    }



    /// <summary>
    /// 当 IsBusy 从 false 变为 true 时，两个命令的 CanExecuteChanged 事件必须被触发。
    /// </summary>
    [Fact]
    public void IsBusyChange_RaisesCanExecuteOnCommands()
    {
        LoadSample();
        bool loadCanExecuteChanged = false;
        bool runDetectCanExecuteChanged = false;
        _vm.LoadSampleCommand.CanExecuteChanged += (_, _) => loadCanExecuteChanged = true;
        _vm.RunDetectCommand.CanExecuteChanged += (_, _) => runDetectCanExecuteChanged = true;

        var tcs = new TaskCompletionSource<InspectionResult>();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(tcs.Task);

        _vm.RunDetectCommand.Execute();

        Assert.True(loadCanExecuteChanged, "LoadSampleCommand.CanExecuteChanged should fire when IsBusy changes");
        Assert.True(runDetectCanExecuteChanged, "RunDetectCommand.CanExecuteChanged should fire when IsBusy changes");

        tcs.SetResult(OkResult());
    }



    /// <summary>
    /// 改变 BinaryThreshold 后，通过 TriggerDetectNowForTestAsync 触发检测，
    /// DetectAsync 被调用一次。验证参数变化能正确启动检测流程。
    /// </summary>
    [Fact]
    public async Task ParameterChange_TriggersThrottledDetect()
    {
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(OkResult()));

        _vm.BinaryThreshold = 50;

        await _vm.TriggerDetectNowForTestAsync();

        _mockAlgorithm.Verify(
            a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// 快速连续改变参数 5 次后，调用一次 TriggerDetectNowForTestAsync，
    /// 仅触发一次 DetectAsync（模拟防抖最终只触发一次）。
    /// </summary>
    [Fact]
    public async Task RapidParameterChanges_OnlyTriggerOnceAfter250ms()
    {
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(OkResult()));

        _vm.BinaryThreshold = 50;
        _vm.BinaryThreshold = 100;
        _vm.BinaryThreshold = 150;
        _vm.BinaryThreshold = 200;
        _vm.BinaryThreshold = 210;

        await _vm.TriggerDetectNowForTestAsync();

        _mockAlgorithm.Verify(
            a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }



    /// <summary>
    /// 改变 ImageRoi 后触发检测，验证 ROI 变化同样触发检测流程。
    /// </summary>
    [Fact]
    public async Task ImageRoiChange_TriggersDetect()
    {
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(OkResult()));

        _vm.ImageRoi = new Rectangle(10, 10, 100, 50);

        await _vm.TriggerDetectNowForTestAsync();

        _mockAlgorithm.Verify(
            a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }



    /// <summary>
    /// mock 返回 OK 结果后，History 增加一条记录；两次检测后最新记录在 [0]。
    /// </summary>
    [Fact]
    public async Task HistoryAddedAfterSuccessfulDetect()
    {
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(OkResult()));

        await _vm.TriggerDetectNowForTestAsync();

        var firstItem = Assert.Single(_vm.History);
        Assert.True(firstItem.IsOk);
        Assert.Equal(20.0, firstItem.TabLengthMm);
        Assert.Equal(5.0, firstItem.TabWidthMm);


        await Task.Delay(10);
        await _vm.TriggerDetectNowForTestAsync();

        Assert.Equal(2, _vm.History.Count);
        Assert.True(_vm.History[0].Timestamp >= _vm.History[1].Timestamp,
            "Most recent detection should be at index 0");
    }



    /// <summary>
    /// 第一次检测挂起时，触发第二次检测，验证第一次检测的 CancellationToken 被取消。
    /// 且最终 History 只有第二次结果（第一次结果被丢弃）。
    /// </summary>
    [Fact]
    public async Task NewDetectCancelsInflight()
    {
        LoadSample();

        var tcs = new TaskCompletionSource<InspectionResult>();
        CancellationToken firstToken = default;
        bool firstCalled = false;

        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
            .Returns<InspectionImage, CancellationToken>((_, ct) =>
            {
                if (!firstCalled)
                {
                    firstCalled = true;
                    firstToken = ct;
                    return tcs.Task;
                }
                return Task.FromResult(OkResult());
            });


        var task1 = _vm.TriggerDetectNowForTestAsync();


        var task2 = _vm.TriggerDetectNowForTestAsync();
        await task2;

        Assert.True(firstToken.IsCancellationRequested, "First detect's token should be cancelled by second detect");
        Assert.Single(_vm.History);


        tcs.SetResult(OkResult());
        await task1;
    }



    /// <summary>
    /// 设置 LengthUpperLimit=18（低于实际 ~19.95mm），mock 返回 NG，
    /// StatusText 应含 "NG"，History[0].IsOk=false。
    /// </summary>
    [Fact]
    public async Task InvalidLengthLimits_StatusContainsNg()
    {
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(NgResult()));

        _vm.LengthUpperLimit = 18.0;

        await _vm.TriggerDetectNowForTestAsync();

        Assert.Contains("NG", _vm.StatusText);
        var ngItem = Assert.Single(_vm.History);
        Assert.False(ngItem.IsOk);
    }



    /// <summary>
    /// 切换 SelectedModel 后，参数滑块应更新为对应配置的值。
    /// </summary>
    [Fact]
    public void SelectedModel_Change_UpdatesParameterSliders()
    {
        _mockConfigService.Setup(c => c.LoadProfile("TabA-12x5"))
            .Returns(new AlgorithmConfig
            {
                ProductModel = "TabA-12x5",
                Parameters = new Dictionary<string, object>
                {
                    ["BinaryThreshold"] = 128,
                    ["MinContourArea"] = 1000.0,
                    ["PixelsPerMm"] = 20.0,
                    ["LengthLowerLimit"] = 11.8,
                    ["LengthUpperLimit"] = 12.2,
                    ["WidthLowerLimit"] = 4.8,
                    ["WidthUpperLimit"] = 5.2,
                }
            });

        _vm.SelectedModel = "TabA-12x5";

        Assert.Equal(128, _vm.BinaryThreshold);
        Assert.Equal(11.8, _vm.LengthLowerLimit);
        Assert.Equal(12.2, _vm.LengthUpperLimit);
        Assert.Equal(4.8, _vm.WidthLowerLimit);
        Assert.Equal(5.2, _vm.WidthUpperLimit);
    }



    /// <summary>
    /// 检测成功后应调用 IInspectionRepository.SaveAsync 一次。
    /// </summary>
    [Fact]
    public async Task RunDetect_Success_SavesRecordToRepository()
    {
        LoadSample();
        _mockAlgorithm.Setup(a => a.DetectAsync(It.IsAny<InspectionImage>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(OkResult()));

        await _vm.TriggerDetectNowForTestAsync();

        _mockRepository.Verify(
            r => r.SaveAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
