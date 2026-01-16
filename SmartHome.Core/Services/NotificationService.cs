using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;
using System.Diagnostics;

namespace SmartHome.Core.Services;

public class NotificationService
{
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly IAuditLog _auditLog;

    public NotificationService(IEnumerable<INotificationProvider> providers, IAuditLog auditLog)
    {
        _providers = providers;
        _auditLog = auditLog;
    }

    public async Task<NotificationResult> NotifyAsync(Incident incident)
    {
        var message = new NotificationMessage(incident.IncidentId, $"Alert: {incident.Type} detected!", incident.Type);
        
        // Define priority order: SMS -> Push -> Email
        // Note: In real DI, we might injected them in order or select by name.
        // Here we'll manually order them based on names we know.
        var orderedProviders = new List<string> { "SMS", "Push", "Email" };
        
        var stopwatch = Stopwatch.StartNew();

        foreach (var channelName in orderedProviders)
        {
            var provider = _providers.FirstOrDefault(p => p.ChannelName == channelName);
            if (provider == null) continue;

            _auditLog.Append("NotificationAttempt", $"Attempting to send via {channelName}", incident.IncidentId);

            try
            {
                // Timeout per provider (e.g. 1s)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                
                // Bounded retries (e.g. 2 attempts total)
                int retries = 2;
                NotificationResult? result = null;

                for (int i = 0; i < retries; i++)
                {
                    try 
                    {
                        result = await provider.SendAsync(message, cts.Token);
                        if (result.Success) break;
                        _auditLog.Append("NotificationRetry", $"Retry {i+1} for {channelName} failed", incident.IncidentId);
                    }
                    catch (Exception ex)
                    {
                         _auditLog.Append("NotificationError", $"Exception in {channelName}: {ex.Message}", incident.IncidentId);
                    }
                }

                if (result != null && result.Success)
                {
                    _auditLog.Append("NotificationSuccess", $"Sent via {channelName}", incident.IncidentId);
                    return result; // Stop after first success
                }
                
                _auditLog.Append("NotificationFallback", $"{channelName} failed, falling back...", incident.IncidentId);
            }
            catch (OperationCanceledException)
            {
                _auditLog.Append("NotificationTimeout", $"{channelName} timed out", incident.IncidentId);
            }
        }

        stopwatch.Stop();
        _auditLog.Append("NotificationFailed", "All channels failed", incident.IncidentId);
        return new NotificationResult(false, "All", "All providers failed", stopwatch.Elapsed);
    }
}

