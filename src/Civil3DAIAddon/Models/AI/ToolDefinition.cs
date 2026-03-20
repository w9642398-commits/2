using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Civil3DAIAddon.Models.AI;

public sealed class ToolDefinition
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("parameters")]
    public JObject ParameterSchema { get; set; } = new();

    [JsonProperty("returns")]
    public string Returns { get; set; } = string.Empty;

    [JsonProperty("safety_level")]
    public SafetyLevel SafetyLevel { get; set; } = SafetyLevel.Safe;

    [JsonProperty("requires_confirmation")]
    public bool RequiresConfirmation { get; set; }

    [JsonProperty("supports_undo")]
    public bool SupportsUndo { get; set; } = true;
}
