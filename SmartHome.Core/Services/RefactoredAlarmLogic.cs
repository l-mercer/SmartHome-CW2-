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
        // 1. Validation
        var validation = _validator.Validate(sensorEvent);
        if (!validation.IsValid)
        {
            _auditLog.Append("EventRejected", $"Invalid event: {string.Join(", ", validation.Errors)}", sensorEvent?.EventId);
            return;
        }

        // 2. Deduplication
        if (_dedupStore.IsDuplicate(sensorEvent.EventId))
        {
            _auditLog.Append("EventDuplicate", "Ignored duplicate event", sensorEvent.EventId);
            return;
        }
        _dedupStore.MarkProcessed(sensorEvent.EventId);

        // 3. Correlation
        var alertResult = _correlationEngine.Evaluate(sensorEvent);

        if (alertResult.DetectedType != null)
        {
            // 4. Incident Management
            // Create idempotency key based on alert type + approximate time bucket (or just correlation logic)
            // For this demo, let's say one incident per type per minute? 
            // Or use the first event ID as part of key?
            // AlertResult doesn't give a unique ID for the "Incident" instance it implies.
            // Let's use "Incident-{Type}-{Day}" or similar to allow grouping, 
            // or better, if we have a valid alert, we try to create an incident.
            
            // To prevent spamming new incidents for the same ongoing event in short succession:
            // The correlation engine might return the same "Incident" repeatedly.
            // We need a key that represents this specific *instance* of a problem.
            // For the Break-in (Door+Motion), use the first event ID or a combo.
            
            string idempotencyKey = $"Inc-{alertResult.DetectedType}-{DateTimeOffset.UtcNow.ToString("yyyyMMddHHmm")}";

            // If we are escalating, we create/update incident
            var incident = _incidentService.CreateOrUpdateIncident(
                alertResult.EvidenceUsed, 
                alertResult.ConfidenceScore, 
                alertResult.DetectedType.Value,
                idempotencyKey
            );

            // 5. Notification Decision
            if (incident.State == IncidentState.Confirmed)
            {
                // Only notify if not already notified
                // But CreateOrUpdate returns existing state.
                // We should check if we already notified this incident?
                // The state machine handles "Notified" state.
                
                try 
                {
                    // Transition to Notified if not already
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
                // Suspected but escalate (e.g. fire warning)
                // In this simplified logic, maybe we don't notify unless confirmed?
                // Or maybe we send a lower priority notification?
                _auditLog.Append("AlertSuspected", $"Suspected {alertResult.DetectedType}, waiting for more evidence", incident.IncidentId);
            }
        }
        else
        {
            // No alert detected yet (e.g. single door open)
            // _auditLog.Append("EventProcessed", $"Event {sensorEvent.Type} processed, no alert", sensorEvent.EventId);
        }
    }
}

