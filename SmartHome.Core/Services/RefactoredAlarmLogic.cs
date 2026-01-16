using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;
using SmartHome.Core.Validation;

namespace SmartHome.Core.Services;

public class RefactoredAlarmLogic
{
    private readonly SensorIngestValidator _validator;
    private readonly IDeduplicationStore _dedupStore;
    private readonly AlertConfirmationEngine _correlationEngine;
    private readonly IncidentService _incidentService;
    private readonly NotificationService _notificationService;
    private readonly IAuditLog _auditLog;

    public RefactoredAlarmLogic(
        SensorIngestValidator validator,
        IDeduplicationStore dedupStore,
        AlertConfirmationEngine correlationEngine,
        IncidentService incidentService,
        NotificationService notificationService,
        IAuditLog auditLog)
    {
        _validator = validator;
        _dedupStore = dedupStore;
        _correlationEngine = correlationEngine;
        _incidentService = incidentService;
        _notificationService = notificationService;
        _auditLog = auditLog;
    }

    public async Task ProcessEventAsync(SensorEvent sensorEvent)
    {
        var validation = _validator.Validate(sensorEvent);
        if (!validation.IsValid)
        {
            _auditLog.Append("EventRejected", $"Invalid event: {string.Join(", ", validation.Errors)}", sensorEvent?.EventId);
            return;
        }

        if (_dedupStore.IsDuplicate(sensorEvent.EventId))
        {
            _auditLog.Append("EventDuplicate", "Ignored duplicate event", sensorEvent.EventId);
            return;
        }
        _dedupStore.MarkProcessed(sensorEvent.EventId);

        var alertResult = _correlationEngine.Evaluate(sensorEvent);

        if (alertResult.DetectedType != null)
        {
            string idempotencyKey = $"Inc-{alertResult.DetectedType}-{DateTimeOffset.UtcNow.ToString("yyyyMMddHHmm")}";

            var incident = _incidentService.CreateOrUpdateIncident(
                alertResult.EvidenceUsed, 
                alertResult.ConfidenceScore, 
                alertResult.DetectedType.Value,
                idempotencyKey
            );

            if (incident.State == IncidentState.Confirmed)
            {
                try 
                {
                    if (incident.State != IncidentState.Notified && incident.State != IncidentState.NotificationFailed)
                    {
                        var result = await _notificationService.NotifyAsync(incident);
                        if (result.Success)
                        {
                            _incidentService.TransitionState(incident.IncidentId, IncidentState.Notified);
                        }
                        else
                        {
                            _incidentService.TransitionState(incident.IncidentId, IncidentState.NotificationFailed);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _auditLog.Append("LogicError", ex.Message, incident.IncidentId);
                }
            }
            else if (alertResult.ShouldEscalate)
            {
                _auditLog.Append("AlertSuspected", $"Suspected {alertResult.DetectedType}, waiting for more evidence", incident.IncidentId);
            }
        }
        else
        {
        }
    }
}
