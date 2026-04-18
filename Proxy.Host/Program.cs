using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Proxy.Host.Models;
using Microsoft.IdentityModel.Tokens;
using Proxy.Host.Middleware;
using Proxy.Host.Providers;
using Proxy.Host.Repositories;
using Proxy.Host.Services;
using Scalar.AspNetCore;
using System.Text;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// OpenAPI / Scalar — development only
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "YARP Proxy Manager API";
            document.Info.Version = "v1";
            document.Info.Description = "REST API for managing YARP reverse proxy routes and clusters.";
            return Task.CompletedTask;
        });
    });
}

// Configure LiteDB
builder.Services.AddSingleton<LiteDbService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiteDbService>()); // same instance
builder.Services.AddSingleton<LogService>();
builder.Services.AddHostedService<LogWriterService>();
builder.Services.AddSingleton<HistoryService>();
builder.Services.AddHostedService<HistoryWriterService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<LiteDbHealthCheck>("litedb");

// Repository pattern
builder.Services.AddSingleton<IRouteRepository, LiteDbRouteRepository>();
builder.Services.AddSingleton<IClusterRepository, LiteDbClusterRepository>();

// Configure Custom YARP Provider
builder.Services.AddSingleton<LiteDbProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<LiteDbProxyConfigProvider>());
builder.Services.AddReverseProxy();

// Load JWT Settings
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings.GetValue<string>("Key");
if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException(
        "JWT key is not configured. Set 'Jwt:Key' via environment variable (JWT__KEY) or user-secrets. " +
        "Minimum 64 characters required for HMAC-SHA512.");
var issuer = jwtSettings.GetValue<string>("Issuer") ?? "AntiGravityProxyAuth";

// Configure Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });
builder.Services.AddAuthorization();

// IP-based rate limiter on login endpoint: max 10 requests per minute per IP
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.RejectionStatusCode = 429;
});

// Configure CORS — only needed for local dev where Angular runs on :4200
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngular",
            policy => policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
                            .AllowAnyMethod()
                            .AllowAnyHeader());
    });
}

var app = builder.Build();

// Serve Angular SPA from wwwroot (in production)
app.UseDefaultFiles();   // serves index.html for /
app.UseStaticFiles();    // serves .js, .css, etc.

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAngular");
    app.MapOpenApi();                   // serves /openapi/v1.json
    app.MapScalarApiReference();        // serves /scalar/v1
}

// Global exception handler — hides stack traces in production
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    var feature = ctx.Features.Get<IExceptionHandlerFeature>();
    var isDev = app.Environment.IsDevelopment();
    var msg = isDev && feature?.Error != null
        ? feature.Error.Message
        : "An unexpected error occurred.";
    await ctx.Response.WriteAsJsonAsync(new ApiError("INTERNAL_SERVER_ERROR", msg));
}));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Enforce password change: block all API calls (except change-password) when MustChangePassword claim is set
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && context.User.FindFirst("must_change_password")?.Value == "true"
        && !context.Request.Path.StartsWithSegments("/api/auth/change-password"))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsJsonAsync(
            new ApiError("PASSWORD_CHANGE_REQUIRED", "You must change your password before using the API."));
        return;
    }
    await next();
});

// Health endpoint — no auth, for load balancers / Docker
app.MapHealthChecks("/health").AllowAnonymous();

// API controllers — mapped before YARP so they take priority
app.MapControllers();

// YARP proxy pipeline
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseYarpLogging();
});

// SPA fallback: any unmatched route returns index.html (for Angular deep links)
// This must come AFTER API and YARP routes
app.MapFallbackToFile("index.html");

app.Run();
