# YARP Advanced Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose the full YARP configuration surface (routes + clusters) through a tab-based UI and individual REST endpoints.

**Architecture:** Backend DTOs are expanded to mirror YARP's native `RouteConfig` / `ClusterConfig` completely. New individual CRUD endpoints replace bulk-only save for route/cluster operations. Angular dialogs are rewritten with 5-tab Material forms; dashboard switches to individual API calls.

**Tech Stack:** ASP.NET Core 10, YARP 2.3.0, LiteDB 5, Angular 21, Angular Material 21, Reactive Forms

---

## File Map

| Action | File |
|--------|------|
| Modify | `Proxy.Host/Models/ConfigDtos.cs` |
| Modify | `Proxy.Host/Providers/LiteDbProxyConfigProvider.cs` |
| Modify | `Proxy.Host/Controllers/ProxyConfigController.cs` |
| Modify | `Proxy.UI/src/app/services/proxy-config.ts` |
| Modify | `Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.ts` |
| Create | `Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.html` |
| Modify | `Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.ts` |
| Create | `Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.html` |
| Modify | `Proxy.UI/src/app/pages/dashboard/dashboard.ts` |

---

## Task 1: Expand Backend DTOs

**Files:**
- Modify: `Proxy.Host/Models/ConfigDtos.cs`

- [ ] **Step 1: Replace ConfigDtos.cs with expanded model**

```csharp
// Proxy.Host/Models/ConfigDtos.cs
using LiteDB;
using Yarp.ReverseProxy.Configuration;

namespace Proxy.Host.Models;

// ── Route ──────────────────────────────────────────────────────────────────

public class RouteDto
{
    public string RouteId { get; set; } = string.Empty;
    public string? ClusterId { get; set; }
    public int? Order { get; set; }
    public RouteMatchDto Match { get; set; } = new();
    public List<Dictionary<string, string>>? Transforms { get; set; }
    public string? AuthorizationPolicy { get; set; }
    public string? CorsPolicy { get; set; }
    public string? RateLimiterPolicy { get; set; }
    public string? TimeoutPolicy { get; set; }
    public string? OutputCachePolicy { get; set; }
    public long? MaxRequestBodySize { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class RouteMatchDto
{
    public string? Path { get; set; }
    public List<string>? Methods { get; set; }
    public List<string>? Hosts { get; set; }
    public List<RouteHeaderDto>? Headers { get; set; }
    public List<RouteQueryParameterDto>? QueryParameters { get; set; }
}

public class RouteHeaderDto
{
    public string Name { get; set; } = string.Empty;
    public List<string>? Values { get; set; }
    public string? Mode { get; set; }
    public bool IsCaseSensitive { get; set; }
}

public class RouteQueryParameterDto
{
    public string Name { get; set; } = string.Empty;
    public List<string>? Values { get; set; }
    public string? Mode { get; set; }
    public bool IsCaseSensitive { get; set; }
}

// ── Cluster ─────────────────────────────────────────────────────────────────

public class ClusterDto
{
    public string ClusterId { get; set; } = string.Empty;
    public string? LoadBalancingPolicy { get; set; }
    public Dictionary<string, DestinationDto> Destinations { get; set; } = new();
    public SessionAffinityDto? SessionAffinity { get; set; }
    public HealthCheckDto? HealthCheck { get; set; }
    public HttpClientDto? HttpClient { get; set; }
    public HttpRequestDto? HttpRequest { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class DestinationDto
{
    public string Address { get; set; } = string.Empty;
    public string? Health { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class SessionAffinityDto
{
    public bool? Enabled { get; set; }
    public string? Policy { get; set; }
    public string? FailurePolicy { get; set; }
    public string? AffinityKeyName { get; set; }
    public SessionAffinityCookieDto? Cookie { get; set; }
}

public class SessionAffinityCookieDto
{
    public string? Path { get; set; }
    public string? Domain { get; set; }
    public bool? HttpOnly { get; set; }
    public bool? IsEssential { get; set; }
    public string? SameSite { get; set; }
    public string? SecurePolicy { get; set; }
}

public class HealthCheckDto
{
    public ActiveHealthCheckDto? Active { get; set; }
    public PassiveHealthCheckDto? Passive { get; set; }
}

public class ActiveHealthCheckDto
{
    public bool? Enabled { get; set; }
    public string? Interval { get; set; }   // TimeSpan string e.g. "00:00:15"
    public string? Timeout { get; set; }
    public string? Policy { get; set; }
    public string? Path { get; set; }
}

public class PassiveHealthCheckDto
{
    public bool? Enabled { get; set; }
    public string? Policy { get; set; }
    public string? ReactivationPeriod { get; set; }
}

public class HttpClientDto
{
    public bool? DangerousAcceptAnyServerCertificate { get; set; }
    public int? MaxConnectionsPerServer { get; set; }
    public bool? EnableMultipleHttp1Connections { get; set; }
    public bool? EnableMultipleHttp2Connections { get; set; }
    public string? RequestHeaderEncoding { get; set; }   // e.g. "utf-8"
    public string? ResponseHeaderEncoding { get; set; }
}

public class HttpRequestDto
{
    public string? ActivityTimeout { get; set; }   // TimeSpan string
    public string? Version { get; set; }            // "1.0" | "1.1" | "2" | "3"
    public string? VersionPolicy { get; set; }      // RequestVersionOrLower | RequestVersionOrHigher | RequestVersionExact
    public bool? AllowResponseBuffering { get; set; }
}

// ── LiteDB Wrappers ──────────────────────────────────────────────────────────

public class RouteConfigWrapper
{
    [BsonId]
    public string RouteId { get; set; } = string.Empty;
    public RouteDto Config { get; set; } = new();
}

public class ClusterConfigWrapper
{
    [BsonId]
    public string ClusterId { get; set; } = string.Empty;
    public ClusterDto Config { get; set; } = new();
}

// ── API Payload ──────────────────────────────────────────────────────────────

public class ProxyConfigPayload
{
    public List<RouteDto> Routes { get; set; } = new();
    public List<ClusterDto> Clusters { get; set; } = new();
}
```

- [ ] **Step 2: Build backend to verify no compile errors**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.Host && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/User/Desktop/Proxy
git add Proxy.Host/Models/ConfigDtos.cs
git commit -m "feat: expand ConfigDtos with full YARP advanced config model"
```

---

## Task 2: Expand Provider Mapping

**Files:**
- Modify: `Proxy.Host/Providers/LiteDbProxyConfigProvider.cs`

- [ ] **Step 1: Replace LiteDbProxyConfigProvider.cs with expanded mapping**

```csharp
// Proxy.Host/Providers/LiteDbProxyConfigProvider.cs
using LiteDB;
using Microsoft.Extensions.Primitives;
using Proxy.Host.Models;
using Proxy.Host.Services;
using System.Net.Http;
using System.Text;
using Yarp.ReverseProxy.Configuration;

namespace Proxy.Host.Providers;

public class LiteDbProxyConfigProvider : IProxyConfigProvider
{
    private readonly LiteDbService _liteDbService;
    private LiteDbProxyConfig _config;

    public LiteDbProxyConfigProvider(LiteDbService liteDbService)
    {
        _liteDbService = liteDbService;
        _config = LoadFromDb();
    }

    public IProxyConfig GetConfig() => _config;

    public void UpdateConfig()
    {
        var oldConfig = _config;
        _config = LoadFromDb();
        oldConfig.SignalChange();
    }

    private LiteDbProxyConfig LoadFromDb()
    {
        var routes = _liteDbService.Database
            .GetCollection<RouteConfigWrapper>("routes")
            .FindAll().Select(x => MapRoute(x.Config)).ToList();

        var clusters = _liteDbService.Database
            .GetCollection<ClusterConfigWrapper>("clusters")
            .FindAll().Select(x => MapCluster(x.Config)).ToList();

        return new LiteDbProxyConfig(routes, clusters);
    }

    private static TimeSpan? ParseTimeSpan(string? value) =>
        string.IsNullOrEmpty(value) ? null :
        TimeSpan.TryParse(value, out var ts) ? ts : null;

    private static RouteConfig MapRoute(RouteDto dto)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>>? transforms = null;
        if (dto.Transforms?.Count > 0)
        {
            transforms = dto.Transforms
                .Select(t => (IReadOnlyDictionary<string, string>)
                    t.ToDictionary(k => k.Key, v => v.Value))
                .ToList();
        }

        return new RouteConfig
        {
            RouteId = dto.RouteId,
            ClusterId = dto.ClusterId,
            Order = dto.Order,
            AuthorizationPolicy = dto.AuthorizationPolicy,
            CorsPolicy = dto.CorsPolicy,
            RateLimiterPolicy = dto.RateLimiterPolicy,
            TimeoutPolicy = dto.TimeoutPolicy,
            OutputCachePolicy = dto.OutputCachePolicy,
            MaxRequestBodySize = dto.MaxRequestBodySize,
            Metadata = dto.Metadata,
            Transforms = transforms,
            Match = new RouteMatch
            {
                Path = dto.Match.Path,
                Methods = dto.Match.Methods,
                Hosts = dto.Match.Hosts,
                Headers = dto.Match.Headers?.Select(h => new RouteHeader
                {
                    Name = h.Name,
                    Values = h.Values,
                    IsCaseSensitive = h.IsCaseSensitive,
                    Mode = Enum.TryParse<HeaderMatchMode>(h.Mode, true, out var hm)
                        ? hm : HeaderMatchMode.ExactHeader,
                }).ToList(),
                QueryParameters = dto.Match.QueryParameters?.Select(q => new RouteQueryParameter
                {
                    Name = q.Name,
                    Values = q.Values,
                    IsCaseSensitive = q.IsCaseSensitive,
                    Mode = Enum.TryParse<QueryParameterMatchMode>(q.Mode, true, out var qm)
                        ? qm : QueryParameterMatchMode.Exact,
                }).ToList(),
            },
        };
    }

    private static ClusterConfig MapCluster(ClusterDto dto)
    {
        var destinations = dto.Destinations.ToDictionary(
            kvp => kvp.Key,
            kvp => new DestinationConfig
            {
                Address = kvp.Value.Address,
                Health = kvp.Value.Health,
                Metadata = kvp.Value.Metadata,
            });

        SessionAffinityConfig? sessionAffinity = null;
        if (dto.SessionAffinity is { } sa)
        {
            SessionAffinityCookieConfig? cookie = null;
            if (sa.Cookie is { } sc)
            {
                cookie = new SessionAffinityCookieConfig
                {
                    Path = sc.Path,
                    Domain = sc.Domain,
                    HttpOnly = sc.HttpOnly,
                    IsEssential = sc.IsEssential,
                    SameSite = Enum.TryParse<Microsoft.AspNetCore.Http.SameSiteMode>(
                        sc.SameSite, true, out var ss) ? ss : null,
                    SecurePolicy = Enum.TryParse<Microsoft.AspNetCore.Http.CookieSecurePolicy>(
                        sc.SecurePolicy, true, out var sp) ? sp : null,
                };
            }
            sessionAffinity = new SessionAffinityConfig
            {
                Enabled = sa.Enabled,
                Policy = sa.Policy,
                FailurePolicy = sa.FailurePolicy,
                AffinityKeyName = sa.AffinityKeyName,
                Cookie = cookie,
            };
        }

        HealthCheckConfig? healthCheck = null;
        if (dto.HealthCheck is { } hc)
        {
            healthCheck = new HealthCheckConfig
            {
                Active = hc.Active == null ? null : new ActiveHealthCheckConfig
                {
                    Enabled = hc.Active.Enabled,
                    Interval = ParseTimeSpan(hc.Active.Interval),
                    Timeout = ParseTimeSpan(hc.Active.Timeout),
                    Policy = hc.Active.Policy,
                    Path = hc.Active.Path,
                },
                Passive = hc.Passive == null ? null : new PassiveHealthCheckConfig
                {
                    Enabled = hc.Passive.Enabled,
                    Policy = hc.Passive.Policy,
                    ReactivationPeriod = ParseTimeSpan(hc.Passive.ReactivationPeriod),
                },
            };
        }

        HttpClientConfig? httpClient = null;
        if (dto.HttpClient is { } hcl)
        {
            httpClient = new HttpClientConfig
            {
                DangerousAcceptAnyServerCertificate = hcl.DangerousAcceptAnyServerCertificate,
                MaxConnectionsPerServer = hcl.MaxConnectionsPerServer,
                EnableMultipleHttp1Connections = hcl.EnableMultipleHttp1Connections,
                EnableMultipleHttp2Connections = hcl.EnableMultipleHttp2Connections,
                RequestHeaderEncoding = string.IsNullOrEmpty(hcl.RequestHeaderEncoding)
                    ? null : Encoding.GetEncoding(hcl.RequestHeaderEncoding),
                ResponseHeaderEncoding = string.IsNullOrEmpty(hcl.ResponseHeaderEncoding)
                    ? null : Encoding.GetEncoding(hcl.ResponseHeaderEncoding),
            };
        }

        ForwarderRequestConfig? httpRequest = null;
        if (dto.HttpRequest is { } hr)
        {
            httpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = ParseTimeSpan(hr.ActivityTimeout),
                Version = string.IsNullOrEmpty(hr.Version) ? null : new Version(hr.Version),
                VersionPolicy = Enum.TryParse<HttpVersionPolicy>(hr.VersionPolicy, true, out var vp)
                    ? vp : null,
                AllowResponseBuffering = hr.AllowResponseBuffering,
            };
        }

        return new ClusterConfig
        {
            ClusterId = dto.ClusterId,
            LoadBalancingPolicy = dto.LoadBalancingPolicy,
            Destinations = destinations,
            SessionAffinity = sessionAffinity,
            HealthCheck = healthCheck,
            HttpClient = httpClient,
            HttpRequest = httpRequest,
            Metadata = dto.Metadata,
        };
    }
}

public class LiteDbProxyConfig : IProxyConfig
{
    private readonly CancellationTokenSource _cts = new();

    public LiteDbProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new Microsoft.Extensions.Primitives.CancellationChangeToken(_cts.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public Microsoft.Extensions.Primitives.IChangeToken ChangeToken { get; }

    internal void SignalChange() => _cts.Cancel();
}
```

- [ ] **Step 2: Build to verify**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.Host && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/User/Desktop/Proxy
git add Proxy.Host/Providers/LiteDbProxyConfigProvider.cs
git commit -m "feat: expand provider mapping for full YARP advanced config"
```

---

## Task 3: Add Individual CRUD API Endpoints

**Files:**
- Modify: `Proxy.Host/Controllers/ProxyConfigController.cs`

- [ ] **Step 1: Replace ProxyConfigController.cs**

```csharp
// Proxy.Host/Controllers/ProxyConfigController.cs
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
```

- [ ] **Step 2: Build to verify**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.Host && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/User/Desktop/Proxy
git add Proxy.Host/Controllers/ProxyConfigController.cs
git commit -m "feat: add individual CRUD endpoints for routes and clusters"
```

---

## Task 4: Update Angular Service & Types

**Files:**
- Modify: `Proxy.UI/src/app/services/proxy-config.ts`

- [ ] **Step 1: Replace proxy-config.ts with typed interfaces and new methods**

```typescript
// Proxy.UI/src/app/services/proxy-config.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin } from 'rxjs';

// ── Interfaces ────────────────────────────────────────────────────────────────

export interface RouteHeaderConfig {
  name: string;
  values?: string[];
  mode?: string;
  isCaseSensitive?: boolean;
}

export interface RouteQueryParameterConfig {
  name: string;
  values?: string[];
  mode?: string;
  isCaseSensitive?: boolean;
}

export interface RouteMatchConfig {
  path?: string;
  methods?: string[];
  hosts?: string[];
  headers?: RouteHeaderConfig[];
  queryParameters?: RouteQueryParameterConfig[];
}

export interface RouteConfig {
  routeId: string;
  clusterId?: string;
  order?: number;
  match: RouteMatchConfig;
  transforms?: Record<string, string>[];
  authorizationPolicy?: string;
  corsPolicy?: string;
  rateLimiterPolicy?: string;
  timeoutPolicy?: string;
  outputCachePolicy?: string;
  maxRequestBodySize?: number;
  metadata?: Record<string, string>;
}

export interface DestinationConfig {
  address: string;
  health?: string;
  metadata?: Record<string, string>;
}

export interface SessionAffinityConfig {
  enabled?: boolean;
  policy?: string;
  failurePolicy?: string;
  affinityKeyName?: string;
  cookie?: {
    path?: string;
    domain?: string;
    httpOnly?: boolean;
    isEssential?: boolean;
    sameSite?: string;
    securePolicy?: string;
  };
}

export interface HealthCheckConfig {
  active?: {
    enabled?: boolean;
    interval?: string;
    timeout?: string;
    policy?: string;
    path?: string;
  };
  passive?: {
    enabled?: boolean;
    policy?: string;
    reactivationPeriod?: string;
  };
}

export interface HttpClientConfig {
  dangerousAcceptAnyServerCertificate?: boolean;
  maxConnectionsPerServer?: number;
  enableMultipleHttp1Connections?: boolean;
  enableMultipleHttp2Connections?: boolean;
  requestHeaderEncoding?: string;
  responseHeaderEncoding?: string;
}

export interface HttpRequestConfig {
  activityTimeout?: string;
  version?: string;
  versionPolicy?: string;
  allowResponseBuffering?: boolean;
}

export interface ClusterConfig {
  clusterId: string;
  loadBalancingPolicy?: string;
  destinations: Record<string, DestinationConfig>;
  sessionAffinity?: SessionAffinityConfig;
  healthCheck?: HealthCheckConfig;
  httpClient?: HttpClientConfig;
  httpRequest?: HttpRequestConfig;
  metadata?: Record<string, string>;
}

export interface ProxyConfigPayload {
  routes: RouteConfig[];
  clusters: ClusterConfig[];
}

// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class ProxyConfigService {
  private http = inject(HttpClient);
  private base = '/api/proxyconfig';

  // Bulk
  getRawConfig(): Observable<ProxyConfigPayload> {
    return this.http.get<ProxyConfigPayload>(`${this.base}/raw`);
  }
  updateRawConfig(payload: ProxyConfigPayload): Observable<any> {
    return this.http.post(`${this.base}/raw`, payload);
  }

  // Routes
  getRoutes(): Observable<RouteConfig[]> {
    return this.http.get<RouteConfig[]>(`${this.base}/routes`);
  }
  addRoute(route: RouteConfig): Observable<any> {
    return this.http.post(`${this.base}/routes`, route);
  }
  updateRoute(routeId: string, route: RouteConfig): Observable<any> {
    return this.http.put(`${this.base}/routes/${routeId}`, route);
  }
  deleteRoute(routeId: string): Observable<any> {
    return this.http.delete(`${this.base}/routes/${routeId}`);
  }

  // Clusters
  getClusters(): Observable<ClusterConfig[]> {
    return this.http.get<ClusterConfig[]>(`${this.base}/clusters`);
  }
  addCluster(cluster: ClusterConfig): Observable<any> {
    return this.http.post(`${this.base}/clusters`, cluster);
  }
  updateCluster(clusterId: string, cluster: ClusterConfig): Observable<any> {
    return this.http.put(`${this.base}/clusters/${clusterId}`, cluster);
  }
  deleteCluster(clusterId: string): Observable<any> {
    return this.http.delete(`${this.base}/clusters/${clusterId}`);
  }

  // Load both at once (for dashboard init)
  loadAll(): Observable<{ routes: RouteConfig[]; clusters: ClusterConfig[] }> {
    return forkJoin({
      routes: this.getRoutes(),
      clusters: this.getClusters(),
    });
  }
}
```

- [ ] **Step 2: Build frontend to verify TypeScript**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.UI && npm run build -- --configuration development 2>&1 | tail -5
```
Expected: no TypeScript errors

- [ ] **Step 3: Commit**

```bash
cd /c/Users/User/Desktop/Proxy
git add Proxy.UI/src/app/services/proxy-config.ts
git commit -m "feat: expand Angular proxy-config service with full types and individual CRUD"
```

---

## Task 5: Rewrite Route Dialog (5 tabs)

**Files:**
- Modify: `Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.ts`
- Create: `Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.html`

- [ ] **Step 1: Replace route-dialog.ts**

```typescript
// Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.ts
import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouteConfig } from '../../../services/proxy-config';

export interface RouteDialogData {
  route?: RouteConfig;
  clusters: { clusterId: string }[];
  existingRoutes: RouteConfig[];
}

@Component({
  selector: 'app-route-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatTabsModule,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatSelectModule,
    MatCheckboxModule, MatChipsModule, MatIconModule, MatDividerModule,
    MatTooltipModule,
  ],
  templateUrl: './route-dialog.html',
  styles: [`
    .tab-content { display: flex; flex-direction: column; gap: 12px; padding: 16px 0; }
    .full-width { width: 100%; }
    .row { display: flex; gap: 8px; align-items: flex-start; }
    .row mat-form-field { flex: 1; }
    .section-label { font-weight: 500; color: #555; margin-top: 8px; margin-bottom: 4px; font-size: 13px; }
    .array-row { display: flex; gap: 8px; align-items: center; margin-bottom: 4px; }
    .array-row mat-form-field { flex: 1; }
    .preset-row { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 12px; }
    .transform-block { border: 1px solid #e0e0e0; border-radius: 4px; padding: 10px; margin-bottom: 8px; }
    .transform-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px; font-size: 13px; color: #666; }
    mat-dialog-content { min-height: 380px; }
  `],
})
export class RouteDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<RouteDialogComponent>);

  readonly separatorKeyCodes = [ENTER, COMMA];
  hosts: string[] = [];
  form: FormGroup;

  constructor(@Inject(MAT_DIALOG_DATA) public data: RouteDialogData) {
    const r = data.route;
    this.hosts = r?.match?.hosts ? [...r.match.hosts] : [];

    this.form = this.fb.group({
      routeId: [r?.routeId ?? '', [
        Validators.required,
        (ctrl: any) => {
          if (!ctrl.value || ctrl.value === r?.routeId) return null;
          return data.existingRoutes.some(x => x.routeId.toLowerCase() === ctrl.value.toLowerCase())
            ? { uniqueId: true } : null;
        },
      ]],
      clusterId: [r?.clusterId ?? '', Validators.required],
      path: [r?.match?.path ?? '', Validators.required],
      methods: [r?.match?.methods ?? []],
      headers: this.fb.array(
        (r?.match?.headers ?? []).map(h => this.newHeaderGroup(h.name, (h.values ?? []).join(','), h.mode, h.isCaseSensitive))
      ),
      queryParameters: this.fb.array(
        (r?.match?.queryParameters ?? []).map(q => this.newQpGroup(q.name, (q.values ?? []).join(','), q.mode, q.isCaseSensitive))
      ),
      transforms: this.fb.array(
        (r?.transforms ?? []).map(t =>
          this.fb.group({
            entries: this.fb.array(Object.entries(t).map(([k, v]) => this.fb.group({ key: [k], value: [v] })))
          })
        )
      ),
      authorizationPolicy: [r?.authorizationPolicy ?? ''],
      corsPolicy: [r?.corsPolicy ?? ''],
      rateLimiterPolicy: [r?.rateLimiterPolicy ?? ''],
      timeoutPolicy: [r?.timeoutPolicy ?? ''],
      outputCachePolicy: [r?.outputCachePolicy ?? ''],
      maxRequestBodySize: [r?.maxRequestBodySize ?? null],
      order: [r?.order ?? null],
      metadata: this.fb.array(
        Object.entries(r?.metadata ?? {}).map(([k, v]) => this.fb.group({ key: [k], value: [v] }))
      ),
    });
  }

  // ── Accessors ──────────────────────────────────────────────────────────────

  get headers() { return this.form.get('headers') as FormArray; }
  get queryParameters() { return this.form.get('queryParameters') as FormArray; }
  get transforms() { return this.form.get('transforms') as FormArray; }
  get metadata() { return this.form.get('metadata') as FormArray; }
  getTransformEntries(i: number) { return this.transforms.at(i).get('entries') as FormArray; }

  // ── Hosts (chip list) ──────────────────────────────────────────────────────

  addHost(event: MatChipInputEvent) {
    const v = (event.value ?? '').trim();
    if (v) this.hosts.push(v);
    event.chipInput!.clear();
  }
  removeHost(h: string) { this.hosts = this.hosts.filter(x => x !== h); }

  // ── Headers ────────────────────────────────────────────────────────────────

  private newHeaderGroup(name = '', value = '', mode = 'ExactHeader', cs = false) {
    return this.fb.group({ name: [name], value: [value], mode: [mode], isCaseSensitive: [cs] });
  }
  addHeader() { this.headers.push(this.newHeaderGroup()); }
  removeHeader(i: number) { this.headers.removeAt(i); }

  // ── Query Parameters ───────────────────────────────────────────────────────

  private newQpGroup(name = '', value = '', mode = 'Exact', cs = false) {
    return this.fb.group({ name: [name], value: [value], mode: [mode], isCaseSensitive: [cs] });
  }
  addQueryParameter() { this.queryParameters.push(this.newQpGroup()); }
  removeQueryParameter(i: number) { this.queryParameters.removeAt(i); }

  // ── Transforms ─────────────────────────────────────────────────────────────

  addTransform() {
    this.transforms.push(this.fb.group({
      entries: this.fb.array([this.fb.group({ key: [''], value: [''] })])
    }));
  }

  addPresetTransform(preset: string) {
    const map: Record<string, Array<{ key: string; value: string }>> = {
      pathRemovePrefix:       [{ key: 'PathRemovePrefix', value: '' }],
      pathSet:                [{ key: 'PathSet', value: '' }],
      pathPattern:            [{ key: 'PathPattern', value: '' }],
      requestHeader:          [{ key: 'RequestHeader', value: '' }, { key: 'Set', value: '' }],
      responseHeader:         [{ key: 'ResponseHeader', value: '' }, { key: 'Set', value: '' }],
      requestHeadersCopy:     [{ key: 'RequestHeadersCopy', value: 'true' }],
      requestHeaderOrigHost:  [{ key: 'RequestHeaderOriginalHost', value: 'true' }],
      xForwardedPrefix:       [{ key: 'RequestHeader', value: 'X-Forwarded-Prefix' }, { key: 'Set', value: '' }],
    };
    const entries = (map[preset] ?? []).map(e => this.fb.group({ key: [e.key], value: [e.value] }));
    this.transforms.push(this.fb.group({ entries: this.fb.array(entries) }));
  }

  removeTransform(i: number) { this.transforms.removeAt(i); }
  addTransformEntry(ti: number) { this.getTransformEntries(ti).push(this.fb.group({ key: [''], value: [''] })); }
  removeTransformEntry(ti: number, ei: number) { this.getTransformEntries(ti).removeAt(ei); }

  // ── Metadata ───────────────────────────────────────────────────────────────

  addMetadata() { this.metadata.push(this.fb.group({ key: [''], value: [''] })); }
  removeMetadata(i: number) { this.metadata.removeAt(i); }

  // ── Save ───────────────────────────────────────────────────────────────────

  onSave() {
    if (this.form.invalid) return;
    const v = this.form.value;

    const result: RouteConfig = {
      routeId: v.routeId,
      clusterId: v.clusterId || undefined,
      order: v.order ?? undefined,
      match: {
        path: v.path || undefined,
        methods: v.methods?.length ? v.methods : undefined,
        hosts: this.hosts.length ? this.hosts : undefined,
        headers: v.headers?.length
          ? v.headers.map((h: any) => ({
              name: h.name,
              values: h.value ? h.value.split(',').map((s: string) => s.trim()).filter(Boolean) : undefined,
              mode: h.mode || undefined,
              isCaseSensitive: h.isCaseSensitive,
            }))
          : undefined,
        queryParameters: v.queryParameters?.length
          ? v.queryParameters.map((q: any) => ({
              name: q.name,
              values: q.value ? q.value.split(',').map((s: string) => s.trim()).filter(Boolean) : undefined,
              mode: q.mode || undefined,
              isCaseSensitive: q.isCaseSensitive,
            }))
          : undefined,
      },
      transforms: v.transforms?.length
        ? v.transforms.map((t: any) => {
            const dict: Record<string, string> = {};
            t.entries.forEach((e: any) => { if (e.key) dict[e.key] = e.value; });
            return dict;
          }).filter((d: any) => Object.keys(d).length > 0)
        : undefined,
      authorizationPolicy: v.authorizationPolicy || undefined,
      corsPolicy: v.corsPolicy || undefined,
      rateLimiterPolicy: v.rateLimiterPolicy || undefined,
      timeoutPolicy: v.timeoutPolicy || undefined,
      outputCachePolicy: v.outputCachePolicy || undefined,
      maxRequestBodySize: v.maxRequestBodySize ?? undefined,
      metadata: v.metadata?.length
        ? Object.fromEntries(v.metadata.map((m: any) => [m.key, m.value]))
        : undefined,
    };

    this.dialogRef.close(result);
  }

  onCancel() { this.dialogRef.close(); }
}
```

- [ ] **Step 2: Create route-dialog.html**

```html
<!-- Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.html -->
<h2 mat-dialog-title>{{ data.route ? 'Edit' : 'Add' }} Route</h2>

<mat-dialog-content>
  <form [formGroup]="form">
    <mat-tab-group dynamicHeight>

      <!-- ── Tab 1: Match ─────────────────────────────────────────────── -->
      <mat-tab label="Match">
        <div class="tab-content">

          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>Route ID</mat-label>
              <input matInput formControlName="routeId" [readonly]="!!data.route" placeholder="my-api-route">
              @if (form.get('routeId')?.hasError('uniqueId')) {
                <mat-error>Route ID already exists</mat-error>
              }
              @if (form.get('routeId')?.hasError('required')) {
                <mat-error>Required</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Cluster</mat-label>
              <mat-select formControlName="clusterId">
                @for (c of data.clusters; track c.clusterId) {
                  <mat-option [value]="c.clusterId">{{ c.clusterId }}</mat-option>
                }
              </mat-select>
              @if (form.get('clusterId')?.hasError('required')) {
                <mat-error>Required</mat-error>
              }
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Match Path</mat-label>
            <input matInput formControlName="path" placeholder="/api/{**catch-all}">
            <mat-hint>Supports YARP path templates with {**catch-all}</mat-hint>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>HTTP Methods</mat-label>
            <mat-select formControlName="methods" multiple>
              @for (m of ['GET','POST','PUT','DELETE','PATCH','HEAD','OPTIONS']; track m) {
                <mat-option [value]="m">{{ m }}</mat-option>
              }
            </mat-select>
            <mat-hint>Empty = all methods allowed</mat-hint>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Hosts</mat-label>
            <mat-chip-grid #hostGrid>
              @for (h of hosts; track h) {
                <mat-chip-row (removed)="removeHost(h)">
                  {{ h }}
                  <button matChipRemove><mat-icon>cancel</mat-icon></button>
                </mat-chip-row>
              }
              <input placeholder="example.com (Enter to add)"
                     [matChipInputFor]="hostGrid"
                     [matChipInputSeparatorKeyCodes]="separatorKeyCodes"
                     (matChipInputTokenEnd)="addHost($event)">
            </mat-chip-grid>
            <mat-hint>Empty = match all hosts</mat-hint>
          </mat-form-field>

          <mat-divider></mat-divider>
          <div class="section-label">Request Headers Filter</div>

          <div formArrayName="headers">
            @for (h of headers.controls; track $index; let i = $index) {
              <div [formGroupName]="i" class="array-row">
                <mat-form-field appearance="outline">
                  <mat-label>Name</mat-label>
                  <input matInput formControlName="name">
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Value(s)</mat-label>
                  <input matInput formControlName="value" placeholder="Comma-separated">
                </mat-form-field>
                <mat-form-field appearance="outline" style="max-width:140px">
                  <mat-label>Mode</mat-label>
                  <mat-select formControlName="mode">
                    @for (m of ['ExactHeader','HeaderPrefix','Contains','NotContains','Exists','NotExists']; track m) {
                      <mat-option [value]="m">{{ m }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-checkbox formControlName="isCaseSensitive" matTooltip="Case Sensitive">Cs</mat-checkbox>
                <button mat-icon-button color="warn" (click)="removeHeader(i)">
                  <mat-icon>remove_circle</mat-icon>
                </button>
              </div>
            }
          </div>
          <button mat-stroked-button type="button" (click)="addHeader()">
            <mat-icon>add</mat-icon> Add Header Filter
          </button>

          <mat-divider></mat-divider>
          <div class="section-label">Query Parameters Filter</div>

          <div formArrayName="queryParameters">
            @for (q of queryParameters.controls; track $index; let i = $index) {
              <div [formGroupName]="i" class="array-row">
                <mat-form-field appearance="outline">
                  <mat-label>Name</mat-label>
                  <input matInput formControlName="name">
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Value(s)</mat-label>
                  <input matInput formControlName="value" placeholder="Comma-separated">
                </mat-form-field>
                <mat-form-field appearance="outline" style="max-width:130px">
                  <mat-label>Mode</mat-label>
                  <mat-select formControlName="mode">
                    @for (m of ['Exact','Contains','NotContains','Exists','NotExists']; track m) {
                      <mat-option [value]="m">{{ m }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <mat-checkbox formControlName="isCaseSensitive" matTooltip="Case Sensitive">Cs</mat-checkbox>
                <button mat-icon-button color="warn" (click)="removeQueryParameter(i)">
                  <mat-icon>remove_circle</mat-icon>
                </button>
              </div>
            }
          </div>
          <button mat-stroked-button type="button" (click)="addQueryParameter()">
            <mat-icon>add</mat-icon> Add Query Filter
          </button>

        </div>
      </mat-tab>

      <!-- ── Tab 2: Transforms ────────────────────────────────────────── -->
      <mat-tab label="Transforms">
        <div class="tab-content">
          <div class="section-label">Presets</div>
          <div class="preset-row">
            <button mat-stroked-button type="button" (click)="addPresetTransform('pathRemovePrefix')">PathRemovePrefix</button>
            <button mat-stroked-button type="button" (click)="addPresetTransform('pathSet')">PathSet</button>
            <button mat-stroked-button type="button" (click)="addPresetTransform('pathPattern')">PathPattern</button>
            <button mat-stroked-button type="button" (click)="addPresetTransform('requestHeader')">RequestHeader</button>
            <button mat-stroked-button type="button" (click)="addPresetTransform('responseHeader')">ResponseHeader</button>
            <button mat-stroked-button type="button" (click)="addPresetTransform('requestHeadersCopy')">HeadersCopy</button>
            <button mat-stroked-button type="button" (click)="addPresetTransform('requestHeaderOrigHost')">OriginalHost</button>
            <button mat-stroked-button type="button" (click)="addPresetTransform('xForwardedPrefix')">X-Forwarded-Prefix</button>
          </div>

          <div formArrayName="transforms">
            @for (t of transforms.controls; track $index; let ti = $index) {
              <div class="transform-block">
                <div class="transform-header">
                  <span>Transform {{ ti + 1 }}</span>
                  <button mat-icon-button color="warn" type="button" (click)="removeTransform(ti)">
                    <mat-icon>delete</mat-icon>
                  </button>
                </div>
                <div [formGroupName]="ti" formArrayName="entries">
                  @for (e of getTransformEntries(ti).controls; track $index; let ei = $index) {
                    <div [formGroupName]="ei" class="array-row">
                      <mat-form-field appearance="outline">
                        <mat-label>Key</mat-label>
                        <input matInput formControlName="key">
                      </mat-form-field>
                      <mat-form-field appearance="outline">
                        <mat-label>Value</mat-label>
                        <input matInput formControlName="value">
                      </mat-form-field>
                      <button mat-icon-button type="button" (click)="removeTransformEntry(ti, ei)">
                        <mat-icon>remove</mat-icon>
                      </button>
                    </div>
                  }
                </div>
                <button mat-stroked-button type="button" (click)="addTransformEntry(ti)">
                  <mat-icon>add</mat-icon> Entry
                </button>
              </div>
            }
          </div>

          <button mat-raised-button type="button" (click)="addTransform()">
            <mat-icon>add</mat-icon> Add Custom Transform
          </button>
        </div>
      </mat-tab>

      <!-- ── Tab 3: Policies ──────────────────────────────────────────── -->
      <mat-tab label="Policies">
        <div class="tab-content">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Authorization Policy</mat-label>
            <input matInput formControlName="authorizationPolicy" placeholder="e.g. Default or Anonymous">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>CORS Policy</mat-label>
            <input matInput formControlName="corsPolicy" placeholder="e.g. myCorsPolicy">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Rate Limiter Policy</mat-label>
            <input matInput formControlName="rateLimiterPolicy">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Timeout Policy</mat-label>
            <input matInput formControlName="timeoutPolicy">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Output Cache Policy</mat-label>
            <input matInput formControlName="outputCachePolicy">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Max Request Body Size (bytes)</mat-label>
            <input matInput type="number" formControlName="maxRequestBodySize" placeholder="e.g. 10485760">
            <mat-hint>Leave empty for default. -1 = unlimited.</mat-hint>
          </mat-form-field>
        </div>
      </mat-tab>

      <!-- ── Tab 4: Advanced ──────────────────────────────────────────── -->
      <mat-tab label="Advanced">
        <div class="tab-content">
          <mat-form-field appearance="outline" style="max-width: 200px">
            <mat-label>Order</mat-label>
            <input matInput type="number" formControlName="order" placeholder="e.g. -1, 0, 1">
            <mat-hint>Lower = higher priority. Negative allowed.</mat-hint>
          </mat-form-field>
        </div>
      </mat-tab>

      <!-- ── Tab 5: Metadata ──────────────────────────────────────────── -->
      <mat-tab label="Metadata">
        <div class="tab-content">
          <div formArrayName="metadata">
            @for (m of metadata.controls; track $index; let i = $index) {
              <div [formGroupName]="i" class="array-row">
                <mat-form-field appearance="outline">
                  <mat-label>Key</mat-label>
                  <input matInput formControlName="key">
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Value</mat-label>
                  <input matInput formControlName="value">
                </mat-form-field>
                <button mat-icon-button color="warn" type="button" (click)="removeMetadata(i)">
                  <mat-icon>remove_circle</mat-icon>
                </button>
              </div>
            }
          </div>
          <button mat-stroked-button type="button" (click)="addMetadata()">
            <mat-icon>add</mat-icon> Add Metadata
          </button>
        </div>
      </mat-tab>

    </mat-tab-group>
  </form>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button (click)="onCancel()">Cancel</button>
  <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="onSave()">Save</button>
</mat-dialog-actions>
```

- [ ] **Step 3: Build to verify**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.UI && npm run build -- --configuration development 2>&1 | tail -5
```
Expected: no TypeScript errors

- [ ] **Step 4: Commit**

```bash
cd /c/Users/User/Desktop/Proxy
git add Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.ts
git add Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.html
git commit -m "feat: rewrite route dialog with 5-tab advanced config UI"
```

---

## Task 6: Rewrite Cluster Dialog (5 tabs)

**Files:**
- Modify: `Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.ts`
- Create: `Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.html`

- [ ] **Step 1: Replace cluster-dialog.ts**

```typescript
// Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.ts
import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ClusterConfig } from '../../../services/proxy-config';

export interface ClusterDialogData {
  cluster?: ClusterConfig;
  existingClusters: ClusterConfig[];
}

@Component({
  selector: 'app-cluster-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatTabsModule,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatSelectModule,
    MatCheckboxModule, MatIconModule, MatDividerModule, MatTooltipModule,
  ],
  templateUrl: './cluster-dialog.html',
  styles: [`
    .tab-content { display: flex; flex-direction: column; gap: 12px; padding: 16px 0; }
    .full-width { width: 100%; }
    .row { display: flex; gap: 8px; align-items: flex-start; }
    .row mat-form-field { flex: 1; }
    .section-label { font-weight: 500; color: #555; margin-top: 8px; margin-bottom: 4px; font-size: 13px; }
    .dest-block { border: 1px solid #e0e0e0; border-radius: 4px; padding: 10px; margin-bottom: 8px; }
    .dest-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px; }
    .array-row { display: flex; gap: 8px; align-items: center; margin-bottom: 4px; }
    .array-row mat-form-field { flex: 1; }
    mat-dialog-content { min-height: 380px; }
  `],
})
export class ClusterDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<ClusterDialogComponent>);

  form: FormGroup;

  constructor(@Inject(MAT_DIALOG_DATA) public data: ClusterDialogData) {
    const c = data.cluster;

    this.form = this.fb.group({
      clusterId: [c?.clusterId ?? '', [
        Validators.required,
        (ctrl: any) => {
          if (!ctrl.value || ctrl.value === c?.clusterId) return null;
          return data.existingClusters.some(x => x.clusterId.toLowerCase() === ctrl.value.toLowerCase())
            ? { uniqueId: true } : null;
        },
      ]],
      loadBalancingPolicy: [c?.loadBalancingPolicy ?? ''],
      destinations: this.fb.array(
        Object.entries(c?.destinations ?? { dest1: { address: '' } }).map(([key, d]) =>
          this.newDestGroup(key, d.address, d.health ?? '')
        )
      ),
      // Session Affinity
      saEnabled: [c?.sessionAffinity?.enabled ?? false],
      saPolicy: [c?.sessionAffinity?.policy ?? 'Cookie'],
      saFailurePolicy: [c?.sessionAffinity?.failurePolicy ?? 'Redistribute'],
      saAffinityKeyName: [c?.sessionAffinity?.affinityKeyName ?? ''],
      saCookiePath: [c?.sessionAffinity?.cookie?.path ?? '/'],
      saCookieDomain: [c?.sessionAffinity?.cookie?.domain ?? ''],
      saCookieHttpOnly: [c?.sessionAffinity?.cookie?.httpOnly ?? true],
      saCookieSameSite: [c?.sessionAffinity?.cookie?.sameSite ?? 'Lax'],
      saCookieSecurePolicy: [c?.sessionAffinity?.cookie?.securePolicy ?? 'SameAsRequest'],
      // Health Check
      hcActiveEnabled: [c?.healthCheck?.active?.enabled ?? false],
      hcActiveInterval: [c?.healthCheck?.active?.interval ?? '00:00:15'],
      hcActiveTimeout: [c?.healthCheck?.active?.timeout ?? '00:00:10'],
      hcActivePolicy: [c?.healthCheck?.active?.policy ?? 'ConsecutiveFailures'],
      hcActivePath: [c?.healthCheck?.active?.path ?? '/health'],
      hcPassiveEnabled: [c?.healthCheck?.passive?.enabled ?? false],
      hcPassivePolicy: [c?.healthCheck?.passive?.policy ?? 'TransportFailureRate'],
      hcPassiveReactivation: [c?.healthCheck?.passive?.reactivationPeriod ?? '00:02:00'],
      // HTTP Client
      httpClientDangerousCert: [c?.httpClient?.dangerousAcceptAnyServerCertificate ?? false],
      httpClientMaxConn: [c?.httpClient?.maxConnectionsPerServer ?? null],
      httpClientHttp1: [c?.httpClient?.enableMultipleHttp1Connections ?? false],
      httpClientHttp2: [c?.httpClient?.enableMultipleHttp2Connections ?? false],
      httpClientReqEncoding: [c?.httpClient?.requestHeaderEncoding ?? ''],
      httpClientResEncoding: [c?.httpClient?.responseHeaderEncoding ?? ''],
      // HTTP Request
      httpReqTimeout: [c?.httpRequest?.activityTimeout ?? ''],
      httpReqVersion: [c?.httpRequest?.version ?? ''],
      httpReqVersionPolicy: [c?.httpRequest?.versionPolicy ?? ''],
      httpReqBuffering: [c?.httpRequest?.allowResponseBuffering ?? false],
      // Metadata
      metadata: this.fb.array(
        Object.entries(c?.metadata ?? {}).map(([k, v]) => this.fb.group({ key: [k], value: [v] }))
      ),
    });
  }

  // ── Accessors ──────────────────────────────────────────────────────────────

  get destinations() { return this.form.get('destinations') as FormArray; }
  get metadata() { return this.form.get('metadata') as FormArray; }

  // ── Destinations ───────────────────────────────────────────────────────────

  private newDestGroup(key = 'dest1', address = '', health = '') {
    return this.fb.group({
      key: [key, Validators.required],
      address: [address, [Validators.required, Validators.pattern('https?://.*')]],
      health: [health],
    });
  }
  addDestination() { this.destinations.push(this.newDestGroup(`dest${this.destinations.length + 1}`)); }
  removeDestination(i: number) { if (this.destinations.length > 1) this.destinations.removeAt(i); }

  // ── Metadata ───────────────────────────────────────────────────────────────

  addMetadata() { this.metadata.push(this.fb.group({ key: [''], value: [''] })); }
  removeMetadata(i: number) { this.metadata.removeAt(i); }

  // ── Save ───────────────────────────────────────────────────────────────────

  onSave() {
    if (this.form.invalid) return;
    const v = this.form.value;

    const result: ClusterConfig = {
      clusterId: v.clusterId,
      loadBalancingPolicy: v.loadBalancingPolicy || undefined,
      destinations: Object.fromEntries(
        v.destinations.map((d: any) => [d.key, {
          address: d.address,
          health: d.health || undefined,
        }])
      ),
      sessionAffinity: v.saEnabled ? {
        enabled: true,
        policy: v.saPolicy || undefined,
        failurePolicy: v.saFailurePolicy || undefined,
        affinityKeyName: v.saAffinityKeyName || undefined,
        cookie: {
          path: v.saCookiePath || undefined,
          domain: v.saCookieDomain || undefined,
          httpOnly: v.saCookieHttpOnly,
          sameSite: v.saCookieSameSite || undefined,
          securePolicy: v.saCookieSecurePolicy || undefined,
        },
      } : undefined,
      healthCheck: (v.hcActiveEnabled || v.hcPassiveEnabled) ? {
        active: v.hcActiveEnabled ? {
          enabled: true,
          interval: v.hcActiveInterval || undefined,
          timeout: v.hcActiveTimeout || undefined,
          policy: v.hcActivePolicy || undefined,
          path: v.hcActivePath || undefined,
        } : undefined,
        passive: v.hcPassiveEnabled ? {
          enabled: true,
          policy: v.hcPassivePolicy || undefined,
          reactivationPeriod: v.hcPassiveReactivation || undefined,
        } : undefined,
      } : undefined,
      httpClient: (v.httpClientDangerousCert || v.httpClientMaxConn || v.httpClientHttp1 ||
                   v.httpClientHttp2 || v.httpClientReqEncoding || v.httpClientResEncoding) ? {
        dangerousAcceptAnyServerCertificate: v.httpClientDangerousCert || undefined,
        maxConnectionsPerServer: v.httpClientMaxConn ?? undefined,
        enableMultipleHttp1Connections: v.httpClientHttp1 || undefined,
        enableMultipleHttp2Connections: v.httpClientHttp2 || undefined,
        requestHeaderEncoding: v.httpClientReqEncoding || undefined,
        responseHeaderEncoding: v.httpClientResEncoding || undefined,
      } : undefined,
      httpRequest: (v.httpReqTimeout || v.httpReqVersion || v.httpReqVersionPolicy || v.httpReqBuffering) ? {
        activityTimeout: v.httpReqTimeout || undefined,
        version: v.httpReqVersion || undefined,
        versionPolicy: v.httpReqVersionPolicy || undefined,
        allowResponseBuffering: v.httpReqBuffering || undefined,
      } : undefined,
      metadata: v.metadata?.length
        ? Object.fromEntries(v.metadata.map((m: any) => [m.key, m.value]))
        : undefined,
    };

    this.dialogRef.close(result);
  }

  onCancel() { this.dialogRef.close(); }
}
```

- [ ] **Step 2: Create cluster-dialog.html**

```html
<!-- Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.html -->
<h2 mat-dialog-title>{{ data.cluster ? 'Edit' : 'Add' }} Cluster</h2>

<mat-dialog-content>
  <form [formGroup]="form">
    <mat-tab-group dynamicHeight>

      <!-- ── Tab 1: Destinations ───────────────────────────────────────── -->
      <mat-tab label="Destinations">
        <div class="tab-content">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Cluster ID</mat-label>
            <input matInput formControlName="clusterId" [readonly]="!!data.cluster" placeholder="my-backend">
            @if (form.get('clusterId')?.hasError('uniqueId')) {
              <mat-error>Cluster ID already exists</mat-error>
            }
            @if (form.get('clusterId')?.hasError('required')) {
              <mat-error>Required</mat-error>
            }
          </mat-form-field>

          <div formArrayName="destinations">
            @for (d of destinations.controls; track $index; let i = $index) {
              <div [formGroupName]="i" class="dest-block">
                <div class="dest-header">
                  <span class="section-label">Destination {{ i + 1 }}</span>
                  <button mat-icon-button color="warn" type="button" (click)="removeDestination(i)"
                          [disabled]="destinations.length === 1" matTooltip="Remove destination">
                    <mat-icon>delete</mat-icon>
                  </button>
                </div>
                <div class="row">
                  <mat-form-field appearance="outline">
                    <mat-label>Name (key)</mat-label>
                    <input matInput formControlName="key" placeholder="dest1">
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Address</mat-label>
                    <input matInput formControlName="address" placeholder="https://api.internal:8080">
                    @if (destinations.at(i).get('address')?.hasError('pattern')) {
                      <mat-error>Must start with http:// or https://</mat-error>
                    }
                  </mat-form-field>
                </div>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Health Check URL (optional)</mat-label>
                  <input matInput formControlName="health" placeholder="https://api.internal:8080/health">
                </mat-form-field>
              </div>
            }
          </div>

          <button mat-stroked-button type="button" (click)="addDestination()">
            <mat-icon>add</mat-icon> Add Destination
          </button>
        </div>
      </mat-tab>

      <!-- ── Tab 2: Load Balancing ─────────────────────────────────────── -->
      <mat-tab label="Load Balancing">
        <div class="tab-content">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Load Balancing Policy</mat-label>
            <mat-select formControlName="loadBalancingPolicy">
              <mat-option value="">Default (RoundRobin)</mat-option>
              @for (p of ['RoundRobin','LeastRequests','Random','PowerOfTwoChoices','FirstAlphabetical']; track p) {
                <mat-option [value]="p">{{ p }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-divider></mat-divider>
          <div class="section-label">Session Affinity</div>

          <mat-checkbox formControlName="saEnabled">Enable Session Affinity</mat-checkbox>

          @if (form.get('saEnabled')?.value) {
            <div class="row">
              <mat-form-field appearance="outline">
                <mat-label>Policy</mat-label>
                <mat-select formControlName="saPolicy">
                  <mat-option value="Cookie">Cookie</mat-option>
                  <mat-option value="CustomHeader">CustomHeader</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Failure Policy</mat-label>
                <mat-select formControlName="saFailurePolicy">
                  <mat-option value="Redistribute">Redistribute</mat-option>
                  <mat-option value="Return503Error">Return503Error</mat-option>
                </mat-select>
              </mat-form-field>
            </div>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Affinity Key Name</mat-label>
              <input matInput formControlName="saAffinityKeyName" placeholder=".AspNetCore.Yarp.SA">
            </mat-form-field>

            <div class="section-label">Cookie Settings</div>
            <div class="row">
              <mat-form-field appearance="outline">
                <mat-label>Path</mat-label>
                <input matInput formControlName="saCookiePath">
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Domain</mat-label>
                <input matInput formControlName="saCookieDomain">
              </mat-form-field>
            </div>
            <div class="row">
              <mat-form-field appearance="outline">
                <mat-label>SameSite</mat-label>
                <mat-select formControlName="saCookieSameSite">
                  @for (s of ['Lax','Strict','None']; track s) {
                    <mat-option [value]="s">{{ s }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Secure Policy</mat-label>
                <mat-select formControlName="saCookieSecurePolicy">
                  @for (s of ['SameAsRequest','Always','None']; track s) {
                    <mat-option [value]="s">{{ s }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            </div>
            <mat-checkbox formControlName="saCookieHttpOnly">HttpOnly</mat-checkbox>
          }
        </div>
      </mat-tab>

      <!-- ── Tab 3: Health Check ───────────────────────────────────────── -->
      <mat-tab label="Health Check">
        <div class="tab-content">
          <div class="section-label">Active Health Check</div>
          <mat-checkbox formControlName="hcActiveEnabled">Enable Active Health Check</mat-checkbox>

          @if (form.get('hcActiveEnabled')?.value) {
            <div class="row">
              <mat-form-field appearance="outline">
                <mat-label>Interval</mat-label>
                <input matInput formControlName="hcActiveInterval" placeholder="00:00:15">
                <mat-hint>hh:mm:ss</mat-hint>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Timeout</mat-label>
                <input matInput formControlName="hcActiveTimeout" placeholder="00:00:10">
                <mat-hint>hh:mm:ss</mat-hint>
              </mat-form-field>
            </div>
            <div class="row">
              <mat-form-field appearance="outline">
                <mat-label>Policy</mat-label>
                <mat-select formControlName="hcActivePolicy">
                  <mat-option value="ConsecutiveFailures">ConsecutiveFailures</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Health Path</mat-label>
                <input matInput formControlName="hcActivePath" placeholder="/health">
              </mat-form-field>
            </div>
          }

          <mat-divider></mat-divider>
          <div class="section-label">Passive Health Check</div>
          <mat-checkbox formControlName="hcPassiveEnabled">Enable Passive Health Check</mat-checkbox>

          @if (form.get('hcPassiveEnabled')?.value) {
            <div class="row">
              <mat-form-field appearance="outline">
                <mat-label>Policy</mat-label>
                <mat-select formControlName="hcPassivePolicy">
                  <mat-option value="TransportFailureRate">TransportFailureRate</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Reactivation Period</mat-label>
                <input matInput formControlName="hcPassiveReactivation" placeholder="00:02:00">
                <mat-hint>hh:mm:ss</mat-hint>
              </mat-form-field>
            </div>
          }
        </div>
      </mat-tab>

      <!-- ── Tab 4: HTTP ───────────────────────────────────────────────── -->
      <mat-tab label="HTTP">
        <div class="tab-content">
          <div class="section-label">HTTP Client</div>
          <mat-checkbox formControlName="httpClientDangerousCert">
            Accept Any Server Certificate (DangerousAcceptAnyServerCertificate)
          </mat-checkbox>
          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>Max Connections Per Server</mat-label>
              <input matInput type="number" formControlName="httpClientMaxConn" placeholder="e.g. 100">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Request Header Encoding</mat-label>
              <input matInput formControlName="httpClientReqEncoding" placeholder="e.g. utf-8">
            </mat-form-field>
          </div>
          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>Response Header Encoding</mat-label>
              <input matInput formControlName="httpClientResEncoding" placeholder="e.g. utf-8">
            </mat-form-field>
          </div>
          <mat-checkbox formControlName="httpClientHttp1">Enable Multiple HTTP/1.1 Connections</mat-checkbox>
          <mat-checkbox formControlName="httpClientHttp2">Enable Multiple HTTP/2 Connections</mat-checkbox>

          <mat-divider></mat-divider>
          <div class="section-label">HTTP Request Forwarding</div>
          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>Activity Timeout</mat-label>
              <input matInput formControlName="httpReqTimeout" placeholder="00:01:40">
              <mat-hint>hh:mm:ss — default 100 seconds</mat-hint>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>HTTP Version</mat-label>
              <mat-select formControlName="httpReqVersion">
                <mat-option value="">Default</mat-option>
                @for (v of ['1.0','1.1','2','3']; track v) {
                  <mat-option [value]="v">HTTP/{{ v }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Version Policy</mat-label>
            <mat-select formControlName="httpReqVersionPolicy">
              <mat-option value="">Default</mat-option>
              @for (p of ['RequestVersionOrLower','RequestVersionOrHigher','RequestVersionExact']; track p) {
                <mat-option [value]="p">{{ p }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-checkbox formControlName="httpReqBuffering">Allow Response Buffering</mat-checkbox>
        </div>
      </mat-tab>

      <!-- ── Tab 5: Metadata ───────────────────────────────────────────── -->
      <mat-tab label="Metadata">
        <div class="tab-content">
          <div formArrayName="metadata">
            @for (m of metadata.controls; track $index; let i = $index) {
              <div [formGroupName]="i" class="array-row">
                <mat-form-field appearance="outline">
                  <mat-label>Key</mat-label>
                  <input matInput formControlName="key">
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Value</mat-label>
                  <input matInput formControlName="value">
                </mat-form-field>
                <button mat-icon-button color="warn" type="button" (click)="removeMetadata(i)">
                  <mat-icon>remove_circle</mat-icon>
                </button>
              </div>
            }
          </div>
          <button mat-stroked-button type="button" (click)="addMetadata()">
            <mat-icon>add</mat-icon> Add Metadata
          </button>
        </div>
      </mat-tab>

    </mat-tab-group>
  </form>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button (click)="onCancel()">Cancel</button>
  <button mat-raised-button color="primary" [disabled]="form.invalid" (click)="onSave()">Save</button>
</mat-dialog-actions>
```

- [ ] **Step 3: Build to verify**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.UI && npm run build -- --configuration development 2>&1 | tail -5
```
Expected: no TypeScript errors

- [ ] **Step 4: Commit**

```bash
cd /c/Users/User/Desktop/Proxy
git add Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.ts
git add Proxy.UI/src/app/pages/dashboard/dialogs/cluster-dialog.html
git commit -m "feat: rewrite cluster dialog with 5-tab advanced config UI"
```

---

## Task 7: Update Dashboard to Use Individual Endpoints

**Files:**
- Modify: `Proxy.UI/src/app/pages/dashboard/dashboard.ts`

- [ ] **Step 1: Replace dashboard.ts**

```typescript
// Proxy.UI/src/app/pages/dashboard/dashboard.ts
import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBarModule, MatSnackBar } from '@angular/material/snack-bar';
import { Router, NavigationEnd } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { ProxyConfigService, RouteConfig, ClusterConfig } from '../../services/proxy-config';
import { RouteDialogComponent } from './dialogs/route-dialog';
import { ClusterDialogComponent } from './dialogs/cluster-dialog';
import { RawEditDialogComponent } from './dialogs/raw-edit-dialog';
import { ServiceWizardDialogComponent } from './dialogs/service-wizard-dialog';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule, MatTableModule, MatCardModule, MatButtonModule,
    MatIconModule, MatDialogModule, MatSnackBarModule,
  ],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.css'],
})
export class DashboardComponent implements OnInit, OnDestroy {
  private proxyService = inject(ProxyConfigService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private routerSub?: Subscription;

  routes: RouteConfig[] = [];
  clusters: ClusterConfig[] = [];
  routesColumns = ['RouteId', 'ClusterId', 'Match', 'Actions'];
  clustersColumns = ['ClusterId', 'Destinations', 'Actions'];

  ngOnInit() {
    this.loadConfig();
    this.routerSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd && e.urlAfterRedirects.includes('/dashboard'))
    ).subscribe(() => this.loadConfig());
  }

  ngOnDestroy() { this.routerSub?.unsubscribe(); }

  loadConfig() {
    this.proxyService.loadAll().subscribe({
      next: ({ routes, clusters }) => {
        this.routes = routes;
        this.clusters = clusters;
      },
      error: () => this.snackBar.open('Failed to load configuration.', 'Close', { duration: 3000 }),
    });
  }

  // ── Service Wizard (bulk, unchanged) ──────────────────────────────────────

  openServiceWizard() {
    const ref = this.dialog.open(ServiceWizardDialogComponent, {
      width: '600px',
      data: { existingClusters: this.clusters },
    });
    ref.afterClosed().subscribe((result: { routes: any[]; cluster: any } | undefined) => {
      if (!result) return;
      const payload = {
        routes: [...this.routes, ...result.routes],
        clusters: [...this.clusters, result.cluster],
      };
      this.proxyService.updateRawConfig(payload).subscribe({
        next: () => { this.snackBar.open('Service added!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: () => this.snackBar.open('Error adding service.', 'Close', { duration: 3000 }),
      });
    });
  }

  // ── Route CRUD ────────────────────────────────────────────────────────────

  addRoute() {
    const ref = this.dialog.open(RouteDialogComponent, {
      width: '750px',
      data: { clusters: this.clusters, existingRoutes: this.routes },
    });
    ref.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.proxyService.addRoute(result).subscribe({
        next: () => { this.snackBar.open('Route added!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.Error ?? 'Error adding route.', 'Close', { duration: 4000 }),
      });
    });
  }

  editRoute(route: RouteConfig) {
    const ref = this.dialog.open(RouteDialogComponent, {
      width: '750px',
      data: { route, clusters: this.clusters, existingRoutes: this.routes },
    });
    ref.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateRoute(route.routeId, result).subscribe({
        next: () => { this.snackBar.open('Route updated!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: () => this.snackBar.open('Error updating route.', 'Close', { duration: 3000 }),
      });
    });
  }

  deleteRoute(route: RouteConfig) {
    if (!confirm(`Delete route "${route.routeId}"?`)) return;
    this.proxyService.deleteRoute(route.routeId).subscribe({
      next: () => { this.snackBar.open('Route deleted.', 'Close', { duration: 3000 }); this.loadConfig(); },
      error: () => this.snackBar.open('Error deleting route.', 'Close', { duration: 3000 }),
    });
  }

  rawEditRoute(route: RouteConfig) {
    const ref = this.dialog.open(RawEditDialogComponent, {
      width: '600px',
      data: { item: route, label: route.routeId },
    });
    ref.afterClosed().subscribe((result: RouteConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateRoute(route.routeId, result).subscribe({
        next: () => { this.snackBar.open('Route updated!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: () => this.snackBar.open('Error updating route.', 'Close', { duration: 3000 }),
      });
    });
  }

  // ── Cluster CRUD ──────────────────────────────────────────────────────────

  addCluster() {
    const ref = this.dialog.open(ClusterDialogComponent, {
      width: '750px',
      data: { existingClusters: this.clusters },
    });
    ref.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.proxyService.addCluster(result).subscribe({
        next: () => { this.snackBar.open('Cluster added!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: (err) => this.snackBar.open(err?.error?.Error ?? 'Error adding cluster.', 'Close', { duration: 4000 }),
      });
    });
  }

  editCluster(cluster: ClusterConfig) {
    const ref = this.dialog.open(ClusterDialogComponent, {
      width: '750px',
      data: { cluster, existingClusters: this.clusters },
    });
    ref.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateCluster(cluster.clusterId, result).subscribe({
        next: () => { this.snackBar.open('Cluster updated!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: () => this.snackBar.open('Error updating cluster.', 'Close', { duration: 3000 }),
      });
    });
  }

  deleteCluster(cluster: ClusterConfig) {
    if (!confirm(`Delete cluster "${cluster.clusterId}"?`)) return;
    this.proxyService.deleteCluster(cluster.clusterId).subscribe({
      next: () => { this.snackBar.open('Cluster deleted.', 'Close', { duration: 3000 }); this.loadConfig(); },
      error: () => this.snackBar.open('Error deleting cluster.', 'Close', { duration: 3000 }),
    });
  }

  rawEditCluster(cluster: ClusterConfig) {
    const ref = this.dialog.open(RawEditDialogComponent, {
      width: '600px',
      data: { item: cluster, label: cluster.clusterId },
    });
    ref.afterClosed().subscribe((result: ClusterConfig | undefined) => {
      if (!result) return;
      this.proxyService.updateCluster(cluster.clusterId, result).subscribe({
        next: () => { this.snackBar.open('Cluster updated!', 'Close', { duration: 3000 }); this.loadConfig(); },
        error: () => this.snackBar.open('Error updating cluster.', 'Close', { duration: 3000 }),
      });
    });
  }
}
```

- [ ] **Step 2: Full build to verify everything compiles**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.UI && npm run build -- --configuration development 2>&1 | tail -10
```
Expected: `Build complete.` with no errors

- [ ] **Step 3: Full backend build**

```bash
cd /c/Users/User/Desktop/Proxy/Proxy.Host && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd /c/Users/User/Desktop/Proxy
git add Proxy.UI/src/app/pages/dashboard/dashboard.ts
git commit -m "feat: update dashboard to use individual CRUD endpoints"
```

---

## Manual Smoke Test (after all tasks complete)

- [ ] Start the backend: `cd /c/Users/User/Desktop/Proxy/Proxy.Host && dotnet run`
- [ ] Start Angular dev server: `cd /c/Users/User/Desktop/Proxy/Proxy.UI && ng serve`
- [ ] Login at `http://localhost:4200`
- [ ] Add a cluster → verify 5 tabs appear, Destinations tab works, save sends `POST /api/proxyconfig/clusters`
- [ ] Add a route → verify 5 tabs appear, Transforms tab presets work, save sends `POST /api/proxyconfig/routes`
- [ ] Edit the route → verify existing values pre-populate in all tabs
- [ ] Delete the route → verify `DELETE /api/proxyconfig/routes/{id}` called
- [ ] Open Raw JSON editor on a cluster → verify update uses `PUT /api/proxyconfig/clusters/{id}`
- [ ] Use Service Wizard → verify still works (uses bulk `POST /api/proxyconfig/raw`)
