using System.Collections.Concurrent;
using SmartHome.Core.Interfaces;

namespace SmartHome.Core.Services;

public class DeduplicationStore : IDeduplicationStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _processedEvents = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public DeduplicationStore()
    {
        Task.Run(CleanupLoop);
    }

    public bool IsDuplicate(string eventId)
    {
        return _processedEvents.ContainsKey(eventId);
    }

    public void MarkProcessed(string eventId)
    {
        _processedEvents.TryAdd(eventId, DateTimeOffset.UtcNow);
    }

    private async Task CleanupLoop()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = _processedEvents
                .Where(kvp => now - kvp.Value > _ttl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _processedEvents.TryRemove(key, out _);
            }
        }
    }
}
