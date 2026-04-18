using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Host.Models;
using Proxy.Host.Services;
using System.Diagnostics;
using Yarp.ReverseProxy.Model;

namespace Proxy.Host.Middleware;

public static class YarpLoggingExtensions
{
    public static void UseYarpLogging(this IReverseProxyApplicationBuilder proxyPipeline)
    {
        proxyPipeline.Use(async (context, next) =>
        {
            var sw = Stopwatch.StartNew();
            
            await next();
            
            sw.Stop();

            var proxyFeature = context.GetReverseProxyFeature();
            if (proxyFeature != null)
            {
                var logService = context.RequestServices.GetRequiredService<LogService>();
                var destination = proxyFeature.ProxiedDestination;
                var cluster = proxyFeature.Route.Cluster;

                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    Method = context.Request.Method,
                    Path = context.Request.Path,
                    ClusterId = cluster?.ClusterId,
                    DestinationAddress = destination?.Model.Config.Address,
                    StatusCode = context.Response.StatusCode,
                    DurationMs = sw.Elapsed.TotalMilliseconds
                };

                logService.Enqueue(logEntry);
            }
        });
    }
}

