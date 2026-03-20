using Civil3DAIAddon.Models.AI;
using Newtonsoft.Json;
using Xunit;
using FluentAssertions;

namespace Civil3DAIAddon.Tests.AI;

public class AIPlanParserTests
{
    private const string ValidPlanJson = """
    {
        "intent": "Create a polyline through specified points",
        "assumptions": ["Points are in model space", "Using current UCS"],
        "target_objects": [],
        "ordered_steps": [
            {
                "step_number": 1,
                "tool_name": "CreatePolyline",
                "parameters": {
                    "points": [[0, 0], [100, 0], [100, 100], [0, 100]],
                    "closed": true,
                    "layer": "0"
                },
                "description": "Create a closed rectangle polyline",
                "depends_on": [],
                "rollback_tool": "EraseEntity"
            }
        ],
        "required_tools": ["CreatePolyline"],
        "safety_level": "Moderate",
        "confirmation_required": false,
        "validation_rules": [
            {
                "type": "geometry",
                "description": "Verify polyline has 4 vertices",
                "check_tool": null
            }
        ],
        "expected_result": "A closed rectangular polyline with 4 vertices",
        "clarification_needed": null
    }
    """;

    [Fact]
    public void ParseValidPlan_ShouldDeserializeCorrectly()
    {
        var plan = JsonConvert.DeserializeObject<AIPlan>(ValidPlanJson);

        plan.Should().NotBeNull();
        plan!.Intent.Should().Be("Create a polyline through specified points");
        plan.Assumptions.Should().HaveCount(2);
        plan.OrderedSteps.Should().HaveCount(1);
        plan.OrderedSteps[0].ToolName.Should().Be("CreatePolyline");
        plan.OrderedSteps[0].StepNumber.Should().Be(1);
        plan.SafetyLevel.Should().Be(SafetyLevel.Moderate);
        plan.ConfirmationRequired.Should().BeFalse();
        plan.ClarificationNeeded.Should().BeNull();
        plan.RequiredTools.Should().Contain("CreatePolyline");
        plan.ValidationRules.Should().HaveCount(1);
    }

    [Fact]
    public void ParsePlanWithClarification_ShouldIndicateNeedsClarification()
    {
        var json = """
        {
            "intent": "Unclear",
            "assumptions": [],
            "target_objects": [],
            "ordered_steps": [],
            "required_tools": [],
            "safety_level": "Safe",
            "confirmation_required": false,
            "validation_rules": [],
            "expected_result": "",
            "clarification_needed": "Which polyline do you want to convert to an alignment? There are 3 polylines in the drawing."
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);

        plan.Should().NotBeNull();
        plan!.ClarificationNeeded.Should().NotBeNullOrEmpty();
        plan.OrderedSteps.Should().BeEmpty();
    }

    [Fact]
    public void ParsePlanWithMultipleSteps_ShouldPreserveDependencies()
    {
        var json = """
        {
            "intent": "Create alignment from polyline and add labels",
            "assumptions": [],
            "target_objects": [{"handle": "1A2B", "type": "Polyline", "layer": "0"}],
            "ordered_steps": [
                {
                    "step_number": 1,
                    "tool_name": "CreateAlignmentFromPolyline",
                    "parameters": {"polyline_handle": "1A2B", "name": "Main Road"},
                    "description": "Create alignment from polyline",
                    "depends_on": []
                },
                {
                    "step_number": 2,
                    "tool_name": "AddLabelsToAlignment",
                    "parameters": {"alignment_handle": "RESULT_FROM_STEP_1", "label_type": "station", "interval": 20},
                    "description": "Add station labels",
                    "depends_on": [1]
                }
            ],
            "required_tools": ["CreateAlignmentFromPolyline", "AddLabelsToAlignment"],
            "safety_level": "Moderate",
            "confirmation_required": true,
            "validation_rules": [],
            "expected_result": "Alignment created with station labels every 20m"
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);

        plan.Should().NotBeNull();
        plan!.OrderedSteps.Should().HaveCount(2);
        plan.OrderedSteps[1].DependsOn.Should().Contain(1);
        plan.TargetObjects.Should().HaveCount(1);
        plan.TargetObjects[0].Handle.Should().Be("1A2B");
    }

    [Fact]
    public void ParseDestructivePlan_ShouldSetCorrectSafetyLevel()
    {
        var json = """
        {
            "intent": "Delete all entities on layer TEMP",
            "assumptions": ["Layer TEMP exists"],
            "target_objects": [],
            "ordered_steps": [
                {
                    "step_number": 1,
                    "tool_name": "QueryEntitiesByLayer",
                    "parameters": {"layer_name": "TEMP"},
                    "description": "Find entities on TEMP layer",
                    "depends_on": []
                },
                {
                    "step_number": 2,
                    "tool_name": "EraseEntity",
                    "parameters": {"handle": "ALL_FROM_STEP_1"},
                    "description": "Erase all found entities",
                    "depends_on": [1]
                }
            ],
            "required_tools": ["QueryEntitiesByLayer", "EraseEntity"],
            "safety_level": "Destructive",
            "confirmation_required": true,
            "validation_rules": [],
            "expected_result": "All entities on TEMP layer deleted"
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);

        plan.Should().NotBeNull();
        plan!.SafetyLevel.Should().Be(SafetyLevel.Destructive);
        plan.ConfirmationRequired.Should().BeTrue();
    }

    [Fact]
    public void SerializePlan_ShouldRoundTrip()
    {
        var plan = new AIPlan
        {
            Intent = "Test roundtrip",
            SafetyLevel = SafetyLevel.Safe,
            OrderedSteps = new List<PlannedStep>
            {
                new()
                {
                    StepNumber = 1,
                    ToolName = "CreateLine",
                    Parameters = new Dictionary<string, object>
                    {
                        ["start_point"] = new[] { 0.0, 0.0 },
                        ["end_point"] = new[] { 100.0, 100.0 }
                    },
                    Description = "Draw a diagonal line"
                }
            }
        };

        var json = JsonConvert.SerializeObject(plan, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<AIPlan>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Intent.Should().Be(plan.Intent);
        deserialized.OrderedSteps.Should().HaveCount(1);
        deserialized.OrderedSteps[0].ToolName.Should().Be("CreateLine");
    }

    [Fact]
    public void ParseInvalidJson_ShouldThrow()
    {
        var invalidJson = "{ this is not valid json }";

        Action act = () => JsonConvert.DeserializeObject<AIPlan>(invalidJson);
        act.Should().Throw<JsonException>();
    }
}
