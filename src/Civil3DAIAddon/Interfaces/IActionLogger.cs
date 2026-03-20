using Civil3DAIAddon.Models.Drawing;

namespace Civil3DAIAddon.Interfaces;

public interface IActionLogger
{
    void LogUserInput(string prompt);
    void LogPlan(string requestId, string planJson);
    void LogToolExecution(string requestId, string toolName, string parameters, string result);
    void LogError(string requestId, string context, Exception ex);
    void LogReport(ExecutionReport report);
    IReadOnlyList<LogEntry> GetRecentEntries(int count = 100);
    IReadOnlyList<LogEntry> GetEntriesByRequest(string requestId);
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info";
    public string RequestId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
