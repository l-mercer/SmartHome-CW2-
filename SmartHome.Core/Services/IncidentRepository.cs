using System.Collections.Concurrent;
using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;

namespace SmartHome.Core.Services;

public class IncidentRepository : IIncidentRepository
{
    private readonly ConcurrentDictionary<string, Incident> _incidents = new();
    private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();

    public void Save(Incident incident)
    {
        _incidents.AddOrUpdate(incident.IncidentId, incident, (k, v) => incident);
    }

    public Incident? GetById(string incidentId)
    {
        _incidents.TryGetValue(incidentId, out var incident);
        return incident;
    }

    public Incident? GetByIdempotencyKey(string key)
    {
        if (_idempotencyIndex.TryGetValue(key, out var incidentId))
        {
            return GetById(incidentId);
        }
        return null;
    }

    public void RegisterIdempotencyKey(string key, string incidentId)
    {
        _idempotencyIndex.TryAdd(key, incidentId);
    }
}
