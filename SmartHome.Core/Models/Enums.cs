namespace SmartHome.Core.Models;

public enum SensorType
{
    DoorContact,
    Motion,
    Smoke,
    Heat
}

public enum IncidentType
{
    BreakIn,
    Fire
}

public enum IncidentState
{
    Detected,
    Suspected,
    Confirmed,
    Notified,
    NotificationFailed,
    Acknowledged,
    Resolved,
    Closed,
    Archived
}


