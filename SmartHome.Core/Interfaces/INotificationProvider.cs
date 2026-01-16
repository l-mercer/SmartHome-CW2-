using SmartHome.Core.Models;

namespace SmartHome.Core.Interfaces;

public interface INotificationProvider
{
    Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
    string ChannelName { get; }
}


