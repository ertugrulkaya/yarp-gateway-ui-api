// Proxy.Host/Models/ConfigDtos.cs
using System.ComponentModel.DataAnnotations;
using LiteDB;

namespace Proxy.Host.Models;

// ── Route ──────────────────────────────────────────────────────────────────

public class RouteDto : IValidatableObject
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(RouteId))
            yield return new ValidationResult("RouteId is required.", [nameof(RouteId)]);

        if (!string.IsNullOrWhiteSpace(ClusterId) && ClusterId.StartsWith(" "))
            yield return new ValidationResult("ClusterId must not start with a space.", [nameof(ClusterId)]);

        if (Order < 0)
            yield return new ValidationResult("Order must be non-negative.", [nameof(Order)]);

        if (MaxRequestBodySize < 0)
            yield return new ValidationResult("MaxRequestBodySize must be non-negative.", [nameof(MaxRequestBodySize)]);

        if (Transforms != null)
        {
            for (int i = 0; i < Transforms.Count; i++)
            {
                if (Transforms[i] == null || Transforms[i].Count == 0)
                    yield return new ValidationResult($"Transforms[{i}] is empty or null.");
            }
        }
    }
}

public class RouteMatchDto : IValidatableObject
{
    public string? Path { get; set; }
    public List<string>? Methods { get; set; }
    public List<string>? Hosts { get; set; }
    public List<RouteHeaderDto>? Headers { get; set; }
    public List<RouteQueryParameterDto>? QueryParameters { get; set; }

    private static readonly string[] ValidHttpMethods = { "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD", "TRACE", "CONNECT" };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Path) && !Path.StartsWith("/") && Path != "{**path}")
            yield return new ValidationResult("Path must start with '/' or be '{**path}'.", [nameof(Path)]);

        if (Methods != null)
        {
            var invalid = Methods.Where(m => !ValidHttpMethods.Contains(m.ToUpperInvariant())).ToList();
            if (invalid.Count > 0)
                yield return new ValidationResult($"Invalid HTTP method(s): {string.Join(", ", invalid)}.", [nameof(Methods)]);
        }

        if (Headers != null)
        {
            for (int i = 0; i < Headers.Count; i++)
            {
                if (Headers[i] == null || string.IsNullOrWhiteSpace(Headers[i].Name))
                    yield return new ValidationResult($"Headers[{i}] has empty name.");
            }
        }

        if (QueryParameters != null)
        {
            for (int i = 0; i < QueryParameters.Count; i++)
            {
                if (QueryParameters[i] == null || string.IsNullOrWhiteSpace(QueryParameters[i].Name))
                    yield return new ValidationResult($"QueryParameters[{i}] has empty name.");
            }
        }
    }
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

public class ClusterDto : IValidatableObject
{
    public string ClusterId { get; set; } = string.Empty;
    public string? LoadBalancingPolicy { get; set; }
    public Dictionary<string, DestinationDto> Destinations { get; set; } = new();
    public SessionAffinityDto? SessionAffinity { get; set; }
    public HealthCheckDto? HealthCheck { get; set; }
    public HttpClientDto? HttpClient { get; set; }
    public HttpRequestDto? HttpRequest { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ClusterId))
            yield return new ValidationResult("ClusterId is required.", [nameof(ClusterId)]);

        if (!string.IsNullOrWhiteSpace(ClusterId) && ClusterId.StartsWith(" "))
            yield return new ValidationResult("ClusterId must not start with a space.", [nameof(ClusterId)]);

        var validLbPolicies = new[] { "First", "RoundRobin", "LeastRequests", "LeastMemory", "Random", "PowerOfTwoChoices" };
        if (!string.IsNullOrWhiteSpace(LoadBalancingPolicy) && !validLbPolicies.Contains(LoadBalancingPolicy))
            yield return new ValidationResult($"Invalid LoadBalancingPolicy. Must be one of: {string.Join(", ", validLbPolicies)}.", [nameof(LoadBalancingPolicy)]);

        if (Destinations == null || Destinations.Count == 0)
            yield return new ValidationResult("At least one destination is required.", [nameof(Destinations)]);

        if (Destinations != null)
        {
            foreach (var dest in Destinations)
            {
                if (string.IsNullOrWhiteSpace(dest.Value.Address))
                    yield return new ValidationResult($"Destination '{dest.Key}' has empty address.");
            }
        }
    }
}

public class DestinationDto : IValidatableObject
{
    public string Address { get; set; } = string.Empty;
    public string? Health { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Address))
            yield return new ValidationResult("Address is required.", [nameof(Address)]);

        if (!string.IsNullOrWhiteSpace(Address) && !Uri.TryCreate(Address, UriKind.Absolute, out _))
            yield return new ValidationResult("Address must be a valid URI.", [nameof(Address)]);
    }
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

public class ProxyConfigPayload : IValidatableObject
{
    public List<RouteDto> Routes { get; set; } = new();
    public List<ClusterDto> Clusters { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((Routes == null || Routes.Count == 0) && (Clusters == null || Clusters.Count == 0))
            yield return new ValidationResult("At least one route or cluster is required.", [nameof(Routes)]);

        var clusterIds = Clusters?.Select(c => c.ClusterId).Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet() ?? new HashSet<string>();

        if (Routes != null)
        {
            var duplicateRouteIds = Routes
                .Where(r => !string.IsNullOrWhiteSpace(r.RouteId))
                .GroupBy(r => r.RouteId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateRouteIds.Count > 0)
                yield return new ValidationResult($"Duplicate RouteId(s): {string.Join(", ", duplicateRouteIds)}.", [nameof(Routes)]);

            foreach (var route in Routes.Where(r => !string.IsNullOrWhiteSpace(r.ClusterId)))
            {
                if (!clusterIds.Contains(route.ClusterId!))
                    yield return new ValidationResult($"Route '{route.RouteId}' references unknown cluster '{route.ClusterId}'.", [nameof(Routes)]);
            }
        }

        if (Clusters != null)
        {
            var duplicateClusterIds = Clusters
                .Where(c => !string.IsNullOrWhiteSpace(c.ClusterId))
                .GroupBy(c => c.ClusterId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateClusterIds.Count > 0)
                yield return new ValidationResult($"Duplicate ClusterId(s): {string.Join(", ", duplicateClusterIds)}.", [nameof(Clusters)]);
        }
    }
}
