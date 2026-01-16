using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;

namespace SmartHome.Core.Services;

public class PushProvider : INotificationProvider
{
    public string ChannelName => "Push";

    public Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        // Always succeeds
        return Task.FromResult(new NotificationResult(true, ChannelName, "Push sent successfully", TimeSpan.Zero));
    }
}

public class SmsProvider : INotificationProvider
{
    public string ChannelName => "SMS";
    private int _attemptCount = 0;

    public async Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        _attemptCount++;
        // Simulate flaky failure: fails on first 2 calls in a sequence (or random).
        // For consistent "Scenario 5" demo, let's make it fail if the message contains "FailSMS".
        // Or just fail randomly. The prompt says "SMS provider fails => fallback".
        // Let's make it fail always for now or based on a static flag I can toggle? 
        // Or just fail the first time it is called for an incident?
        
        // Let's simulate a delay then fail
        await Task.Delay(100, cancellationToken);
        
        // Fail by default for demo purposes when we want to show fallback
        return new NotificationResult(false, ChannelName, "SMS Gateway Timeout", TimeSpan.FromMilliseconds(100));
    }
}

public class EmailProvider : INotificationProvider
{
    public string ChannelName => "Email";

    public async Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);
        return new NotificationResult(true, ChannelName, "Email sent successfully", TimeSpan.FromMilliseconds(50));
    }
}

