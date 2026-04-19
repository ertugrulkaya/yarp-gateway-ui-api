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

            try
            {
                var proxyFeature = context.GetReverseProxyFeature();
                if (proxyFeature == null) return;

                var logService = context.RequestServices.GetRequiredService<LogService>();
                var destination = proxyFeature.ProxiedDestination;
                var cluster    = proxyFeature.Route?.Cluster;

                var logEntry = new LogEntry
                {
                    Timestamp          = DateTime.UtcNow,
                    ClientIp           = context.Connection.RemoteIpAddress?.ToString(),
                    Method             = context.Request.Method,
                    Path               = context.Request.Path,
                    ClusterId          = cluster?.ClusterId,
                    DestinationAddress = destination?.Model?.Config?.Address,
                    StatusCode         = context.Response.StatusCode,
                    DurationMs         = sw.Elapsed.TotalMilliseconds,
                };

                logService.Enqueue(logEntry);
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("YarpLoggingMiddleware");
                logger.LogError(ex, "Failed to enqueue log entry for {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            }
        });
    }
}
