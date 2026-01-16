namespace SmartHome.Core.Interfaces;

public interface IDeduplicationStore
{
    bool IsDuplicate(string eventId);
    void MarkProcessed(string eventId);
}

