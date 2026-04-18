// Proxy.Host/Providers/LiteDbProxyConfigProvider.cs
using LiteDB;
using Microsoft.Extensions.Primitives;
using Proxy.Host.Models;
using Proxy.Host.Services;
using System.Net.Http;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

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

public class LiteDbProxyConfig : IProxyConfig
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
}
