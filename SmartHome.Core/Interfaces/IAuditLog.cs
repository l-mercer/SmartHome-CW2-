namespace SmartHome.Core.Interfaces;

public interface IAuditLog
{
    void Append(string action, string details, string? correlationId = null);
    List<string> GetRecentLogs(int count);
}

