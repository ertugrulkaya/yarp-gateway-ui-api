using Proxy.Host.Models;

namespace Proxy.Host.Repositories;

public interface IClusterRepository
{
    IEnumerable<ClusterDto> GetAll();
    ClusterDto? FindById(string clusterId);
    bool ExistsById(string clusterId);
    void Insert(ClusterDto cluster);
    void Upsert(ClusterDto cluster);
    bool Delete(string clusterId);
    int Count();
}
