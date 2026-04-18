namespace Proxy.Host.Models;

/// <summary>Standard error envelope returned by all API endpoints.</summary>
public record ApiError(string Code, string Message);
