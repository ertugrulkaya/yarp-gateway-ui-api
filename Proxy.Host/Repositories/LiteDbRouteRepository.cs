using LiteDB;
using Proxy.Host.Models;
using Proxy.Host.Services;

namespace Proxy.Host.Repositories;

public class LiteDbRouteRepository : IRouteRepository
{
    private readonly ILiteCollection<RouteConfigWrapper> _col;

    public LiteDbRouteRepository(LiteDbService db)
    {
        _col = db.Database.GetCollection<RouteConfigWrapper>("routes");
    }

    public IEnumerable<RouteDto> GetAll() =>
        _col.FindAll().Select(x => x.Config);

    public RouteDto? FindById(string routeId) =>
        _col.FindById(new BsonValue(routeId))?.Config;

    public bool ExistsById(string routeId) =>
        _col.FindById(new BsonValue(routeId)) != null;

    public void Insert(RouteDto route) =>
        _col.Insert(new RouteConfigWrapper { RouteId = route.RouteId, Config = route });

    public void Upsert(RouteDto route) =>
        _col.Upsert(new RouteConfigWrapper { RouteId = route.RouteId, Config = route });

    public bool Delete(string routeId) =>
        _col.Delete(new BsonValue(routeId));

    public int Count() => _col.Count();
}
