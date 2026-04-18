# YARP Advanced Configuration Design

**Date:** 2026-04-17
**Status:** Approved
**Migration:** Clean slate (DB reset on deploy)

---

## Overview

Expand the YARP Proxy Manager to expose the full YARP configuration surface — routes and clusters — through a tab-based UI and a RESTful API. The design maps 1:1 with YARP's native model so any future YARP features require only DTO field additions.

---

## 1. Data Model

### RouteDto

```csharp
public class RouteDto
{
    public string RouteId { get; set; }
    public string? ClusterId { get; set; }
    public int? Order { get; set; }
    public RouteMatchDto Match { get; set; }
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
    public string Name { get; set; }
    public List<string>? Values { get; set; }
    public string? Mode { get; set; }          // ExactHeader, HeaderPrefix, Contains, NotContains, NotExists
    public bool IsCaseSensitive { get; set; }
}

public class RouteQueryParameterDto
{
    public string Name { get; set; }
    public List<string>? Values { get; set; }
    public string? Mode { get; set; }          // Exact, Contains, NotContains, Exists, NotExists
    public bool IsCaseSensitive { get; set; }
}
```

### ClusterDto

```csharp
public class ClusterDto
{
    public string ClusterId { get; set; }
    public string? LoadBalancingPolicy { get; set; }  // RoundRobin, LeastRequests, Random, PowerOfTwoChoices, FirstAlphabetical
    public Dictionary<string, DestinationDto> Destinations { get; set; }
    public SessionAffinityDto? SessionAffinity { get; set; }
    public HealthCheckDto? HealthCheck { get; set; }
    public HttpClientDto? HttpClient { get; set; }
    public HttpRequestDto? HttpRequest { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class DestinationDto
{
    public string Address { get; set; }
    public string? Health { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class SessionAffinityDto
{
    public bool? Enabled { get; set; }
    public string? Policy { get; set; }         // Cookie, CustomHeader
    public string? FailurePolicy { get; set; }  // Redistribute, Return503Error
    public string? AffinityKeyName { get; set; }
    public SessionAffinityCookieDto? Cookie { get; set; }
}

public class SessionAffinityCookieDto
{
    public string? Path { get; set; }
    public string? Domain { get; set; }
    public TimeSpan? Expiration { get; set; }
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
    public TimeSpan? Interval { get; set; }
    public TimeSpan? Timeout { get; set; }
    public string? Policy { get; set; }
    public string? Path { get; set; }
}

public class PassiveHealthCheckDto
{
    public bool? Enabled { get; set; }
    public string? Policy { get; set; }
    public TimeSpan? ReactivationPeriod { get; set; }
}

public class HttpClientDto
{
    public bool? DangerousAcceptAnyServerCertificate { get; set; }
    public int? MaxConnectionsPerServer { get; set; }
    public bool? EnableMultipleHttp1Connections { get; set; }
    public bool? EnableMultipleHttp2Connections { get; set; }
    public string? RequestHeaderEncoding { get; set; }
    public string? ResponseHeaderEncoding { get; set; }
}

public class HttpRequestDto
{
    public TimeSpan? ActivityTimeout { get; set; }
    public string? Version { get; set; }        // "1.0", "1.1", "2", "3"
    public string? VersionPolicy { get; set; }  // RequestVersionOrLower, RequestVersionOrHigher, RequestVersionExact
    public bool? AllowResponseBuffering { get; set; }
}
```

---

## 2. Storage

LiteDB `proxy.db` — same collections, expanded schema:

| Collection | Key | Value |
|-----------|-----|-------|
| `routes` | `RouteId` (BsonId) | `RouteConfigWrapper { RouteId, Config: RouteDto }` |
| `clusters` | `ClusterId` (BsonId) | `ClusterConfigWrapper { ClusterId, Config: ClusterDto }` |
| `users` | int Id | `User` |

No schema migration needed (clean slate). LiteDB serializes new nullable fields automatically.

---

## 3. API

### Existing (kept)
```
GET  /api/proxyconfig/raw      → ProxyConfigPayload { Routes[], Clusters[] }
POST /api/proxyconfig/raw      → bulk upsert + YARP reload
```

### New — Routes
```
GET    /api/proxyconfig/routes
POST   /api/proxyconfig/routes
PUT    /api/proxyconfig/routes/{routeId}
DELETE /api/proxyconfig/routes/{routeId}
```

### New — Clusters
```
GET    /api/proxyconfig/clusters
POST   /api/proxyconfig/clusters
PUT    /api/proxyconfig/clusters/{clusterId}
DELETE /api/proxyconfig/clusters/{clusterId}
```

Every write operation calls `_configProvider.UpdateConfig()` for zero-downtime YARP reload.

---

## 4. Frontend UI

### Dashboard
Two Material tables (Routes + Clusters) — unchanged structure, updated action buttons to use new individual endpoints.

### Route Editor Dialog — 5 Tabs

| Tab | Fields |
|-----|--------|
| **Match** | Path, Methods (multi-select chip), Hosts (chip list), Headers (expandable key/value/mode list), QueryParameters (expandable list) |
| **Transforms** | Preset picker (PathRemovePrefix, PathSet, PathPattern, RequestHeader, ResponseHeader, RequestHeadersCopy, RequestHeaderOriginalHost) + raw key/value fallback |
| **Policies** | AuthorizationPolicy, CorsPolicy, RateLimiterPolicy, TimeoutPolicy, OutputCachePolicy, MaxRequestBodySize |
| **Advanced** | Order (number input) |
| **Metadata** | Dynamic key/value pairs (add/remove rows) |

### Cluster Editor Dialog — 5 Tabs

| Tab | Fields |
|-----|--------|
| **Destinations** | List of destinations: Address, Health URL, Metadata per destination |
| **Load Balancing** | LoadBalancingPolicy dropdown, SessionAffinity toggle + Policy/FailurePolicy/AffinityKeyName + Cookie sub-section |
| **Health Check** | Active toggle + Interval/Timeout/Path/Policy; Passive toggle + Policy/ReactivationPeriod |
| **HTTP** | HttpClient section (MaxConnections, DangerousAcceptAnyServerCertificate, encodings); HttpRequest section (ActivityTimeout, Version, VersionPolicy, AllowResponseBuffering) |
| **Metadata** | Dynamic key/value pairs (add/remove rows) |

### Extensibility Rule
Each tab is a standalone Angular component. New YARP fields → add to the relevant tab component only. Dialog structure does not change.

---

## 5. LiteDbProxyConfigProvider Mapping

`MapRoute()` and `MapCluster()` methods expanded to map all new DTO fields to YARP's `RouteConfig` / `ClusterConfig`. Null fields are omitted (YARP uses its defaults).

---

## Out of Scope

- Multi-user / RBAC
- Config versioning / history
- Import/export
- HTTPS enforcement (intranet deployment)
