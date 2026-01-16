using SmartHome.Core.Models;

namespace SmartHome.Core.Services;

public class BadAlarmLogic
{
    private readonly Action<string> _logger;

    public BadAlarmLogic(Action<string> logger)
    {
        _logger = logger;
    }

    public void ProcessEvent(SensorEvent sensorEvent)
    {
        if (sensorEvent.Type == SensorType.DoorContact || sensorEvent.Type == SensorType.Motion)
        {
            _logger($"[BAD] Break-in detected! (Triggered by {sensorEvent.Type})");
            _logger($"[BAD] NOTIFYING POLICE IMMEDIATELY via SMS..."); 
        }
    }
}
