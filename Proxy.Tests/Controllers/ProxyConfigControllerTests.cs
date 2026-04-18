using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Proxy.Host.Controllers;
using Proxy.Host.Models;
using Proxy.Host.Providers;
using Proxy.Host.Repositories;
using Proxy.Host.Services;
using System.Security.Claims;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Proxy.Tests.Controllers;

public class ProxyConfigControllerTests
{
    private readonly Mock<IRouteRepository>          _routeRepo   = new();
    private readonly Mock<IClusterRepository>        _clusterRepo = new();
    private readonly Mock<IConfigValidator>          _validator   = new();
    private readonly Mock<LiteDbService>             _db;
    private readonly Mock<LiteDbProxyConfigProvider> _provider;
    private readonly Mock<LogService>                _logService;
    private readonly Mock<HistoryService>            _historyService;

    public ProxyConfigControllerTests()
    {
        var cfg = new Mock<IConfiguration>();
        cfg.Setup(c => c["LiteDb:Path"]).Returns(":memory:");
        cfg.Setup(c => c["LiteDb:LogPath"]).Returns(":memory:");

        _db = new Mock<LiteDbService>(cfg.Object);
        // Provider constructor calls LoadFromDb() → needs a real (empty) LiteDatabase
        _db.Setup(d => d.Database).Returns(new LiteDB.LiteDatabase(":memory:"));

        _provider       = new Mock<LiteDbProxyConfigProvider>(_db.Object, Mock.Of<ILogger<LiteDbProxyConfigProvider>>());
        _logService     = new Mock<LogService>(cfg.Object);
        _historyService = new Mock<HistoryService>(_db.Object);

        _validator.Setup(v => v.ValidateRouteAsync(It.IsAny<RouteConfig>()))
                  .Returns(ValueTask.FromResult<IList<Exception>>(new List<Exception>()));
        _validator.Setup(v => v.ValidateClusterAsync(It.IsAny<ClusterConfig>()))
                  .Returns(ValueTask.FromResult<IList<Exception>>(new List<Exception>()));
    }

    private ProxyConfigController CreateController()
    {
        var ctrl = new ProxyConfigController(
            _routeRepo.Object, _clusterRepo.Object,
            _db.Object, _provider.Object,
            _validator.Object, _logService.Object, _historyService.Object);

        // Simulate authenticated user for RecordHistory
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, "test-user") }, "test"))
            }
        };
        return ctrl;
    }

    // ── GetRoutes ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetRoutes_ReturnsOk_WithAllRoutes()
    {
        _routeRepo.Setup(r => r.GetAll()).Returns(new List<RouteDto>
        {
            new() { RouteId = "r1", ClusterId = "c1" },
            new() { RouteId = "r2", ClusterId = "c1" },
        });

        var result = CreateController().GetRoutes();

        var ok = Assert.IsType<OkObjectResult>(result);
        var routes = Assert.IsAssignableFrom<IEnumerable<RouteDto>>(ok.Value);
        Assert.Equal(2, routes.Count());
    }

    // ── AddRoute ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddRoute_WhenRouteIdMissing_ReturnsBadRequest()
    {
        var result = await CreateController().AddRoute(new RouteDto { RouteId = "" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddRoute_WhenRouteAlreadyExists_ReturnsConflict()
    {
        _routeRepo.Setup(r => r.ExistsById("r1")).Returns(true);

        var result = await CreateController().AddRoute(new RouteDto { RouteId = "r1" });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task AddRoute_WhenClusterNotFound_ReturnsUnprocessable()
    {
        _routeRepo.Setup(r => r.ExistsById("r1")).Returns(false);
        _clusterRepo.Setup(c => c.ExistsById("missing-cluster")).Returns(false);

        var result = await CreateController().AddRoute(
            new RouteDto { RouteId = "r1", ClusterId = "missing-cluster" });

        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    [Fact]
    public async Task AddRoute_WhenValid_InsertsAndReturnsOk()
    {
        _routeRepo.Setup(r => r.ExistsById("r1")).Returns(false);
        _clusterRepo.Setup(c => c.ExistsById("c1")).Returns(true);

        var result = await CreateController().AddRoute(
            new RouteDto { RouteId = "r1", ClusterId = "c1" });

        Assert.IsType<OkObjectResult>(result);
        _routeRepo.Verify(r => r.Insert(It.Is<RouteDto>(d => d.RouteId == "r1")), Times.Once);
        _provider.Verify(p => p.UpdateConfig("route", "r1", false), Times.Once);
    }

    // ── UpdateRoute ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRoute_WhenNotFound_ReturnsNotFound()
    {
        _routeRepo.Setup(r => r.FindById("r1")).Returns((RouteDto?)null);

        var result = await CreateController().UpdateRoute("r1", new RouteDto());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateRoute_WhenValid_UpsertsAndReturnsOk()
    {
        _routeRepo.Setup(r => r.FindById("r1")).Returns(new RouteDto { RouteId = "r1", ClusterId = "c1" });
        _clusterRepo.Setup(c => c.ExistsById("c1")).Returns(true);

        var result = await CreateController().UpdateRoute("r1",
            new RouteDto { RouteId = "r1", ClusterId = "c1" });

        Assert.IsType<OkObjectResult>(result);
        _routeRepo.Verify(r => r.Upsert(It.Is<RouteDto>(d => d.RouteId == "r1")), Times.Once);
    }

    // ── DeleteRoute ───────────────────────────────────────────────────────────

    [Fact]
    public void DeleteRoute_WhenNotFound_ReturnsNotFound()
    {
        _routeRepo.Setup(r => r.FindById("r1")).Returns((RouteDto?)null);

        var result = CreateController().DeleteRoute("r1");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void DeleteRoute_WhenExists_DeletesAndReturnsOk()
    {
        _routeRepo.Setup(r => r.FindById("r1")).Returns(new RouteDto { RouteId = "r1" });

        var result = CreateController().DeleteRoute("r1");

        Assert.IsType<OkObjectResult>(result);
        _routeRepo.Verify(r => r.Delete("r1"), Times.Once);
        _provider.Verify(p => p.UpdateConfig("route", "r1", true), Times.Once);
    }

    // ── GetClusters ───────────────────────────────────────────────────────────

    [Fact]
    public void GetClusters_ReturnsOk_WithAllClusters()
    {
        _clusterRepo.Setup(c => c.GetAll()).Returns(new List<ClusterDto>
        {
            new() { ClusterId = "c1" },
        });

        var result = CreateController().GetClusters();

        var ok = Assert.IsType<OkObjectResult>(result);
        var clusters = Assert.IsAssignableFrom<IEnumerable<ClusterDto>>(ok.Value);
        Assert.Single(clusters);
    }

    // ── AddCluster ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddCluster_WhenClusterIdMissing_ReturnsBadRequest()
    {
        var result = await CreateController().AddCluster(new ClusterDto { ClusterId = "" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddCluster_WhenAlreadyExists_ReturnsConflict()
    {
        _clusterRepo.Setup(c => c.ExistsById("c1")).Returns(true);

        var result = await CreateController().AddCluster(new ClusterDto { ClusterId = "c1" });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task AddCluster_WhenValid_InsertsAndReturnsOk()
    {
        _clusterRepo.Setup(c => c.ExistsById("c1")).Returns(false);

        var result = await CreateController().AddCluster(new ClusterDto { ClusterId = "c1" });

        Assert.IsType<OkObjectResult>(result);
        _clusterRepo.Verify(c => c.Insert(It.Is<ClusterDto>(d => d.ClusterId == "c1")), Times.Once);
    }

    // ── DeleteCluster ─────────────────────────────────────────────────────────

    [Fact]
    public void DeleteCluster_WhenNotFound_ReturnsNotFound()
    {
        _clusterRepo.Setup(c => c.FindById("c1")).Returns((ClusterDto?)null);

        var result = CreateController().DeleteCluster("c1");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void DeleteCluster_WhenExists_DeletesAndReturnsOk()
    {
        _clusterRepo.Setup(c => c.FindById("c1")).Returns(new ClusterDto { ClusterId = "c1" });

        var result = CreateController().DeleteCluster("c1");

        Assert.IsType<OkObjectResult>(result);
        _clusterRepo.Verify(c => c.Delete("c1"), Times.Once);
    }

    // ── GetSummary ────────────────────────────────────────────────────────────

    [Fact]
    public void GetSummary_ReturnsCorrectCounts()
    {
        _routeRepo.Setup(r => r.Count()).Returns(4);
        _clusterRepo.Setup(c => c.Count()).Returns(3);
        _logService.Setup(l => l.GetTotalCount(null, null, null, null)).Returns(1000);
        _logService.Setup(l => l.CountErrors(It.IsAny<DateTime>())).Returns(2);

        var result = CreateController().GetSummary();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"routes\":4", json);
        Assert.Contains("\"clusters\":3", json);
    }
}
