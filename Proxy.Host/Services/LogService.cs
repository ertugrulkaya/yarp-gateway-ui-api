using LiteDB;
using Proxy.Host.Models;

namespace Proxy.Host.Services;

public class LogService : IDisposable
{
    private readonly LiteDatabase _db;
    private const string CollectionName = "logs";

    public LogService(IConfiguration configuration)
    {
        var dbPath = configuration["LiteDb:LogPath"] ?? "proxy-log.db";
        // Use Shared connection mode to avoid locking issues in web environments
        _db = new LiteDatabase($"Filename={dbPath};Connection=shared");
        
        // Ensure index on Timestamp for faster querying and deletion
        var collection = _db.GetCollection<LogEntry>(CollectionName);
        collection.EnsureIndex(x => x.Timestamp);
    }


    public void LogRequest(LogEntry entry)
    {
        var collection = _db.GetCollection<LogEntry>(CollectionName);
        collection.Insert(entry);
    }

    public IEnumerable<LogEntry> GetLogs(int limit = 100, int offset = 0)
    {
        var collection = _db.GetCollection<LogEntry>(CollectionName);
        return collection.Query()
            .OrderByDescending(x => x.Timestamp)
            .Offset(offset)
            .Limit(limit)
            .ToEnumerable();
    }

    public long GetTotalCount()
    {
        var collection = _db.GetCollection<LogEntry>(CollectionName);
        return collection.Count();
    }

    public int ClearLogs()
    {
        var collection = _db.GetCollection<LogEntry>(CollectionName);
        return collection.DeleteAll();
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
