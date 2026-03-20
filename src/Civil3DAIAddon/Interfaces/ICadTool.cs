using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Interfaces;

public interface ICadTool
{
    string Name { get; }
    string Description { get; }
    string Category { get; }
    SafetyLevel SafetyLevel { get; }
    bool RequiresConfirmation { get; }
    bool SupportsUndo { get; }

    ToolDefinition GetDefinition();
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters);
    ValidationResult ValidateParameters(Dictionary<string, object> parameters);
}

public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ValidationResult Ok() => new() { IsValid = true };

    public static ValidationResult Invalid(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}
