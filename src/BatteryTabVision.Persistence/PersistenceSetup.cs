using BatteryTabVision.Persistence.Models;
using FreeSql;

namespace BatteryTabVision.Persistence;

/// <summary>FreeSql 工厂，负责创建 IFreeSql 实例并配置实体映射。</summary>
public static class PersistenceSetup
{
    /// <summary>创建并初始化 IFreeSql（自动建库建表）。</summary>
    public static IFreeSql CreateFreeSql(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath)!;
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={dbPath}")
            .UseAutoSyncStructure(true)
            .UseNoneCommandParameter(true)
            .Build();

        return fsql;
    }
}
