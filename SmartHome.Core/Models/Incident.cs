namespace SmartHome.Core.Models;

public class Incident
{
    public string IncidentId { get; set; } = Guid.NewGuid().ToString();
    public IncidentType Type { get; set; }
    public IncidentState State { get; set; }
    public double ConfidenceScore { get; set; }
    public List<SensorEvent> Evidence { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}


