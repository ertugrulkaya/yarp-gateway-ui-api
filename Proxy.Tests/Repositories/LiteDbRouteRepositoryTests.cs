using LiteDB;
using Moq;
using Proxy.Host.Models;
using Proxy.Host.Repositories;
using Proxy.Host.Services;
using Xunit;

namespace Proxy.Tests.Repositories;

public class LiteDbRouteRepositoryTests : IDisposable
{
    private readonly LiteDatabase _liteDb = new(":memory:");
    private readonly LiteDbRouteRepository _repo;

    public LiteDbRouteRepositoryTests()
    {
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c["LiteDb:Path"]).Returns(":memory:");

        var mockDbService = new Mock<LiteDbService>(mockConfig.Object);
        mockDbService.Setup(s => s.Database).Returns(_liteDb);

        _repo = new LiteDbRouteRepository(mockDbService.Object);
    }

    private static RouteDto MakeRoute(string id, string cluster = "c1") =>
        new() { RouteId = id, ClusterId = cluster };

    [Fact]
    public void GetAll_WhenEmpty_ReturnsEmptyList()
    {
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void Insert_ThenGetAll_ReturnsInsertedItem()
    {
        _repo.Insert(MakeRoute("r1"));

        var all = _repo.GetAll().ToList();
        Assert.Single(all);
        Assert.Equal("r1", all[0].RouteId);
    }

    [Fact]
    public void FindById_WhenExists_ReturnsDto()
    {
        _repo.Insert(MakeRoute("r1"));

        var found = _repo.FindById("r1");

        Assert.NotNull(found);
        Assert.Equal("r1", found.RouteId);
    }

    [Fact]
    public void FindById_WhenNotExists_ReturnsNull()
    {
        Assert.Null(_repo.FindById("nonexistent"));
    }

    [Fact]
    public void ExistsById_WhenExists_ReturnsTrue()
    {
        _repo.Insert(MakeRoute("r1"));
        Assert.True(_repo.ExistsById("r1"));
    }

    [Fact]
    public void ExistsById_WhenNotExists_ReturnsFalse()
    {
        Assert.False(_repo.ExistsById("nonexistent"));
    }

    [Fact]
    public void Upsert_WhenNotExists_InsertsNew()
    {
        _repo.Upsert(MakeRoute("r1"));

        Assert.True(_repo.ExistsById("r1"));
    }

    [Fact]
    public void Upsert_WhenExists_UpdatesValue()
    {
        _repo.Insert(MakeRoute("r1", "c1"));
        _repo.Upsert(MakeRoute("r1", "c2"));

        var updated = _repo.FindById("r1");
        Assert.Equal("c2", updated!.ClusterId);
    }

    [Fact]
    public void Delete_WhenExists_ReturnsTrueAndRemoves()
    {
        _repo.Insert(MakeRoute("r1"));

        var deleted = _repo.Delete("r1");

        Assert.True(deleted);
        Assert.False(_repo.ExistsById("r1"));
    }

    [Fact]
    public void Delete_WhenNotExists_ReturnsFalse()
    {
        Assert.False(_repo.Delete("nonexistent"));
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        _repo.Insert(MakeRoute("r1"));
        _repo.Insert(MakeRoute("r2"));
        _repo.Insert(MakeRoute("r3"));

        Assert.Equal(3, _repo.Count());
    }

    public void Dispose() => _liteDb.Dispose();
}
