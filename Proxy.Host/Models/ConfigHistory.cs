using LiteDB;

namespace Proxy.Host.Models;

public class ConfigHistory
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EntityType { get; set; } = string.Empty;  // "route" | "cluster"
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;       // "create" | "update" | "delete"
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
}
