using LiteDB;
using Proxy.Host.Models;
using Proxy.Host.Services;

namespace Proxy.Host.Repositories;

public class LiteDbClusterRepository : IClusterRepository
{
    private readonly ILiteCollection<ClusterConfigWrapper> _col;

    public LiteDbClusterRepository(LiteDbService db)
    {
        _col = db.Database.GetCollection<ClusterConfigWrapper>("clusters");
    }

    public IEnumerable<ClusterDto> GetAll() =>
        _col.FindAll().Select(x => x.Config);

    public ClusterDto? FindById(string clusterId) =>
        _col.FindById(new BsonValue(clusterId))?.Config;

    public bool ExistsById(string clusterId) =>
        _col.FindById(new BsonValue(clusterId)) != null;

    public void Insert(ClusterDto cluster) =>
        _col.Insert(new ClusterConfigWrapper { ClusterId = cluster.ClusterId, Config = cluster });

    public void Upsert(ClusterDto cluster) =>
        _col.Upsert(new ClusterConfigWrapper { ClusterId = cluster.ClusterId, Config = cluster });

    public bool Delete(string clusterId) =>
        _col.Delete(new BsonValue(clusterId));

    public int Count() => _col.Count();
}
