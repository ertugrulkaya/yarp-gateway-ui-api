// Proxy.Host/Models/ConfigDtos.cs
using LiteDB;

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
