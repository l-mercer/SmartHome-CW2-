using SmartHome.Core.Models;

namespace SmartHome.Core.Services;

public record AlertResult(
    double ConfidenceScore,
    bool ShouldEscalate,
    IncidentType? DetectedType,
    List<SensorEvent> EvidenceUsed
);

public class AlertConfirmationEngine
{
    private readonly List<SensorEvent> _recentEvents = new();
    private readonly TimeSpan _correlationWindow = TimeSpan.FromSeconds(10);
    private readonly object _lock = new();

    public AlertResult Evaluate(SensorEvent newEvent)
    {
        lock (_lock)
        {
            _recentEvents.Add(newEvent);

            var now = newEvent.Timestamp;
            _recentEvents.RemoveAll(e => e.Timestamp < now.Subtract(_correlationWindow));

            if (newEvent.Type == SensorType.Smoke || newEvent.Type == SensorType.Heat)
            {
                bool isHigh = newEvent.Value > 80;
                
                if (isHigh)
                {
                    if (newEvent.Value > 90)
                    {
                        return new AlertResult(1.0, true, IncidentType.Fire, new List<SensorEvent> { newEvent });
                    }
                    else
                    {
                        return new AlertResult(0.5, true, IncidentType.Fire, new List<SensorEvent> { newEvent });
                    }
                }
            }

            if (newEvent.Type == SensorType.DoorContact || newEvent.Type == SensorType.Motion)
            {
                var doorEvent = _recentEvents.OrderByDescending(e => e.Timestamp).FirstOrDefault(e => e.Type == SensorType.DoorContact && e.Value == 1);
                var motionEvent = _recentEvents.OrderByDescending(e => e.Timestamp).FirstOrDefault(e => e.Type == SensorType.Motion && e.Value == 1);

                if (doorEvent != null && motionEvent != null)
                {
                    var diff = (doorEvent.Timestamp - motionEvent.Timestamp).Duration();
                    if (diff <= _correlationWindow)
                    {
                        return new AlertResult(1.0, true, IncidentType.BreakIn, new List<SensorEvent> { doorEvent, motionEvent });
                    }
                }
                
                return new AlertResult(0.3, false, IncidentType.BreakIn, new List<SensorEvent> { newEvent });
            }

            return new AlertResult(0.0, false, null, new List<SensorEvent>());
        }
    }
}
