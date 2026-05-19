using BatteryTabVision.Core.Models;

namespace BatteryTabVision.Core.Services;

/// <summary>按产品型号加载算法参数配置。</summary>
public interface IProfileConfigService
{
    /// <summary>所有可用产品型号名称，UI 绑定到下拉框。</summary>
    IReadOnlyList<string> AvailableModels { get; }

    /// <summary>按型号加载配置，找不到时返回 null。</summary>
    AlgorithmConfig? LoadProfile(string productModel);
}
