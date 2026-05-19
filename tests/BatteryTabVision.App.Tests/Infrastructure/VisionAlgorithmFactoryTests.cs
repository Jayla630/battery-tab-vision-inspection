using BatteryTabVision.App.Infrastructure;
using BatteryTabVision.Core.Abstractions;
using Moq;
using Prism.Ioc;
using Xunit;

namespace BatteryTabVision.App.Tests.Infrastructure;

/// <summary>
/// VisionAlgorithmFactory 的单元测试。
/// 验证 AvailableEngines 返回预期列表、Create 正确处理已知/未知引擎。
/// </summary>
public class VisionAlgorithmFactoryTests
{
    private static readonly IVisionAlgorithm MockAlgorithm = Mock.Of<IVisionAlgorithm>();

    private static VisionAlgorithmFactory CreateFactory()
    {
        var mockContainer = new Mock<IContainerProvider>();
        mockContainer
            .Setup(c => c.Resolve(typeof(IVisionAlgorithm), It.IsAny<string>()))
            .Returns((Type t, string name) =>
                name == "OpenCV" ? MockAlgorithm
                    : throw new InvalidOperationException($"No registration for engine '{name}'"));

        return new VisionAlgorithmFactory(mockContainer.Object);
    }

    /// <summary>
    /// AvailableEngines 应包含 OpenCV 和 Halcon。
    /// </summary>
    [Fact]
    public void AvailableEngines_ContainsBothEngines()
    {
        var factory = CreateFactory();

        var engines = factory.AvailableEngines;

        Assert.Equal(2, engines.Count);
        Assert.Contains("OpenCV", engines);
        Assert.Contains("Halcon", engines);
    }

    /// <summary>
    /// 已知引擎名 "OpenCV" 应成功创建。
    /// </summary>
    [Fact]
    public void Create_KnownEngine_ReturnsAlgorithm()
    {
        var factory = CreateFactory();

        var algorithm = factory.Create("OpenCV");

        Assert.NotNull(algorithm);
    }

    /// <summary>
    /// 未知引擎名应抛 ArgumentException。
    /// </summary>
    [Fact]
    public void Create_UnknownEngine_ThrowsArgumentException()
    {
        var factory = CreateFactory();

        var ex = Assert.Throws<ArgumentException>(() => factory.Create("UnknownEngine"));
        Assert.Contains("UnknownEngine", ex.Message);
    }

    /// <summary>
    /// 空字符串引擎名应抛 ArgumentException。
    /// </summary>
    [Fact]
    public void Create_EmptyEngineName_ThrowsArgumentException()
    {
        var factory = CreateFactory();

        Assert.Throws<ArgumentException>(() => factory.Create(""));
    }
}
