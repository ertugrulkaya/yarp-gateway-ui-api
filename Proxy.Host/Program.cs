using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Proxy.Host.Middleware;
using Proxy.Host.Providers;
using Proxy.Host.Services;
using System.Text;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure LiteDB
builder.Services.AddSingleton<LiteDbService>();
builder.Services.AddSingleton<LogService>();

// Configure Custom YARP Provider
builder.Services.AddSingleton<LiteDbProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<LiteDbProxyConfigProvider>());
builder.Services.AddReverseProxy();

// Load JWT Settings
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings.GetValue<string>("Key") ?? "SuperSecretHmacKeyForProxyManager2026!++AndItNeedsToBeAtLeast64CharactersLongToWorkWithSha512";
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
}

app.UseAuthentication();
app.UseAuthorization();

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
