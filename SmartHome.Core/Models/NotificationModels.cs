namespace SmartHome.Core.Models;

public record NotificationMessage(
    string IncidentId,
    string Message,
    IncidentType Priority
);

public record NotificationResult(
    bool Success,
    string Channel,
    string Details,
    TimeSpan Duration
);


