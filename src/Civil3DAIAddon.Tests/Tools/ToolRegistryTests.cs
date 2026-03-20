using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;
using Civil3DAIAddon.Services.Tools;
using Newtonsoft.Json.Linq;
using Xunit;
using FluentAssertions;

namespace Civil3DAIAddon.Tests.Tools;

public class ToolRegistryTests
{
    private readonly ToolRegistry _registry = new();

    [Fact]
    public void RegisterAndGetTool_ShouldWork()
    {
        var tool = new FakeTool("TestTool", "Test", "TestCategory");
        _registry.RegisterTool(tool);

        var retrieved = _registry.GetTool("TestTool");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("TestTool");
    }

    [Fact]
    public void GetTool_CaseInsensitive_ShouldWork()
    {
        _registry.RegisterTool(new FakeTool("MyTool", "Desc", "Cat"));

        _registry.GetTool("mytool").Should().NotBeNull();
        _registry.GetTool("MYTOOL").Should().NotBeNull();
        _registry.GetTool("MyTool").Should().NotBeNull();
    }

    [Fact]
    public void GetTool_NonExistent_ShouldReturnNull()
    {
        _registry.GetTool("NonExistent").Should().BeNull();
    }

    [Fact]
    public void HasTool_ShouldReturnCorrectValue()
    {
        _registry.RegisterTool(new FakeTool("ExistingTool", "Desc", "Cat"));

        _registry.HasTool("ExistingTool").Should().BeTrue();
        _registry.HasTool("MissingTool").Should().BeFalse();
    }

    [Fact]
    public void GetAllTools_ShouldReturnAll()
    {
        _registry.RegisterTool(new FakeTool("Tool1", "Desc1", "Cat1"));
        _registry.RegisterTool(new FakeTool("Tool2", "Desc2", "Cat2"));
        _registry.RegisterTool(new FakeTool("Tool3", "Desc3", "Cat1"));

        var all = _registry.GetAllTools();
        all.Should().HaveCount(3);
    }

    [Fact]
    public void GetToolsByCategory_ShouldFilter()
    {
        _registry.RegisterTool(new FakeTool("Tool1", "Desc1", "Create"));
        _registry.RegisterTool(new FakeTool("Tool2", "Desc2", "Query"));
        _registry.RegisterTool(new FakeTool("Tool3", "Desc3", "Create"));

        var createTools = _registry.GetToolsByCategory("Create");
        createTools.Should().HaveCount(2);
        createTools.Should().OnlyContain(t => t.Category == "Create");
    }

    [Fact]
    public void GetToolDefinitions_ShouldReturnDefinitionsForAll()
    {
        _registry.RegisterTool(new FakeTool("T1", "D1", "C1"));
        _registry.RegisterTool(new FakeTool("T2", "D2", "C2"));

        var defs = _registry.GetToolDefinitions();
        defs.Should().HaveCount(2);
        defs.Should().Contain(d => d.Name == "T1");
        defs.Should().Contain(d => d.Name == "T2");
    }

    [Fact]
    public void RegisterTool_SameNameOverwrites()
    {
        _registry.RegisterTool(new FakeTool("Dup", "First", "Cat"));
        _registry.RegisterTool(new FakeTool("Dup", "Second", "Cat"));

        var tool = _registry.GetTool("Dup");
        tool.Should().NotBeNull();
        tool!.Description.Should().Be("Second");
    }

    private sealed class FakeTool : ICadTool
    {
        public FakeTool(string name, string description, string category)
        {
            Name = name;
            Description = description;
            Category = category;
        }

        public string Name { get; }
        public string Description { get; }
        public string Category { get; }
        public SafetyLevel SafetyLevel => SafetyLevel.Safe;
        public bool RequiresConfirmation => false;
        public bool SupportsUndo => true;

        public ToolDefinition GetDefinition() => new()
        {
            Name = Name,
            Description = Description,
            Category = Category,
            SafetyLevel = SafetyLevel,
            ParameterSchema = new JObject()
        };

        public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters) =>
            Task.FromResult(ToolResult.Ok(Name, "OK"));

        public ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
            ValidationResult.Ok();
    }
}
