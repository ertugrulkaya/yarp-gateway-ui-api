using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Proxy.Host.Models;
using Proxy.Host.Providers;
using Proxy.Host.Services;

namespace Proxy.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProxyConfigController : ControllerBase
{
    private readonly LiteDbService _db;
    private readonly LiteDbProxyConfigProvider _provider;

    public ProxyConfigController(LiteDbService db, LiteDbProxyConfigProvider provider)
    {
        _db = db;
        _provider = provider;
    }

    // ── Bulk ─────────────────────────────────────────────────────────────────

    [HttpGet("raw")]
    public IActionResult GetRawConfig()
    {
        var routes = _db.Database.GetCollection<RouteConfigWrapper>("routes")
            .FindAll().Select(x => x.Config);
        var clusters = _db.Database.GetCollection<ClusterConfigWrapper>("clusters")
            .FindAll().Select(x => x.Config);
        return Ok(new ProxyConfigPayload { Routes = routes.ToList(), Clusters = clusters.ToList() });
    }

    [HttpPost("raw")]
    public IActionResult UpdateRawConfig([FromBody] ProxyConfigPayload payload)
    {
        try
        {
            var routesCol = _db.Database.GetCollection<RouteConfigWrapper>("routes");
            var clustersCol = _db.Database.GetCollection<ClusterConfigWrapper>("clusters");

            routesCol.DeleteAll();
            clustersCol.DeleteAll();

            if (payload.Routes?.Count > 0)
                routesCol.InsertBulk(payload.Routes.Select(r => new RouteConfigWrapper { RouteId = r.RouteId, Config = r }));

            if (payload.Clusters?.Count > 0)
                clustersCol.InsertBulk(payload.Clusters.Select(c => new ClusterConfigWrapper { ClusterId = c.ClusterId, Config = c }));

            _provider.UpdateConfig();
            return Ok(new { Message = "Configuration updated." });
        }
        catch (Exception ex) { return BadRequest(new { Error = ex.Message }); }
    }

    // ── Routes ───────────────────────────────────────────────────────────────

    [HttpGet("routes")]
    public IActionResult GetRoutes()
    {
        var routes = _db.Database.GetCollection<RouteConfigWrapper>("routes")
            .FindAll().Select(x => x.Config).ToList();
        return Ok(routes);
    }

    [HttpPost("routes")]
    public IActionResult AddRoute([FromBody] RouteDto route)
    {
        if (string.IsNullOrWhiteSpace(route.RouteId))
            return BadRequest(new { Error = "RouteId is required." });
        var col = _db.Database.GetCollection<RouteConfigWrapper>("routes");
        if (col.FindById(route.RouteId) != null)
            return Conflict(new { Error = $"Route '{route.RouteId}' already exists." });

        col.Insert(new RouteConfigWrapper { RouteId = route.RouteId, Config = route });
        _provider.UpdateConfig();
        return Ok(new { Message = "Route added." });
    }

    [HttpPut("routes/{routeId}")]
    public IActionResult UpdateRoute(string routeId, [FromBody] RouteDto route)
    {
        var col = _db.Database.GetCollection<RouteConfigWrapper>("routes");
        if (col.FindById(routeId) == null)
            return NotFound(new { Error = $"Route '{routeId}' not found." });

        route.RouteId = routeId;
        col.Upsert(new RouteConfigWrapper { RouteId = routeId, Config = route });
        _provider.UpdateConfig();
        return Ok(new { Message = "Route updated." });
    }

    [HttpDelete("routes/{routeId}")]
    public IActionResult DeleteRoute(string routeId)
    {
        var col = _db.Database.GetCollection<RouteConfigWrapper>("routes");
        if (!col.Delete(routeId))
            return NotFound(new { Error = $"Route '{routeId}' not found." });

        _provider.UpdateConfig();
        return Ok(new { Message = "Route deleted." });
    }

    // ── Clusters ─────────────────────────────────────────────────────────────

    [HttpGet("clusters")]
    public IActionResult GetClusters()
    {
        var clusters = _db.Database.GetCollection<ClusterConfigWrapper>("clusters")
            .FindAll().Select(x => x.Config).ToList();
        return Ok(clusters);
    }

    [HttpPost("clusters")]
    public IActionResult AddCluster([FromBody] ClusterDto cluster)
    {
        if (string.IsNullOrWhiteSpace(cluster.ClusterId))
            return BadRequest(new { Error = "ClusterId is required." });
        var col = _db.Database.GetCollection<ClusterConfigWrapper>("clusters");
        if (col.FindById(cluster.ClusterId) != null)
            return Conflict(new { Error = $"Cluster '{cluster.ClusterId}' already exists." });

        col.Insert(new ClusterConfigWrapper { ClusterId = cluster.ClusterId, Config = cluster });
        _provider.UpdateConfig();
        return Ok(new { Message = "Cluster added." });
    }

    [HttpPut("clusters/{clusterId}")]
    public IActionResult UpdateCluster(string clusterId, [FromBody] ClusterDto cluster)
    {
        var col = _db.Database.GetCollection<ClusterConfigWrapper>("clusters");
        if (col.FindById(clusterId) == null)
            return NotFound(new { Error = $"Cluster '{clusterId}' not found." });

        cluster.ClusterId = clusterId;
        col.Upsert(new ClusterConfigWrapper { ClusterId = clusterId, Config = cluster });
        _provider.UpdateConfig();
        return Ok(new { Message = "Cluster updated." });
    }

    [HttpDelete("clusters/{clusterId}")]
    public IActionResult DeleteCluster(string clusterId)
    {
        var col = _db.Database.GetCollection<ClusterConfigWrapper>("clusters");
        if (!col.Delete(clusterId))
            return NotFound(new { Error = $"Cluster '{clusterId}' not found." });

        _provider.UpdateConfig();
        return Ok(new { Message = "Cluster deleted." });
    }
}
