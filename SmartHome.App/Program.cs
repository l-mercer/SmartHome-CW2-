using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;
using SmartHome.Core.Services;
using SmartHome.Core.Validation;

namespace SmartHome.App;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== Smart Home Reliability Evidence Generator ===");
        Console.WriteLine($"Time: {DateTime.Now}");
        Console.WriteLine("=================================================");
        Console.WriteLine();

        // 1. Setup Dependency Injection (Manual for Console App)
        var auditLog = new AuditLog();
        var dedupStore = new DeduplicationStore();
        var incidentRepo = new IncidentRepository();
        var validator = new SensorIngestValidator();
        var correlationEngine = new AlertConfirmationEngine();
        
        var incidentService = new IncidentService(incidentRepo, auditLog);
        
        var providers = new List<INotificationProvider> 
        { 
            new SmsProvider(), 
            new PushProvider(), 
            new EmailProvider() 
        };
        var notificationService = new NotificationService(providers, auditLog);

        var refactoredLogic = new RefactoredAlarmLogic(
            validator, dedupStore, correlationEngine, incidentService, notificationService, auditLog
        );

        var badLogic = new BadAlarmLogic(msg => Console.WriteLine($"[BAD LOGIC] {msg}"));

        // Helper to Create Events
        SensorEvent CreateEvent(string type, double val, string? id = null)
        {
            return new SensorEvent(
                EventId: id ?? Guid.NewGuid().ToString(),
                DeviceId: "Dev-1",
                Type: Enum.Parse<SensorType>(type),
                Value: val,
                Timestamp: DateTimeOffset.UtcNow,
                Signature: "valid_signature"
            );
        }

        // ==========================================
        // SCENARIO 1: Invalid Event (Validation)
        // ==========================================
        Console.WriteLine("--- SCENARIO 1: Invalid Event (Validation at Boundaries) ---");
        var invalidEvent = new SensorEvent(
            EventId: Guid.NewGuid().ToString(),
            DeviceId: "Dev-1",
            Type: SensorType.DoorContact,
            Value: 5, // Invalid for Door (0/1)
            Timestamp: DateTimeOffset.UtcNow,
            Signature: "bad_sig" // Invalid signature
        );
        Console.WriteLine($"Input: DoorContact with Value=5 and invalid signature");
        await refactoredLogic.ProcessEventAsync(invalidEvent);
        PrintAuditLogs(auditLog, 1);
        Console.WriteLine();

        // ==========================================
        // SCENARIO 2: Deduplication
        // ==========================================
        Console.WriteLine("--- SCENARIO 2: Deduplication (Idempotency) ---");
        var dupId = Guid.NewGuid().ToString();
        var eventA = CreateEvent("Motion", 1, dupId);
        var eventB = CreateEvent("Motion", 1, dupId); // Same ID

        Console.WriteLine("Sending 1st event...");
        await refactoredLogic.ProcessEventAsync(eventA);
        Console.WriteLine("Sending 2nd event (duplicate)...");
        await refactoredLogic.ProcessEventAsync(eventB);
        PrintAuditLogs(auditLog, 1); // Should see "Ignored duplicate"
        Console.WriteLine();

        // ==========================================
        // BAD VS REFACTORED DEMO
        // ==========================================
        Console.WriteLine("--- BAD VS REFACTORED LOGIC DEMO ---");
        
        Console.WriteLine("\n[Run: BAD LOGIC]");
        // Bad logic triggers on single event
        badLogic.ProcessEvent(CreateEvent("Motion", 1)); // Triggers immediately
        
        Console.WriteLine("\n[Run: REFACTORED LOGIC]");
        // Refactored logic waits for correlation
        var motionEvent = CreateEvent("Motion", 1);
        Console.WriteLine("Step A: Single Motion Event (Suspected)");
        await refactoredLogic.ProcessEventAsync(motionEvent);
        // Check logs for no notification yet
        
        var doorEvent = CreateEvent("DoorContact", 1);
        Console.WriteLine("Step B: Door Event within 10s (Confirmed)");
        await refactoredLogic.ProcessEventAsync(doorEvent);
        // Should trigger notification
        PrintAuditLogs(auditLog, 3); // NotificationSuccess
        Console.WriteLine();

        // ==========================================
        // SCENARIO 5: Fallback Logic
        // ==========================================
        Console.WriteLine("--- SCENARIO 5: Reliability - Notification Fallback ---");
        // Force a new confirmed incident that will fail SMS
        // The SmsProvider logic fails randomly or always.
        // We configured it to fail for demonstration.
        // Let's create a Fire incident to trigger another flow
        
        var fireEvent = CreateEvent("Smoke", 100); // High confidence
        Console.WriteLine("Input: High Confidence Smoke Event");
        await refactoredLogic.ProcessEventAsync(fireEvent);
        
        PrintAuditLogs(auditLog, 5); // Should see SMS fail then Push success
        Console.WriteLine();

        // ==========================================
        // SCENARIO 6: Invalid State Transition
        // ==========================================
        Console.WriteLine("--- SCENARIO 6: Incident State Machine Integrity ---");
        try 
        {
            // Get an incident (we just created one for fire)
            var incident = incidentRepo.GetByIdempotencyKey($"Inc-Fire-{DateTimeOffset.UtcNow:yyyyMMddHHmm}");
            if (incident != null)
            {
                Console.WriteLine($"Current State: {incident.State}");
                Console.WriteLine("Attempting illegal transition: Confirmed -> Detected");
                incidentService.TransitionState(incident.IncidentId, IncidentState.Detected);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CAUGHT EXCEPTION: {ex.Message}");
        }
        PrintAuditLogs(auditLog, 1);
        Console.WriteLine();

        Console.WriteLine("=================================================");
        Console.WriteLine($"Audit log saved to: {Path.GetFullPath("audit.log")}");
        Console.WriteLine("Run Complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL ERROR: {ex}");
        }
    }

    static void PrintAuditLogs(IAuditLog log, int count)
    {
        var logs = log.GetRecentLogs(count);
        foreach (var l in logs)
        {
            Console.WriteLine($"[AUDIT] {l}");
        }
    }
}
