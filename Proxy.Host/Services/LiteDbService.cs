using LiteDB;
using Proxy.Host.Models;
using System.Security.Cryptography;
using System.Text;

namespace Proxy.Host.Services;

public class LiteDbService : IHostedService
{
    private readonly LiteDatabase _db;

    public LiteDbService(IConfiguration configuration)
    {
        var dbPath = configuration["LiteDb:Path"] ?? "proxy.db";
        _db = new LiteDatabase($"Filename={dbPath};Connection=shared");
    }

    public LiteDatabase Database => _db;

    // IHostedService — seed runs after DI is fully built, not during constructor
    public Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureDefaultAdminExists();
        EnsureDefaultConfigExists();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ── Auth ──────────────────────────────────────────────────────────────────

    private void EnsureDefaultAdminExists()
    {
        var users = _db.GetCollection<User>("users");
        if (users.Count() == 0)
        {
            CreatePasswordHash("Rexadmin1234.", out byte[] hash, out byte[] salt);
            users.Insert(new User
            {
                Username = "Admin",
                PasswordHash = hash,
                PasswordSalt = salt,
                MustChangePassword = true
            });
        }
    }

    private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using var hmac = new HMACSHA512();
        passwordSalt = hmac.Key;
        passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    // ── Seed Config ───────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts example clusters and routes only when the collections are empty.
    /// Covers every advanced YARP field so you can see them in the UI.
    /// </summary>
    private void EnsureDefaultConfigExists()
    {
        var clusters = _db.GetCollection<ClusterConfigWrapper>("clusters");
        var routes   = _db.GetCollection<RouteConfigWrapper>("routes");

        // Indexes (idempotent — safe to call on every startup)
        routes.EnsureIndex(x => x.Config.ClusterId);

        var history = _db.GetCollection<ConfigHistory>("config_history");
        history.EnsureIndex(x => x.ChangedAt);

        if (clusters.Count() > 0 || routes.Count() > 0)
            return; // already seeded

        // ── Cluster 1: REST API — round-robin, active health check, HTTP/2 ──
        clusters.Upsert(new ClusterConfigWrapper
        {
            ClusterId = "cluster-api",
            Config = new ClusterDto
            {
                ClusterId = "cluster-api",
                LoadBalancingPolicy = "RoundRobin",
                Destinations = new Dictionary<string, DestinationDto>
                {
                    ["api-node-1"] = new() { Address = "http://api-node1.internal:8080" },
                    ["api-node-2"] = new() { Address = "http://api-node2.internal:8080" },
                },
                // HealthCheck: gerçek backend olmadığı için seed'de bırakılmadı.
                // UI'dan Edit > Health Check tabında aktif edebilirsin.
                HttpRequest = new HttpRequestDto
                {
                    ActivityTimeout = "00:01:00",
                    Version         = "2",
                    VersionPolicy   = "RequestVersionOrLower",
                },
                Metadata = new Dictionary<string, string>
                {
                    ["team"]        = "backend",
                    ["environment"] = "production",
                },
            },
        });

        // ── Cluster 2: File server — session affinity, LeastRequests, max connections ──
        clusters.Upsert(new ClusterConfigWrapper
        {
            ClusterId = "cluster-fileserver",
            Config = new ClusterDto
            {
                ClusterId = "cluster-fileserver",
                LoadBalancingPolicy = "LeastRequests",
                Destinations = new Dictionary<string, DestinationDto>
                {
                    ["fs-primary"]   = new() { Address = "http://fileserver1.internal:9000" },
                    ["fs-secondary"] = new() { Address = "http://fileserver2.internal:9000" },
                },
                SessionAffinity = new SessionAffinityDto
                {
                    Enabled          = true,
                    Policy           = "Cookie",
                    FailurePolicy    = "Redistribute",
                    AffinityKeyName  = ".Proxy.FileServer.Affinity",
                    Cookie = new SessionAffinityCookieDto
                    {
                        Path          = "/",
                        HttpOnly      = true,
                        IsEssential   = false,
                        SameSite      = "Lax",
                        SecurePolicy  = "SameAsRequest",
                    },
                },
                HttpClient = new HttpClientDto
                {
                    MaxConnectionsPerServer        = 50,
                    EnableMultipleHttp2Connections = true,
                },
            },
        });

        // ── Cluster 3: Legacy internal service — self-signed cert, HTTP/1.1 ──
        clusters.Upsert(new ClusterConfigWrapper
        {
            ClusterId = "cluster-legacy",
            Config = new ClusterDto
            {
                ClusterId = "cluster-legacy",
                LoadBalancingPolicy = "FirstAlphabetical",
                Destinations = new Dictionary<string, DestinationDto>
                {
                    ["legacy-main"] = new() { Address = "https://legacy.internal:4430" },
                },
                HttpClient = new HttpClientDto
                {
                    DangerousAcceptAnyServerCertificate = true,
                    RequestHeaderEncoding               = "latin1",
                    ResponseHeaderEncoding              = "latin1",
                },
                HttpRequest = new HttpRequestDto
                {
                    ActivityTimeout       = "00:03:00",
                    AllowResponseBuffering = true,
                    Version               = "1.1",
                    VersionPolicy         = "RequestVersionExact",
                },
                Metadata = new Dictionary<string, string>
                {
                    ["note"] = "Legacy service — migrate by Q3",
                },
            },
        });

        // ── Route 1: Public API — wildcard path, all HTTP methods ──
        routes.Upsert(new RouteConfigWrapper
        {
            RouteId = "route-api-public",
            Config  = new RouteDto
            {
                RouteId   = "route-api-public",
                ClusterId = "cluster-api",
                Order     = 10,
                Match = new RouteMatchDto
                {
                    Path  = "/api/{**catch-all}",
                    Hosts = new List<string> { "proxy.internal", "api.internal" },
                },
                Transforms = new List<Dictionary<string, string>>
                {
                    new() { ["RequestHeaderOriginalHost"] = "true" },
                    new() { ["RequestHeader"] = "X-Forwarded-Prefix", ["Set"] = "/api" },
                },
                Metadata = new Dictionary<string, string>
                {
                    ["visibility"] = "public",
                },
            },
        });

        // ── Route 2: Admin API — POST/PUT/DELETE only, auth policy, header filter ──
        routes.Upsert(new RouteConfigWrapper
        {
            RouteId = "route-api-admin",
            Config  = new RouteDto
            {
                RouteId   = "route-api-admin",
                ClusterId = "cluster-api",
                Order     = 5,  // higher priority than public route
                Match = new RouteMatchDto
                {
                    Path    = "/api/admin/{**catch-all}",
                    Methods = new List<string> { "POST", "PUT", "DELETE" },
                    Headers = new List<RouteHeaderDto>
                    {
                        new()
                        {
                            Name             = "X-Internal-Request",
                            Values           = new List<string> { "true" },
                            Mode             = "ExactHeader",
                            IsCaseSensitive  = false,
                        },
                    },
                },
                AuthorizationPolicy = "Anonymous",
                Transforms = new List<Dictionary<string, string>>
                {
                    new() { ["PathRemovePrefix"] = "/api/admin" },
                    new() { ["RequestHeader"] = "X-Admin-Route", ["Set"] = "1" },
                },
                MaxRequestBodySize = 10_485_760,  // 10 MB
            },
        });

        // ── Route 3: File downloads — GET/HEAD, path prefix strip, session affinity ──
        routes.Upsert(new RouteConfigWrapper
        {
            RouteId = "route-files",
            Config  = new RouteDto
            {
                RouteId   = "route-files",
                ClusterId = "cluster-fileserver",
                Order     = 10,
                Match = new RouteMatchDto
                {
                    Path    = "/files/{**catch-all}",
                    Methods = new List<string> { "GET", "HEAD" },
                },
                CorsPolicy = null,
                Transforms = new List<Dictionary<string, string>>
                {
                    new() { ["PathRemovePrefix"] = "/files" },
                    new() { ["ResponseHeader"] = "Cache-Control", ["Set"] = "public, max-age=3600", ["When"] = "Always" },
                },
            },
        });

        // ── Route 4: Legacy — query param filter, rate limiter, output cache ──
        routes.Upsert(new RouteConfigWrapper
        {
            RouteId = "route-legacy",
            Config  = new RouteDto
            {
                RouteId   = "route-legacy",
                ClusterId = "cluster-legacy",
                Order     = 20,
                Match = new RouteMatchDto
                {
                    Path = "/legacy/{**catch-all}",
                    QueryParameters = new List<RouteQueryParameterDto>
                    {
                        new()
                        {
                            Name            = "version",
                            Values          = new List<string> { "v1", "v2" },
                            Mode            = "Contains",
                            IsCaseSensitive = false,
                        },
                    },
                },
                RateLimiterPolicy  = null,
                OutputCachePolicy  = null,
                TimeoutPolicy      = null,
                Transforms = new List<Dictionary<string, string>>
                {
                    new() { ["PathPattern"] = "/service/{**catch-all}" },
                    new() { ["RequestHeadersCopy"] = "true" },
                },
                Metadata = new Dictionary<string, string>
                {
                    ["deprecated"] = "true",
                    ["owner"]      = "platform-team",
                },
            },
        });
    }
}
