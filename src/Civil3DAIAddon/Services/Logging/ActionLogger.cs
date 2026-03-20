using System.Collections.Concurrent;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.Drawing;

namespace Civil3DAIAddon.Services.Logging;

public sealed class ActionLogger : IActionLogger
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly IConfigurationService _configService;
    private const int MaxEntries = 5000;

    public ActionLogger(IConfigurationService configService)
    {
        _configService = configService;
    }

    public void LogUserInput(string prompt)
    {
        AddEntry(new LogEntry
        {
            Level = "Info",
            Category = "UserInput",
            Message = prompt
        });
    }

    public void LogPlan(string requestId, string planJson)
    {
        AddEntry(new LogEntry
        {
            Level = "Info",
            RequestId = requestId,
            Category = "Plan",
            Message = "AI plan generated",
            Detail = planJson
        });
    }

    public void LogToolExecution(string requestId, string toolName, string parameters, string result)
    {
        AddEntry(new LogEntry
        {
            Level = "Info",
            RequestId = requestId,
            Category = "ToolExecution",
            Message = $"Tool: {toolName}",
            Detail = $"Params: {parameters}\nResult: {result}"
        });
    }

    public void LogError(string requestId, string context, Exception ex)
    {
        AddEntry(new LogEntry
        {
            Level = "Error",
            RequestId = requestId,
            Category = "Error",
            Message = $"{context}: {ex.Message}",
            Detail = ex.StackTrace
        });
    }

    public void LogReport(ExecutionReport report)
    {
        AddEntry(new LogEntry
        {
            Level = report.OverallSuccess ? "Info" : "Warning",
            RequestId = report.RequestId,
            Category = "Report",
            Message = report.Summary,
        });

        WriteToFileIfConfigured(report);
    }

    public IReadOnlyList<LogEntry> GetRecentEntries(int count = 100)
    {
        return _entries.TakeLast(count).ToList();
    }

    public IReadOnlyList<LogEntry> GetEntriesByRequest(string requestId)
    {
        return _entries.Where(e => e.RequestId == requestId).ToList();
    }

    private void AddEntry(LogEntry entry)
    {
        _entries.Enqueue(entry);

        // Trim if too many
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);
    }

    private void WriteToFileIfConfigured(ExecutionReport report)
    {
        try
        {
            var config = _configService.Load();
            if (string.IsNullOrEmpty(config.LogFilePath)) return;

            var dir = Path.GetDirectoryName(config.LogFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var logLine = $"[{report.Timestamp:yyyy-MM-dd HH:mm:ss}] [{report.RequestId}] {report.Summary}\n";
            File.AppendAllText(config.LogFilePath, logLine);
        }
        catch
        {
            // Silent fail for file logging
        }
    }
}
