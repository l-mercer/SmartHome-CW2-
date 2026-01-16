using SmartHome.Core.Models;

namespace SmartHome.Core.Interfaces;

public interface IIncidentRepository
{
    void Save(Incident incident);
    Incident? GetById(string incidentId);
    Incident? GetByIdempotencyKey(string key);
    void RegisterIdempotencyKey(string key, string incidentId);
}

