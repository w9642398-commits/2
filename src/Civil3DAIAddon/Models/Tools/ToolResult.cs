namespace Civil3DAIAddon.Models.Tools;

public sealed class ToolResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> OutputData { get; set; } = new();
    public List<string> CreatedHandles { get; set; } = new();
    public List<string> ModifiedHandles { get; set; } = new();
    public List<string> DeletedHandles { get; set; } = new();
    public string? ErrorDetail { get; set; }
    public TimeSpan ExecutionTime { get; set; }

    public static ToolResult Ok(string toolName, string message, Dictionary<string, object>? data = null)
    {
        return new ToolResult
        {
            Success = true,
            ToolName = toolName,
            Message = message,
            OutputData = data ?? new()
        };
    }

    public static ToolResult Fail(string toolName, string error, string? detail = null)
    {
        return new ToolResult
        {
            Success = false,
            ToolName = toolName,
            Message = error,
            ErrorDetail = detail
        };
    }
}
