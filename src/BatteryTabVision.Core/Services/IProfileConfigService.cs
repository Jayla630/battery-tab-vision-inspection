using BatteryTabVision.Core.Models;

namespace BatteryTabVision.Core.Services;

/// <summary>按产品型号加载算法参数配置。</summary>
public interface IProfileConfigService
{
    /// <summary>所有可用产品型号名称，UI 绑定到下拉框。</summary>
    IReadOnlyList<string> AvailableModels { get; }

    /// <summary>按型号加载配置，找不到时返回 null。</summary>
    AlgorithmConfig? LoadProfile(string productModel);

    /// <summary>新增或更新一个产品型号的参数配置，并持久化到 JSON 文件。</summary>
    void UpsertProfile(string productModel, IDictionary<string, object> parameters);

    /// <summary>删除指定产品型号，并持久化到 JSON 文件。</summary>
    void DeleteProfile(string productModel);
}
