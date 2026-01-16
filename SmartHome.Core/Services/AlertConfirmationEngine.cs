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
    // Simple in-memory evidence window: list of recent events
    private readonly List<SensorEvent> _recentEvents = new();
    private readonly TimeSpan _correlationWindow = TimeSpan.FromSeconds(10);
    private readonly object _lock = new();

    public AlertResult Evaluate(SensorEvent newEvent)
    {
        lock (_lock)
        {
            // Add new event
            _recentEvents.Add(newEvent);

            // Cleanup old events
            var now = newEvent.Timestamp;
            _recentEvents.RemoveAll(e => e.Timestamp < now.Subtract(_correlationWindow));

            // Rule 1: Fire (Smoke or Heat)
            if (newEvent.Type == SensorType.Smoke || newEvent.Type == SensorType.Heat)
            {
                // Simple logic: if value > 50 considered "high"
                // For Smoke (0-1000), let's say > 50 is smoke detected.
                // For Heat (0-1000), let's say > 50 degrees C? Assuming value is temperature? 
                // Or maybe the sensor sends boolean-ish 0/1 for smoke? 
                // Prompt says "Smoke OR Heat exceed threshold".
                
                // Let's assume > 80 is threshold for both for simplicity, or 1 for boolean.
                // Let's assume analog values for demonstration of "confidence".
                
                bool isHigh = newEvent.Value > 80;
                
                if (isHigh)
                {
                    // Fail-safe: if only one sensor, maybe just suspected?
                    // But usually smoke is critical.
                    // Prompt: "If fire sensors are high confidence -> Confirmed. If uncertain -> Suspected"
                    // Let's say Value > 90 is high confidence, > 50 is uncertain.
                    
                    if (newEvent.Value > 90)
                    {
                        return new AlertResult(1.0, true, IncidentType.Fire, new List<SensorEvent> { newEvent });
                    }
                    else
                    {
                        // Suspected
                        return new AlertResult(0.5, true, IncidentType.Fire, new List<SensorEvent> { newEvent });
                    }
                }
            }

            // Rule 2: Break-in (Door + Motion within window)
            // If this event is Door or Motion, look for the other one in window.
            if (newEvent.Type == SensorType.DoorContact || newEvent.Type == SensorType.Motion)
            {
                // Check if we have both types in the window
                var doorEvent = _recentEvents.OrderByDescending(e => e.Timestamp).FirstOrDefault(e => e.Type == SensorType.DoorContact && e.Value == 1); // 1 = Open
                var motionEvent = _recentEvents.OrderByDescending(e => e.Timestamp).FirstOrDefault(e => e.Type == SensorType.Motion && e.Value == 1); // 1 = Detected

                if (doorEvent != null && motionEvent != null)
                {
                    // Calculate time difference
                    var diff = (doorEvent.Timestamp - motionEvent.Timestamp).Duration();
                    if (diff <= _correlationWindow)
                    {
                        return new AlertResult(1.0, true, IncidentType.BreakIn, new List<SensorEvent> { doorEvent, motionEvent });
                    }
                }
                
                // If single event, suspected but not confirmed?
                // Prompt: "Rule: Break-in confirmed when DoorContact + Motion occur within 10 seconds"
                // Prompt: "Single motion event => suspected only, no notify" (Wait, Scenario 3 says "suspected only, no notify")
                // So return ShouldEscalate = false for single event.
                
                return new AlertResult(0.3, false, IncidentType.BreakIn, new List<SensorEvent> { newEvent });
            }

            return new AlertResult(0.0, false, null, new List<SensorEvent>());
        }
    }
}

