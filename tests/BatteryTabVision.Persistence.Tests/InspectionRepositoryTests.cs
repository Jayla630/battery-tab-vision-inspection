using BatteryTabVision.Core.Models;
using BatteryTabVision.Persistence.Services;
using FreeSql;
using Xunit;

namespace BatteryTabVision.Persistence.Tests;

public class InspectionRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IFreeSql _fsql;
    private readonly InspectionRepository _repository;

    public InspectionRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _fsql = PersistenceSetup.CreateFreeSql(_dbPath);
        _repository = new InspectionRepository(_fsql);
    }

    public void Dispose()
    {
        _fsql.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task InspectionRepository_SaveAndRetrieve()
    {
        var record = new InspectionRecord
        {
            ProductModel = "TabA-12x5",
            EngineName = "TestEngine",
            IsOk = true,
            ElapsedMs = 42,
            TabLengthMm = 12.0,
            TabWidthMm = 5.0,
            AlgorithmParamsJson = "{}"
        };

        await _repository.SaveAsync(record);

        var records = await _repository.GetRecentAsync(50);
        var saved = Assert.Single(records);
        Assert.Equal("TabA-12x5", saved.ProductModel);
        Assert.True(saved.IsOk);
        Assert.Equal(42, saved.ElapsedMs);
        Assert.Equal(12.0, saved.TabLengthMm);
        Assert.Equal(5.0, saved.TabWidthMm);
    }

    [Fact]
    public async Task InspectionRepository_GetRecent_RespectsCount()
    {
        for (int i = 0; i < 60; i++)
        {
            await _repository.SaveAsync(new InspectionRecord
            {
                ProductModel = "TabA-12x5",
                EngineName = "TestEngine",
                IsOk = true,
                AlgorithmParamsJson = "{}"
            });
        }

        var records = await _repository.GetRecentAsync(50);

        Assert.Equal(50, records.Count);
    }

    [Fact]
    public async Task InspectionRepository_OrderedByIdDesc()
    {
        await _repository.SaveAsync(new InspectionRecord
        {
            ProductModel = "First",
            EngineName = "TestEngine",
            IsOk = true,
            AlgorithmParamsJson = "{}"
        });

        await Task.Delay(10);

        await _repository.SaveAsync(new InspectionRecord
        {
            ProductModel = "Second",
            EngineName = "TestEngine",
            IsOk = false,
            AlgorithmParamsJson = "{}"
        });

        var records = await _repository.GetRecentAsync(50);

        Assert.True(records.Count >= 2);
        Assert.Equal("Second", records[0].ProductModel);
        Assert.Equal("First", records[1].ProductModel);
    }

    [Fact]
    public void PersistenceSetup_CreateFreeSql_CreatesDbFile()
    {
        Assert.True(File.Exists(_dbPath),
            $"Database file should exist at: {_dbPath}");
    }
}
