using SmartHome.Core.Interfaces;
using SmartHome.Core.Models;

namespace SmartHome.Core.Services;

public class PushProvider : INotificationProvider
{
    public string ChannelName => "Push";

    public Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
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
        await Task.Delay(100, cancellationToken);
        
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
