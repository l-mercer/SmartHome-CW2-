using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;
using SmartHome.Core.Services;
using SmartHome.Core.Validation;
using Xunit;

namespace SmartHome.Tests;

public class ReliabilityTests
{
    [Fact]
    public void Validator_ShouldRejectFutureTimestamp()
    {
        var validator = new SensorIngestValidator();
        var futureEvent = new SensorEvent(
            Guid.NewGuid().ToString(), "Dev1", SensorType.Motion, 1, 
            DateTimeOffset.UtcNow.AddMinutes(10), "valid_signature");

        var result = validator.Validate(futureEvent);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("future"));
    }

    [Fact]
    public void Deduplication_ShouldDetectDuplicate()
    {
        var store = new DeduplicationStore();
        var id = "duplicate-id";

        // First pass
        Assert.False(store.IsDuplicate(id));
        store.MarkProcessed(id);

        // Second pass
        Assert.True(store.IsDuplicate(id));
    }

    [Fact]
    public void CorrelationEngine_DoorAndMotion_ShouldConfirmBreakIn()
    {
        var engine = new AlertConfirmationEngine();
        
        var motion = new SensorEvent("1", "Dev1", SensorType.Motion, 1, DateTimeOffset.UtcNow, "sig");
        var door = new SensorEvent("2", "Dev1", SensorType.DoorContact, 1, DateTimeOffset.UtcNow.AddSeconds(2), "sig");

        engine.Evaluate(motion);
        var result = engine.Evaluate(door);

        Assert.True(result.ConfidenceScore >= 1.0);
        Assert.Equal(IncidentType.BreakIn, result.DetectedType);
    }

    [Fact]
    public void CorrelationEngine_SingleMotion_ShouldNotConfirm()
    {
        var engine = new AlertConfirmationEngine();
        var motion = new SensorEvent("1", "Dev1", SensorType.Motion, 1, DateTimeOffset.UtcNow, "sig");

        var result = engine.Evaluate(motion);

        // Expect low confidence / suspected
        Assert.True(result.ConfidenceScore < 1.0);
        // Depending on logic, it might return "BreakIn" type but with low confidence/ShouldEscalate=false
        // My logic returns ShouldEscalate = false for single event
        Assert.False(result.ShouldEscalate);
    }

    [Fact]
    public async Task NotificationService_ShouldFallback_WhenSmsFails()
    {
        // Setup
        var audit = new AuditLog();
        var sms = new MockProvider("SMS", false); // Fails
        var push = new MockProvider("Push", true); // Succeeds
        var email = new MockProvider("Email", true);
        
        var service = new NotificationService(new List<INotificationProvider> { sms, push, email }, audit);
        var incident = new Incident { Type = IncidentType.Fire, State = IncidentState.Confirmed };

        // Act
        var result = await service.NotifyAsync(incident);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Push", result.Channel); // Should be Push because SMS failed
        Assert.Equal(2, sms.Calls); // 1 initial + 1 retry
        Assert.Equal(1, push.Calls);
        Assert.Equal(0, email.Calls); // Shouldn't reach email
    }

    [Fact]
    public void IncidentService_InvalidTransition_ShouldThrow()
    {
        var repo = new IncidentRepository();
        var audit = new AuditLog();
        var service = new IncidentService(repo, audit);

        var incident = new Incident { State = IncidentState.Confirmed };
        repo.Save(incident);

        // Confirmed -> Detected is invalid (backward step not allowed)
        Assert.Throws<InvalidOperationException>(() => 
            service.TransitionState(incident.IncidentId, IncidentState.Detected));
    }

    // Helper Mock
    class MockProvider : INotificationProvider
    {
        public string ChannelName { get; }
        private readonly bool _succeeds;
        public int Calls { get; private set; }

        public MockProvider(string name, bool succeeds)
        {
            ChannelName = name;
            _succeeds = succeeds;
        }

        public Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken token)
        {
            Calls++;
            if (_succeeds)
                return Task.FromResult(new NotificationResult(true, ChannelName, "OK", TimeSpan.Zero));
            
            return Task.FromResult(new NotificationResult(false, ChannelName, "Fail", TimeSpan.Zero));
        }
    }
}

