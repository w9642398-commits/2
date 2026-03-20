using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Civil3DAIAddon.Models.AI;

public sealed class AIPlan
{
    [JsonProperty("intent")]
    public string Intent { get; set; } = string.Empty;

    [JsonProperty("assumptions")]
    public List<string> Assumptions { get; set; } = new();

    [JsonProperty("target_objects")]
    public List<TargetObject> TargetObjects { get; set; } = new();

    [JsonProperty("ordered_steps")]
    public List<PlannedStep> OrderedSteps { get; set; } = new();

    [JsonProperty("required_tools")]
    public List<string> RequiredTools { get; set; } = new();

    [JsonProperty("safety_level")]
    [JsonConverter(typeof(StringEnumConverter))]
    public SafetyLevel SafetyLevel { get; set; } = SafetyLevel.Safe;

    [JsonProperty("confirmation_required")]
    public bool ConfirmationRequired { get; set; } = true;

    [JsonProperty("validation_rules")]
    public List<ValidationRule> ValidationRules { get; set; } = new();

    [JsonProperty("expected_result")]
    public string ExpectedResult { get; set; } = string.Empty;

    [JsonProperty("clarification_needed")]
    public string? ClarificationNeeded { get; set; }
}

public sealed class TargetObject
{
    [JsonProperty("handle")]
    public string? Handle { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("layer")]
    public string? Layer { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
}

public sealed class PlannedStep
{
    [JsonProperty("step_number")]
    public int StepNumber { get; set; }

    [JsonProperty("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    [JsonProperty("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("depends_on")]
    public List<int> DependsOn { get; set; } = new();

    [JsonProperty("rollback_tool")]
    public string? RollbackTool { get; set; }
}

public sealed class ValidationRule
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("check_tool")]
    public string? CheckTool { get; set; }
}

public enum SafetyLevel
{
    Safe,
    Moderate,
    Destructive,
    Critical
}
