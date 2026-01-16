using SmartHome.Core.Interfaces;

namespace SmartHome.Core.Services;

public class AuditLog : IAuditLog
{
    private readonly List<string> _memoryLog = new();
    private readonly string _filePath = "audit.log";
    private readonly object _lock = new();

    public void Append(string action, string details, string? correlationId = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{action}] {details} (CorrId: {correlationId ?? "N/A"})";

        lock (_lock)
        {
            _memoryLog.Add(logEntry);
            
            try
            {
                File.AppendAllText(_filePath, logEntry + Environment.NewLine);
            }
            catch
            {
            }
        }
    }

    public List<string> GetRecentLogs(int count)
    {
        lock (_lock)
        {
            return _memoryLog.TakeLast(count).ToList();
        }
    }
}
