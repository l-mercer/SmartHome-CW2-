namespace SmartHome.Core.Models;

public record SensorEvent(
    string EventId,
    string DeviceId,
    SensorType Type,
    double Value,
    DateTimeOffset Timestamp,
    string Signature
);

