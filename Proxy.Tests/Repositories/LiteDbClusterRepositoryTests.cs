using LiteDB;
using Moq;
using Proxy.Host.Models;
using Proxy.Host.Repositories;
using Proxy.Host.Services;
using Xunit;

namespace Proxy.Tests.Repositories;

public class LiteDbClusterRepositoryTests : IDisposable
{
    private readonly LiteDatabase _liteDb = new(":memory:");
    private readonly LiteDbClusterRepository _repo;

    public LiteDbClusterRepositoryTests()
    {
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c["LiteDb:Path"]).Returns(":memory:");

        var mockDbService = new Mock<LiteDbService>(mockConfig.Object);
        mockDbService.Setup(s => s.Database).Returns(_liteDb);

        _repo = new LiteDbClusterRepository(mockDbService.Object);
    }

    private static ClusterDto MakeCluster(string id) => new() { ClusterId = id };

    [Fact]
    public void GetAll_WhenEmpty_ReturnsEmptyList()
    {
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void Insert_ThenFindById_ReturnsInsertedItem()
    {
        _repo.Insert(MakeCluster("c1"));

        var found = _repo.FindById("c1");
        Assert.NotNull(found);
        Assert.Equal("c1", found.ClusterId);
    }

    [Fact]
    public void FindById_WhenNotExists_ReturnsNull()
    {
        Assert.Null(_repo.FindById("nonexistent"));
    }

    [Fact]
    public void ExistsById_WhenExists_ReturnsTrue()
    {
        _repo.Insert(MakeCluster("c1"));
        Assert.True(_repo.ExistsById("c1"));
    }

    [Fact]
    public void ExistsById_WhenNotExists_ReturnsFalse()
    {
        Assert.False(_repo.ExistsById("nonexistent"));
    }

    [Fact]
    public void Upsert_WhenNotExists_InsertsNew()
    {
        _repo.Upsert(MakeCluster("c1"));
        Assert.True(_repo.ExistsById("c1"));
    }

    [Fact]
    public void Upsert_WhenExists_UpdatesValue()
    {
        _repo.Insert(new ClusterDto { ClusterId = "c1", LoadBalancingPolicy = "RoundRobin" });
        _repo.Upsert(new ClusterDto { ClusterId = "c1", LoadBalancingPolicy = "LeastRequests" });

        var updated = _repo.FindById("c1");
        Assert.Equal("LeastRequests", updated!.LoadBalancingPolicy);
    }

    [Fact]
    public void Delete_WhenExists_ReturnsTrueAndRemoves()
    {
        _repo.Insert(MakeCluster("c1"));

        var deleted = _repo.Delete("c1");

        Assert.True(deleted);
        Assert.False(_repo.ExistsById("c1"));
    }

    [Fact]
    public void Delete_WhenNotExists_ReturnsFalse()
    {
        Assert.False(_repo.Delete("nonexistent"));
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        _repo.Insert(MakeCluster("c1"));
        _repo.Insert(MakeCluster("c2"));

        Assert.Equal(2, _repo.Count());
    }

    public void Dispose() => _liteDb.Dispose();
}
