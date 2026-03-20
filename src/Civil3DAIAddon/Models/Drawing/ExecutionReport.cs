using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Models.Drawing;

public sealed class ExecutionReport
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserPrompt { get; set; } = string.Empty;
    public AIPlan? Plan { get; set; }
    public ExecutionMode Mode { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
    public bool OverallSuccess => StepResults.Count > 0 && StepResults.All(s => s.Success);
    public bool WasRolledBack { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public string Summary => BuildSummary();

    private string BuildSummary()
    {
        var succeeded = StepResults.Count(s => s.Success);
        var failed = StepResults.Count(s => !s.Success);
        var status = WasRolledBack ? "ROLLED BACK" : (OverallSuccess ? "SUCCESS" : "PARTIAL FAILURE");
        return $"[{status}] {succeeded}/{StepResults.Count} steps completed, {failed} failed. Duration: {TotalDuration.TotalMilliseconds:F0}ms";
    }
}

public sealed class StepExecutionResult
{
    public int StepNumber { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Success { get; set; }
    public ToolResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}
