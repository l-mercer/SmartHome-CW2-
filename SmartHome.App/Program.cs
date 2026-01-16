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

            Console.WriteLine("--- SCENARIO 1: Invalid Event (Validation at Boundaries) ---");
            var invalidEvent = new SensorEvent(
                EventId: Guid.NewGuid().ToString(),
                DeviceId: "Dev-1",
                Type: SensorType.DoorContact,
                Value: 5, 
                Timestamp: DateTimeOffset.UtcNow,
                Signature: "bad_sig" 
            );
            Console.WriteLine($"Input: DoorContact with Value=5 and invalid signature");
            await refactoredLogic.ProcessEventAsync(invalidEvent);
            PrintAuditLogs(auditLog, 1);
            Console.WriteLine();

            Console.WriteLine("--- SCENARIO 2: Deduplication (Idempotency) ---");
            var dupId = Guid.NewGuid().ToString();
            var eventA = CreateEvent("Motion", 1, dupId);
            var eventB = CreateEvent("Motion", 1, dupId); 

            Console.WriteLine("Sending 1st event...");
            await refactoredLogic.ProcessEventAsync(eventA);
            Console.WriteLine("Sending 2nd event (duplicate)...");
            await refactoredLogic.ProcessEventAsync(eventB);
            PrintAuditLogs(auditLog, 1); 
            Console.WriteLine();

            Console.WriteLine("--- BAD VS REFACTORED LOGIC DEMO ---");
            
            Console.WriteLine("\n[Run: BAD LOGIC]");
            badLogic.ProcessEvent(CreateEvent("Motion", 1)); 
            
            Console.WriteLine("\n[Run: REFACTORED LOGIC]");
            var motionEvent = CreateEvent("Motion", 1);
            Console.WriteLine("Step A: Single Motion Event (Suspected)");
            await refactoredLogic.ProcessEventAsync(motionEvent);
            
            var doorEvent = CreateEvent("DoorContact", 1);
            Console.WriteLine("Step B: Door Event within 10s (Confirmed)");
            await refactoredLogic.ProcessEventAsync(doorEvent);
            PrintAuditLogs(auditLog, 3); 
            Console.WriteLine();

            Console.WriteLine("--- SCENARIO 5: Reliability - Notification Fallback ---");
            
            var fireEvent = CreateEvent("Smoke", 100); 
            Console.WriteLine("Input: High Confidence Smoke Event");
            await refactoredLogic.ProcessEventAsync(fireEvent);
            
            PrintAuditLogs(auditLog, 5); 
            Console.WriteLine();

            Console.WriteLine("--- SCENARIO 6: Incident State Machine Integrity ---");
            try 
            {
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
