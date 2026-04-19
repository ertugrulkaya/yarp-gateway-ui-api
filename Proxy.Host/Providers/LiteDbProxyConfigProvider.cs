// Proxy.Host/Providers/LiteDbProxyConfigProvider.cs
using LiteDB;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;
using Proxy.Host.Models;
using Proxy.Host.Services;
using System.Collections.Concurrent;
using System.Net.Http;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Proxy.Host.Providers;

public class LiteDbProxyConfigProvider : IProxyConfigProvider, IDisposable
{
    private readonly LiteDbService _liteDbService;
    private readonly ILogger<LiteDbProxyConfigProvider> _logger;
    private readonly ReaderWriterLockSlim _lock = new();
    private LiteDbProxyConfig _config;
    private bool _disposed;

    // ── Caches ────────────────────────────────────────────────────────────────
    // Path parse cache: avoids re-running RoutePatternFactory.Parse on every reload
    private readonly ConcurrentDictionary<string, bool> _pathValidCache = new();
    // Entity caches: avoids re-mapping unchanged routes/clusters
    private readonly ConcurrentDictionary<string, RouteConfig>  _routeCache   = new();
    private readonly ConcurrentDictionary<string, ClusterConfig> _clusterCache = new();

    public LiteDbProxyConfigProvider(LiteDbService liteDbService, ILogger<LiteDbProxyConfigProvider> logger)
    {
        _liteDbService = liteDbService;
        _logger = logger;
        _config = LoadFromDb();
    }

    public IProxyConfig GetConfig()
    {
        _lock.EnterReadLock();
        try { return _config; }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>Full reload — used when multiple entities may have changed (bulk, restore).</summary>
    public virtual void UpdateConfig()
    {
        var newConfig = LoadFromDb();
        SwapConfig(newConfig);
    }

    /// <summary>
    /// Targeted reload — only reloads the single changed route or cluster from DB,
    /// leaving all other cached entries intact. Call after single-entity CRUD.
    /// </summary>
    public virtual void UpdateConfig(string entityType, string entityId, bool deleted = false)
    {
        if (deleted)
        {
            if (entityType == "route")
            {
                _routeCache.TryRemove(entityId, out _);
                // Also remove path cache entry for this route
                var wrapper = _liteDbService.Database
                    .GetCollection<RouteConfigWrapper>("routes").FindById(entityId);
                if (wrapper?.Config.Match?.Path is { } p) _pathValidCache.TryRemove(p, out _);
            }
            else
            {
                _clusterCache.TryRemove(entityId, out _);
            }
        }
        else
        {
            // Re-map only the changed entity
            if (entityType == "route")
            {
                var wrapper = _liteDbService.Database
                    .GetCollection<RouteConfigWrapper>("routes").FindById(entityId);
                if (wrapper != null)
                {
                    var path = wrapper.Config.Match?.Path;
                    if (!string.IsNullOrEmpty(path)) _pathValidCache.TryRemove(path, out _);
                    try
                    {
                        var mapped = MapRoute(wrapper.Config);
                        _routeCache[entityId] = mapped;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Route '{RouteId}' skipped — {Message}", entityId, ex.Message);
                        _routeCache.TryRemove(entityId, out _);
                    }
                }
            }
            else
            {
                var wrapper = _liteDbService.Database
                    .GetCollection<ClusterConfigWrapper>("clusters").FindById(entityId);
                if (wrapper != null)
                {
                    try
                    {
                        _clusterCache[entityId] = MapCluster(wrapper.Config);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Cluster '{ClusterId}' skipped — {Message}", entityId, ex.Message);
                        _clusterCache.TryRemove(entityId, out _);
                    }
                }
            }
        }

        // Rebuild config from caches
        var routes   = _routeCache.Values.ToList();
        var clusters = _clusterCache.Values.ToList();
        SwapConfig(new LiteDbProxyConfig(routes, clusters));
    }

    private void SwapConfig(LiteDbProxyConfig newConfig)
    {
        _lock.EnterWriteLock();
        LiteDbProxyConfig oldConfig;
        try
        {
            oldConfig = _config;
            _config = newConfig;
        }
        finally { _lock.ExitWriteLock(); }

        oldConfig.SignalChange();
        oldConfig.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.EnterReadLock();
        LiteDbProxyConfig current;
        try { current = _config; }
        finally { _lock.ExitReadLock(); }
        current.Dispose();
        _lock.Dispose();
    }

    private LiteDbProxyConfig LoadFromDb()
    {
        // Track which IDs are still in the DB so we can evict stale cache entries
        var dbRouteIds    = new HashSet<string>();
        var dbClusterIds  = new HashSet<string>();

        foreach (var wrapper in _liteDbService.Database
            .GetCollection<RouteConfigWrapper>("routes").FindAll())
        {
            dbRouteIds.Add(wrapper.RouteId);

            // Skip if we already have a valid cached mapping for this route
            if (_routeCache.ContainsKey(wrapper.RouteId))
                continue;

            try
            {
                var path = wrapper.Config.Match?.Path;
                if (!string.IsNullOrEmpty(path))
                {
                    // Check path validity cache first
                    if (!_pathValidCache.TryGetValue(path, out var isValid))
                    {
                        try { RoutePatternFactory.Parse(path); isValid = true; }
                        catch { isValid = false; }
                        _pathValidCache[path] = isValid;
                    }

                    if (!isValid)
                    {
                        _logger.LogWarning(
                            "Route '{RouteId}' skipped — invalid path pattern: {Path}. " +
                            "Delete or fix it via the dashboard.",
                            wrapper.RouteId, path);
                        continue;
                    }
                }

                _routeCache[wrapper.RouteId] = MapRoute(wrapper.Config);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Route '{RouteId}' skipped — {Message}",
                    wrapper.RouteId, ex.Message);
            }
        }

        foreach (var wrapper in _liteDbService.Database
            .GetCollection<ClusterConfigWrapper>("clusters").FindAll())
        {
            dbClusterIds.Add(wrapper.ClusterId);
            if (!_clusterCache.ContainsKey(wrapper.ClusterId))
            {
                try
                {
                    _clusterCache[wrapper.ClusterId] = MapCluster(wrapper.Config);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Cluster '{ClusterId}' skipped — {Message}",
                        wrapper.ClusterId, ex.Message);
                }
            }
        }

        // Evict deleted entries from caches
        foreach (var key in _routeCache.Keys.Except(dbRouteIds).ToList())
            _routeCache.TryRemove(key, out _);
        foreach (var key in _clusterCache.Keys.Except(dbClusterIds).ToList())
            _clusterCache.TryRemove(key, out _);

        return new LiteDbProxyConfig(
            _routeCache.Values.ToList(),
            _clusterCache.Values.ToList());
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
        var destinations = (dto.Destinations ?? new Dictionary<string, DestinationDto>())
            .ToDictionary(
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
                EnableMultipleHttp2Connections = hcl.EnableMultipleHttp2Connections,
                RequestHeaderEncoding = string.IsNullOrEmpty(hcl.RequestHeaderEncoding)
                    ? null : hcl.RequestHeaderEncoding,
                ResponseHeaderEncoding = string.IsNullOrEmpty(hcl.ResponseHeaderEncoding)
                    ? null : hcl.ResponseHeaderEncoding,
            };
        }

        ForwarderRequestConfig? httpRequest = null;
        if (dto.HttpRequest is { } hr)
        {
            httpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = ParseTimeSpan(hr.ActivityTimeout),
                Version = Version.TryParse(hr.Version, out var ver) ? ver : null,
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

public class LiteDbProxyConfig : IProxyConfig, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public LiteDbProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    internal void SignalChange() => _cts.Cancel();

    public void Dispose() => _cts.Dispose();
}
