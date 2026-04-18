using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Proxy.Host.Models;
using Proxy.Host.Providers;
using Proxy.Host.Services;
using System.Security.Claims;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;

namespace Proxy.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProxyConfigController : ControllerBase
{
    private readonly LiteDbService _db;
    private readonly LiteDbProxyConfigProvider _provider;
    private readonly IConfigValidator _validator;

    public ProxyConfigController(LiteDbService db, LiteDbProxyConfigProvider provider, IConfigValidator validator)
    {
        _db = db;
        _provider = provider;
        _validator = validator;
    }

    private async Task<IActionResult?> ValidateRouteAsync(RouteDto dto)
    {
        var route = new RouteConfig
        {
            RouteId = dto.RouteId ?? string.Empty,
            ClusterId = dto.ClusterId,
            Order = dto.Order,
            Match = new RouteMatch
            {
                Path = dto.Match?.Path,
                Methods = dto.Match?.Methods,
                Hosts = dto.Match?.Hosts,
            },
        };
        var errors = await _validator.ValidateRouteAsync(route);
        if (errors.Count > 0)
            return UnprocessableEntity(new ApiError("INVALID_ROUTE",
                string.Join(" ", errors.Select(e => e.Message))));
        return null;
    }

    private async Task<IActionResult?> ValidateClusterAsync(ClusterDto dto)
    {
        var dests = (dto.Destinations ?? new()).ToDictionary(
            kv => kv.Key,
            kv => new DestinationConfig { Address = kv.Value.Address ?? string.Empty });
        var cluster = new ClusterConfig
        {
            ClusterId = dto.ClusterId ?? string.Empty,
            LoadBalancingPolicy = dto.LoadBalancingPolicy,
            Destinations = dests,
        };
        var errors = await _validator.ValidateClusterAsync(cluster);
        if (errors.Count > 0)
            return UnprocessableEntity(new ApiError("INVALID_CLUSTER",
                string.Join(" ", errors.Select(e => e.Message))));
        return null;
    }

    private void RecordHistory(string entityType, string entityId, string action, object? oldValue, object? newValue)
    {
        var history = _db.Database.GetCollection<ConfigHistory>("config_history");
        history.Insert(new ConfigHistory
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ChangedBy = User.FindFirstValue(ClaimTypes.Name) ?? "unknown",
            ChangedAt = DateTime.UtcNow,
            OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue),
        });
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
    public async Task<IActionResult> UpdateRawConfig([FromBody] ProxyConfigPayload payload)
    {
        // Cross-reference validation before touching the DB
        if (payload.Routes?.Count > 0 && payload.Clusters != null)
        {
            var clusterIds = payload.Clusters.Select(c => c.ClusterId).ToHashSet();
            var invalid = payload.Routes
                .Where(r => !string.IsNullOrWhiteSpace(r.ClusterId) && !clusterIds.Contains(r.ClusterId!))
                .Select(r => r.ClusterId!)
                .Distinct()
                .ToList();
            if (invalid.Count > 0)
                return UnprocessableEntity(new ApiError("UNPROCESSABLE",
                    $"Route(s) reference unknown cluster(s): {string.Join(", ", invalid)}."));
        }

        // YARP config validation before touching the DB
        foreach (var route in payload.Routes ?? [])
        {
            var err = await ValidateRouteAsync(route);
            if (err != null) return err;
        }
        foreach (var cluster in payload.Clusters ?? [])
        {
            var err = await ValidateClusterAsync(cluster);
            if (err != null) return err;
        }

        var db = _db.Database;
        db.BeginTrans();
        try
        {
            var routesCol = db.GetCollection<RouteConfigWrapper>("routes");
            var clustersCol = db.GetCollection<ClusterConfigWrapper>("clusters");

            routesCol.DeleteAll();
            clustersCol.DeleteAll();

            if (payload.Routes?.Count > 0)
                routesCol.InsertBulk(payload.Routes.Select(r => new RouteConfigWrapper { RouteId = r.RouteId, Config = r }));

            if (payload.Clusters?.Count > 0)
                clustersCol.InsertBulk(payload.Clusters.Select(c => new ClusterConfigWrapper { ClusterId = c.ClusterId, Config = c }));

            db.Commit();
            _provider.UpdateConfig();
            return Ok(new { Message = "Configuration updated." });
        }
        catch (Exception ex)
        {
            db.Rollback();
            return BadRequest(new ApiError("BAD_REQUEST", ex.Message));
        }
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
    public async Task<IActionResult> AddRoute([FromBody] RouteDto route)
    {
        if (string.IsNullOrWhiteSpace(route.RouteId))
            return BadRequest(new ApiError("BAD_REQUEST", "RouteId is required."));
        var col = _db.Database.GetCollection<RouteConfigWrapper>("routes");
        if (col.FindById(route.RouteId) != null)
            return Conflict(new ApiError("CONFLICT", $"Route '{route.RouteId}' already exists."));
        if (!string.IsNullOrWhiteSpace(route.ClusterId))
        {
            var clusterExists = _db.Database.GetCollection<ClusterConfigWrapper>("clusters").FindById(route.ClusterId) != null;
            if (!clusterExists)
                return UnprocessableEntity(new ApiError("UNPROCESSABLE", $"Cluster '{route.ClusterId}' not found."));
        }
        var validationError = await ValidateRouteAsync(route);
        if (validationError != null) return validationError;

        col.Insert(new RouteConfigWrapper { RouteId = route.RouteId, Config = route });
        RecordHistory("route", route.RouteId, "create", null, route);
        _provider.UpdateConfig();
        return Ok(new { Message = "Route added." });
    }

    [HttpPut("routes/{routeId}")]
    public async Task<IActionResult> UpdateRoute(string routeId, [FromBody] RouteDto route)
    {
        var col = _db.Database.GetCollection<RouteConfigWrapper>("routes");
        if (col.FindById(routeId) == null)
            return NotFound(new ApiError("NOT_FOUND", $"Route '{routeId}' not found."));

        var oldRoute = col.FindById(routeId)?.Config;
        if (!string.IsNullOrWhiteSpace(route.ClusterId))
        {
            var clusterExists = _db.Database.GetCollection<ClusterConfigWrapper>("clusters").FindById(route.ClusterId) != null;
            if (!clusterExists)
                return UnprocessableEntity(new ApiError("UNPROCESSABLE", $"Cluster '{route.ClusterId}' not found."));
        }
        route.RouteId = routeId;
        var validationError = await ValidateRouteAsync(route);
        if (validationError != null) return validationError;

        col.Upsert(new RouteConfigWrapper { RouteId = routeId, Config = route });
        RecordHistory("route", routeId, "update", oldRoute, route);
        _provider.UpdateConfig();
        return Ok(new { Message = "Route updated." });
    }

    [HttpDelete("routes/{routeId}")]
    public IActionResult DeleteRoute(string routeId)
    {
        var col = _db.Database.GetCollection<RouteConfigWrapper>("routes");
        var existing = col.FindById(routeId);
        if (existing == null)
            return NotFound(new ApiError("NOT_FOUND", $"Route '{routeId}' not found."));

        col.Delete(routeId);
        RecordHistory("route", routeId, "delete", existing.Config, null);
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
    public async Task<IActionResult> AddCluster([FromBody] ClusterDto cluster)
    {
        if (string.IsNullOrWhiteSpace(cluster.ClusterId))
            return BadRequest(new ApiError("BAD_REQUEST", "ClusterId is required."));
        var col = _db.Database.GetCollection<ClusterConfigWrapper>("clusters");
        if (col.FindById(cluster.ClusterId) != null)
            return Conflict(new ApiError("CONFLICT", $"Cluster '{cluster.ClusterId}' already exists."));
        var validationError = await ValidateClusterAsync(cluster);
        if (validationError != null) return validationError;

        col.Insert(new ClusterConfigWrapper { ClusterId = cluster.ClusterId, Config = cluster });
        RecordHistory("cluster", cluster.ClusterId, "create", null, cluster);
        _provider.UpdateConfig();
        return Ok(new { Message = "Cluster added." });
    }

    [HttpPut("clusters/{clusterId}")]
    public async Task<IActionResult> UpdateCluster(string clusterId, [FromBody] ClusterDto cluster)
    {
        var col = _db.Database.GetCollection<ClusterConfigWrapper>("clusters");
        var oldCluster = col.FindById(clusterId);
        if (oldCluster == null)
            return NotFound(new ApiError("NOT_FOUND", $"Cluster '{clusterId}' not found."));

        cluster.ClusterId = clusterId;
        var validationError = await ValidateClusterAsync(cluster);
        if (validationError != null) return validationError;

        col.Upsert(new ClusterConfigWrapper { ClusterId = clusterId, Config = cluster });
        RecordHistory("cluster", clusterId, "update", oldCluster.Config, cluster);
        _provider.UpdateConfig();
        return Ok(new { Message = "Cluster updated." });
    }

    [HttpDelete("clusters/{clusterId}")]
    public IActionResult DeleteCluster(string clusterId)
    {
        var col = _db.Database.GetCollection<ClusterConfigWrapper>("clusters");
        var existing = col.FindById(clusterId);
        if (existing == null)
            return NotFound(new ApiError("NOT_FOUND", $"Cluster '{clusterId}' not found."));

        col.Delete(clusterId);
        RecordHistory("cluster", clusterId, "delete", existing.Config, null);
        _provider.UpdateConfig();
        return Ok(new { Message = "Cluster deleted." });
    }

    // ── Backup / Restore ─────────────────────────────────────────────────────

    [HttpGet("backup")]
    public IActionResult Backup()
    {
        var routes = _db.Database.GetCollection<RouteConfigWrapper>("routes")
            .FindAll().Select(x => x.Config).ToList();
        var clusters = _db.Database.GetCollection<ClusterConfigWrapper>("clusters")
            .FindAll().Select(x => x.Config).ToList();

        var payload = new ProxyConfigPayload { Routes = routes, Clusters = clusters };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var filename = $"proxy-config-{DateTime.UtcNow:yyyy-MM-dd_HHmmss}.json";

        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", filename);
    }

    [HttpPost("restore")]
    public IActionResult Restore([FromBody] ProxyConfigPayload payload)
    {
        if (payload.Routes == null && payload.Clusters == null)
            return BadRequest(new ApiError("BAD_REQUEST", "Payload contains no routes or clusters."));

        var db = _db.Database;
        db.BeginTrans();
        try
        {
            var routesCol = db.GetCollection<RouteConfigWrapper>("routes");
            var clustersCol = db.GetCollection<ClusterConfigWrapper>("clusters");

            routesCol.DeleteAll();
            clustersCol.DeleteAll();

            if (payload.Routes?.Count > 0)
                routesCol.InsertBulk(payload.Routes.Select(r => new RouteConfigWrapper { RouteId = r.RouteId, Config = r }));
            if (payload.Clusters?.Count > 0)
                clustersCol.InsertBulk(payload.Clusters.Select(c => new ClusterConfigWrapper { ClusterId = c.ClusterId, Config = c }));

            db.Commit();
            RecordHistory("system", "all", "restore", null, payload);
            _provider.UpdateConfig();
            return Ok(new { Message = $"Restored {payload.Routes?.Count ?? 0} routes and {payload.Clusters?.Count ?? 0} clusters." });
        }
        catch (Exception ex)
        {
            db.Rollback();
            return BadRequest(new ApiError("BAD_REQUEST", ex.Message));
        }
    }

    // ── History ──────────────────────────────────────────────────────────────

    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        var db = _db.Database;
        int total;
        List<ConfigHistory> items;
        db.BeginTrans();
        try
        {
            var col = db.GetCollection<ConfigHistory>("config_history");
            total = col.Count();
            items = col.Query()
                .OrderByDescending(x => x.ChangedAt)
                .Offset(offset)
                .Limit(limit)
                .ToList();
            db.Commit();
        }
        catch { db.Rollback(); throw; }
        return Ok(new { data = items, total });
    }
}
