using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Services.AI;
using Xunit;
using FluentAssertions;

namespace Civil3DAIAddon.Tests.AI;

public class SystemPromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_ShouldContainDrawingContext()
    {
        var snapshot = new DrawingSnapshot
        {
            FileName = "test.dwg",
            Units = "Meters",
            TotalEntityCount = 42
        };

        var tools = new List<ToolDefinition>
        {
            new() { Name = "CreateLine", Description = "Creates a line", Category = "Create" }
        };

        var prompt = SystemPromptBuilder.BuildSystemPrompt(snapshot, tools);

        prompt.Should().Contain("test.dwg");
        prompt.Should().Contain("Meters");
        prompt.Should().Contain("CreateLine");
        prompt.Should().Contain("Safety Rules");
        prompt.Should().Contain("Response Schema");
    }

    [Fact]
    public void BuildSystemPrompt_ShouldContainAllTools()
    {
        var snapshot = new DrawingSnapshot();
        var tools = new List<ToolDefinition>
        {
            new() { Name = "CreateLine", Description = "Creates a line", Category = "Create" },
            new() { Name = "CreateCircle", Description = "Creates a circle", Category = "Create" },
            new() { Name = "EraseEntity", Description = "Erases an entity", Category = "Modify" },
        };

        var prompt = SystemPromptBuilder.BuildSystemPrompt(snapshot, tools);

        prompt.Should().Contain("CreateLine");
        prompt.Should().Contain("CreateCircle");
        prompt.Should().Contain("EraseEntity");
    }

    [Fact]
    public void BuildSystemPrompt_ShouldContainSafetyRules()
    {
        var prompt = SystemPromptBuilder.BuildSystemPrompt(new DrawingSnapshot(), new List<ToolDefinition>());

        prompt.Should().Contain("Never delete or erase");
        prompt.Should().Contain("XREF");
        prompt.Should().Contain("locked layers");
        prompt.Should().Contain("fabricate or guess");
    }

    [Fact]
    public void BuildClassificationPrompt_ShouldContainCategories()
    {
        var prompt = SystemPromptBuilder.BuildClassificationPrompt();

        prompt.Should().Contain("DRAW");
        prompt.Should().Contain("CIVIL");
        prompt.Should().Contain("MODIFY");
        prompt.Should().Contain("QUERY");
        prompt.Should().Contain("WORKFLOW");
        prompt.Should().Contain("AMBIGUOUS");
    }
}
