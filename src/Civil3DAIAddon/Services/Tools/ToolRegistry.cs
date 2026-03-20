using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;

namespace Civil3DAIAddon.Services.Tools;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ICadTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterTool(ICadTool tool)
    {
        _tools[tool.Name] = tool;
    }

    public ICadTool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyList<ICadTool> GetAllTools() => _tools.Values.ToList();

    public IReadOnlyList<ICadTool> GetToolsByCategory(string category) =>
        _tools.Values.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<ToolDefinition> GetToolDefinitions() =>
        _tools.Values.Select(t => t.GetDefinition()).ToList();

    public bool HasTool(string name) => _tools.ContainsKey(name);
}
