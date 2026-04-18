using LiteDB;
using Proxy.Host.Models;
using System.Threading.Channels;

namespace Proxy.Host.Services;

public class LogService : IDisposable
{
    private readonly LiteDatabase _db;
    private const string CollectionName = "logs";

    // Bounded channel: drops entries under extreme load rather than OOM-ing
    private readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelReader<LogEntry> Reader => _channel.Reader;

    public LogService(IConfiguration configuration)
    {
        var dbPath = configuration["LiteDb:LogPath"] ?? "proxy-log.db";
        _db = new LiteDatabase($"Filename={dbPath};Connection=shared");

        var collection = _db.GetCollection<LogEntry>(CollectionName);
        collection.EnsureIndex(x => x.Timestamp);
        collection.EnsureIndex(x => x.ClusterId);
        collection.EnsureIndex(x => x.StatusCode);
        collection.EnsureIndex(x => x.ClientIp);
    }

    /// <summary>Non-blocking enqueue from the proxy middleware.</summary>
    public void Enqueue(LogEntry entry) => _channel.Writer.TryWrite(entry);

    /// <summary>Called only by LogWriterService on its dedicated background thread.</summary>
    internal void WriteToDb(LogEntry entry)
    {
        var collection = _db.GetCollection<LogEntry>(CollectionName);
        collection.Insert(entry);
    }

    internal void CompleteChannel() => _channel.Writer.TryComplete();

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
