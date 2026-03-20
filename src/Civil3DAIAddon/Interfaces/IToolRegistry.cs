using Civil3DAIAddon.Models.AI;

namespace Civil3DAIAddon.Interfaces;

public interface IToolRegistry
{
    void RegisterTool(ICadTool tool);
    ICadTool? GetTool(string name);
    IReadOnlyList<ICadTool> GetAllTools();
    IReadOnlyList<ICadTool> GetToolsByCategory(string category);
    IReadOnlyList<ToolDefinition> GetToolDefinitions();
    bool HasTool(string name);
}
