using LiteDB;
using Proxy.Host.Models;
using System.Threading.Channels;

namespace Proxy.Host.Services;

public class HistoryService
{
    private readonly LiteDbService _db;
    private const string CollectionName = "config_history";

    private readonly Channel<ConfigHistory> _channel = Channel.CreateBounded<ConfigHistory>(
        new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public ChannelReader<ConfigHistory> Reader => _channel.Reader;

    public HistoryService(LiteDbService db) => _db = db;

    /// <summary>Non-blocking enqueue from the request path.</summary>
    public virtual void Enqueue(ConfigHistory entry) => _channel.Writer.TryWrite(entry);

    /// <summary>Called only by HistoryWriterService on its dedicated background thread.</summary>
    internal void WriteToDb(ConfigHistory entry)
    {
        var col = _db.Database.GetCollection<ConfigHistory>(CollectionName);
        col.Insert(entry);
    }

    internal void CompleteChannel() => _channel.Writer.TryComplete();
}
