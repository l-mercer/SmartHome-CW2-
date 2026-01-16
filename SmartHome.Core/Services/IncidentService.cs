using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;

namespace SmartHome.Core.Services;

public class IncidentService
{
    private readonly IIncidentRepository _repository;
    private readonly IAuditLog _auditLog;

    public IncidentService(IIncidentRepository repository, IAuditLog auditLog)
    {
        _repository = repository;
        _auditLog = auditLog;
    }

    public Incident CreateOrUpdateIncident(List<SensorEvent> evidence, double confidenceScore, IncidentType type, string idempotencyKey)
    {
        var incident = _repository.GetByIdempotencyKey(idempotencyKey);
        
        if (incident == null)
        {
            incident = new Incident
            {
                Type = type,
                ConfidenceScore = confidenceScore,
                Evidence = evidence,
                State = confidenceScore >= 1.0 ? IncidentState.Confirmed : IncidentState.Suspected
            };

            _repository.Save(incident);
            _repository.RegisterIdempotencyKey(idempotencyKey, incident.IncidentId);
            
            _auditLog.Append("IncidentCreated", $"Created {type} incident with score {confidenceScore} and state {incident.State}", incident.IncidentId);
        }
        else
        {
            _auditLog.Append("IncidentDedup", $"Updating existing incident for key {idempotencyKey}", incident.IncidentId);
            
            incident.Evidence.AddRange(evidence.Where(e => !incident.Evidence.Any(existing => existing.EventId == e.EventId)));

            if (confidenceScore > incident.ConfidenceScore)
            {
                incident.ConfidenceScore = confidenceScore;
            }

            if (confidenceScore >= 1.0 && incident.State != IncidentState.Confirmed && incident.State != IncidentState.Notified && incident.State != IncidentState.Resolved)
            {
                try 
                {
                     TransitionState(incident.IncidentId, IncidentState.Confirmed);
                }
                catch
                {
                }
            }
            
            incident.LastUpdatedAt = DateTimeOffset.UtcNow;
            _repository.Save(incident);
        }

        return incident;
    }

    public void TransitionState(string incidentId, IncidentState newState)
    {
        var incident = _repository.GetById(incidentId);
        if (incident == null)
        {
            throw new KeyNotFoundException($"Incident {incidentId} not found");
        }

        if (!IsValidTransition(incident.State, newState))
        {
            var msg = $"Invalid state transition from {incident.State} to {newState}";
            _auditLog.Append("StateTransitionFailed", msg, incidentId);
            throw new InvalidOperationException(msg);
        }

        var oldState = incident.State;
        incident.State = newState;
        incident.LastUpdatedAt = DateTimeOffset.UtcNow;
        _repository.Save(incident);

        _auditLog.Append("StateTransition", $"Transitioned from {oldState} to {newState}", incidentId);
    }

    private bool IsValidTransition(IncidentState current, IncidentState next)
    {
        if (current == next) return true;

        switch (current)
        {
            case IncidentState.Detected:
                return next == IncidentState.Suspected || next == IncidentState.Confirmed;
            case IncidentState.Suspected:
                return next == IncidentState.Confirmed || next == IncidentState.Resolved;
            case IncidentState.Confirmed:
                return next == IncidentState.Notified || next == IncidentState.NotificationFailed || next == IncidentState.Resolved;
            case IncidentState.Notified:
                return next == IncidentState.Acknowledged || next == IncidentState.Resolved;
            case IncidentState.NotificationFailed:
                return next == IncidentState.Notified || next == IncidentState.Resolved;
            case IncidentState.Acknowledged:
                return next == IncidentState.Resolved;
            case IncidentState.Resolved:
                return next == IncidentState.Closed;
            case IncidentState.Closed:
                return next == IncidentState.Archived;
            case IncidentState.Archived:
                return false;
            default:
                return false;
        }
    }
}
