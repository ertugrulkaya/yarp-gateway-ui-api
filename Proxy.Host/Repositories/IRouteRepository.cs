using Proxy.Host.Models;

namespace Proxy.Host.Repositories;

public interface IRouteRepository
{
    IEnumerable<RouteDto> GetAll();
    RouteDto? FindById(string routeId);
    bool ExistsById(string routeId);
    void Insert(RouteDto route);
    void Upsert(RouteDto route);
    bool Delete(string routeId);
    int Count();
}
