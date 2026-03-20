using Civil3DAIAddon.Models.AI;
using Newtonsoft.Json;
using Xunit;
using FluentAssertions;

namespace Civil3DAIAddon.Tests.Fixtures;

/// <summary>
/// End-to-end scenario tests verifying plan parsing for realistic prompts.
/// These validate that the expected plan structure for common operations
/// can be deserialized and validated correctly.
/// </summary>
public class SampleScenarios
{
    [Fact]
    public void Scenario_DrawPolylineAxis_ShouldParse()
    {
        var json = """
        {
            "intent": "Draw an axis as a polyline through specified points",
            "assumptions": ["Points will be provided by user or picked interactively", "Model space coordinate system"],
            "target_objects": [],
            "ordered_steps": [
                {
                    "step_number": 1,
                    "tool_name": "CreatePolyline",
                    "parameters": {
                        "points": [[0, 0], [50, 10], [100, 0], [150, -10], [200, 0]],
                        "closed": false,
                        "layer": "C-ROAD-CNTR"
                    },
                    "description": "Create axis polyline through 5 waypoints",
                    "depends_on": []
                }
            ],
            "required_tools": ["CreatePolyline"],
            "safety_level": "Moderate",
            "confirmation_required": false,
            "validation_rules": [{"type": "count", "description": "Verify polyline has 5 vertices"}],
            "expected_result": "Polyline axis created through 5 points on layer C-ROAD-CNTR"
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);
        plan.Should().NotBeNull();
        plan!.OrderedSteps.Should().HaveCount(1);
        plan.OrderedSteps[0].ToolName.Should().Be("CreatePolyline");
    }

    [Fact]
    public void Scenario_CreateAlignmentAndLabel_ShouldParse()
    {
        var json = """
        {
            "intent": "Create alignment from polyline and add labels",
            "assumptions": ["Polyline handle 3FA exists", "Default alignment style available"],
            "target_objects": [{"handle": "3FA", "type": "Polyline", "layer": "0"}],
            "ordered_steps": [
                {
                    "step_number": 1,
                    "tool_name": "StartUndoScope",
                    "parameters": {"scope_name": "Create alignment with labels"},
                    "description": "Begin undo group",
                    "depends_on": []
                },
                {
                    "step_number": 2,
                    "tool_name": "CreateAlignmentFromPolyline",
                    "parameters": {"polyline_handle": "3FA", "name": "Main Road Axis"},
                    "description": "Convert polyline to alignment",
                    "depends_on": [1]
                },
                {
                    "step_number": 3,
                    "tool_name": "AddLabelsToAlignment",
                    "parameters": {"alignment_handle": "RESULT_HANDLE", "label_type": "station", "interval": 20},
                    "description": "Add station labels every 20m",
                    "depends_on": [2]
                },
                {
                    "step_number": 4,
                    "tool_name": "CommitTransaction",
                    "parameters": {},
                    "description": "Commit undo scope",
                    "depends_on": [3]
                }
            ],
            "required_tools": ["StartUndoScope", "CreateAlignmentFromPolyline", "AddLabelsToAlignment", "CommitTransaction"],
            "safety_level": "Moderate",
            "confirmation_required": true,
            "validation_rules": [],
            "expected_result": "Alignment created from polyline with station labels every 20m"
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);
        plan.Should().NotBeNull();
        plan!.OrderedSteps.Should().HaveCount(4);
        plan.ConfirmationRequired.Should().BeTrue();
    }

    [Fact]
    public void Scenario_MoveEntitiesBetweenLayers_ShouldParse()
    {
        var json = """
        {
            "intent": "Move objects from layer TEMP to layer FINAL",
            "assumptions": ["Both layers exist"],
            "target_objects": [],
            "ordered_steps": [
                {
                    "step_number": 1,
                    "tool_name": "QueryEntitiesByLayer",
                    "parameters": {"layer_name": "TEMP"},
                    "description": "Find all entities on layer TEMP",
                    "depends_on": []
                },
                {
                    "step_number": 2,
                    "tool_name": "ChangeLayer",
                    "parameters": {"handles": "ALL_FROM_STEP_1", "target_layer": "FINAL"},
                    "description": "Move all found entities to FINAL layer",
                    "depends_on": [1]
                }
            ],
            "required_tools": ["QueryEntitiesByLayer", "ChangeLayer"],
            "safety_level": "Moderate",
            "confirmation_required": true,
            "validation_rules": [{"type": "count", "description": "Verify no entities remain on TEMP layer"}],
            "expected_result": "All entities moved from TEMP to FINAL layer"
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);
        plan.Should().NotBeNull();
        plan!.OrderedSteps.Should().HaveCount(2);
        plan.OrderedSteps[1].DependsOn.Should().Contain(1);
    }

    [Fact]
    public void Scenario_CreateTINSurface_ShouldParse()
    {
        var json = """
        {
            "intent": "Create a TIN surface from a point group",
            "assumptions": ["Point group 'Survey Points' exists"],
            "target_objects": [],
            "ordered_steps": [
                {
                    "step_number": 1,
                    "tool_name": "QueryPointGroups",
                    "parameters": {},
                    "description": "List available point groups",
                    "depends_on": []
                },
                {
                    "step_number": 2,
                    "tool_name": "CreateSurfaceTin",
                    "parameters": {"name": "Existing Ground", "point_group_name": "Survey Points"},
                    "description": "Create TIN surface from point group",
                    "depends_on": [1]
                }
            ],
            "required_tools": ["QueryPointGroups", "CreateSurfaceTin"],
            "safety_level": "Moderate",
            "confirmation_required": true,
            "validation_rules": [{"type": "surface", "description": "Verify surface has valid triangulation"}],
            "expected_result": "TIN surface 'Existing Ground' created from Survey Points group"
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);
        plan.Should().NotBeNull();
        plan!.OrderedSteps[1].Parameters["name"].Should().NotBeNull();
    }

    [Fact]
    public void Scenario_AnalyzeGeometry_ShouldParse()
    {
        var json = """
        {
            "intent": "Analyze geometry continuity of alignment",
            "assumptions": ["Alignment handle 5BC exists"],
            "target_objects": [{"handle": "5BC", "type": "Alignment"}],
            "ordered_steps": [
                {
                    "step_number": 1,
                    "tool_name": "AnalyzeGeometryContinuity",
                    "parameters": {"alignment_handle": "5BC"},
                    "description": "Check for tangent breaks and radius discontinuities",
                    "depends_on": []
                }
            ],
            "required_tools": ["AnalyzeGeometryContinuity"],
            "safety_level": "Safe",
            "confirmation_required": false,
            "validation_rules": [],
            "expected_result": "Geometry continuity report for the alignment"
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);
        plan.Should().NotBeNull();
        plan!.SafetyLevel.Should().Be(SafetyLevel.Safe);
        plan.ConfirmationRequired.Should().BeFalse();
    }

    [Fact]
    public void Scenario_ErrorRollback_EmptySteps_ShouldParse()
    {
        var json = """
        {
            "intent": "Unknown operation",
            "assumptions": [],
            "target_objects": [],
            "ordered_steps": [],
            "required_tools": [],
            "safety_level": "Safe",
            "confirmation_required": false,
            "validation_rules": [],
            "expected_result": "Cannot perform this operation: the requested Civil 3D operation is not available through the current tool set.",
            "clarification_needed": null
        }
        """;

        var plan = JsonConvert.DeserializeObject<AIPlan>(json);
        plan.Should().NotBeNull();
        plan!.OrderedSteps.Should().BeEmpty();
        plan.ExpectedResult.Should().Contain("not available");
    }
}
