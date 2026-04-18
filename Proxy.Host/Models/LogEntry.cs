using LiteDB;

namespace Proxy.Host.Models;

public class LogEntry
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ClientIp { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string? ClusterId { get; set; }
    public string? DestinationAddress { get; set; }
    public int StatusCode { get; set; }
    public double DurationMs { get; set; }
}
