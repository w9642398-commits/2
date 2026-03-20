using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Services.Safety;
using Xunit;
using FluentAssertions;

namespace Civil3DAIAddon.Tests.Services;

public class SafetyValidatorTests
{
    private readonly SafetyValidator _validator = new();

    [Fact]
    public void ValidatePlan_SafePlan_ShouldApprove()
    {
        var plan = new AIPlan
        {
            Intent = "Draw a line",
            SafetyLevel = SafetyLevel.Safe,
            RequiredTools = new List<string> { "CreateLine" },
            OrderedSteps = new List<PlannedStep>
            {
                new()
                {
                    StepNumber = 1,
                    ToolName = "CreateLine",
                    Parameters = new Dictionary<string, object>
                    {
                        ["start_point"] = new[] { 0.0, 0.0 },
                        ["end_point"] = new[] { 100.0, 0.0 }
                    },
                    Description = "Draw a horizontal line"
                }
            }
        };

        var result = _validator.ValidatePlan(plan);

        result.IsApproved.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePlan_DestructivePlan_ShouldWarn()
    {
        var plan = new AIPlan
        {
            Intent = "Delete an entity",
            SafetyLevel = SafetyLevel.Destructive,
            RequiredTools = new List<string> { "EraseEntity" },
            OrderedSteps = new List<PlannedStep>
            {
                new()
                {
                    StepNumber = 1,
                    ToolName = "EraseEntity",
                    Parameters = new Dictionary<string, object> { ["handle"] = "ABC123" },
                    Description = "Erase entity"
                }
            }
        };

        var result = _validator.ValidatePlan(plan);

        result.IsApproved.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("destructive"));
    }

    [Fact]
    public void ValidatePlan_EmptyToolName_ShouldReject()
    {
        var plan = new AIPlan
        {
            Intent = "Bad plan",
            RequiredTools = new List<string> { "" },
            OrderedSteps = new List<PlannedStep>
            {
                new()
                {
                    StepNumber = 1,
                    ToolName = "",
                    Parameters = new(),
                    Description = "Invalid step"
                }
            }
        };

        var result = _validator.ValidatePlan(plan);

        result.IsApproved.Should().BeFalse();
        result.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidatePlan_XrefModification_ShouldReject()
    {
        var plan = new AIPlan
        {
            Intent = "Modify XREF",
            RequiredTools = new List<string> { "ChangeLayer" },
            OrderedSteps = new List<PlannedStep>
            {
                new()
                {
                    StepNumber = 1,
                    ToolName = "ChangeLayer",
                    Parameters = new Dictionary<string, object>
                    {
                        ["handles"] = "xref|entity",
                        ["target_layer"] = "NEW_LAYER"
                    },
                    Description = "Change XREF layer"
                }
            }
        };

        var result = _validator.ValidatePlan(plan);

        result.IsApproved.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("XREF"));
    }

    [Fact]
    public void ValidatePlan_InvalidDependency_ShouldReject()
    {
        var plan = new AIPlan
        {
            Intent = "Bad dependency",
            RequiredTools = new List<string> { "CreateLine" },
            OrderedSteps = new List<PlannedStep>
            {
                new()
                {
                    StepNumber = 1,
                    ToolName = "CreateLine",
                    Parameters = new(),
                    DependsOn = new List<int> { 99 },
                    Description = "Depends on non-existent step"
                }
            }
        };

        var result = _validator.ValidatePlan(plan);

        result.IsApproved.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("non-existent step 99"));
    }

    [Fact]
    public void IsDestructiveOperation_EraseEntity_ShouldReturnTrue()
    {
        _validator.IsDestructiveOperation("EraseEntity").Should().BeTrue();
    }

    [Fact]
    public void IsDestructiveOperation_CreateLine_ShouldReturnFalse()
    {
        _validator.IsDestructiveOperation("CreateLine").Should().BeFalse();
    }

    [Fact]
    public void RequiresConfirmation_AlwaysConfirmPolicy_ShouldReturnTrue()
    {
        var plan = new AIPlan { SafetyLevel = SafetyLevel.Safe };
        _validator.RequiresConfirmation(plan, ConfirmationPolicy.AlwaysConfirm).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_AutoExecutePolicy_ShouldReturnFalse()
    {
        var plan = new AIPlan { SafetyLevel = SafetyLevel.Safe };
        _validator.RequiresConfirmation(plan, ConfirmationPolicy.AutoExecute).Should().BeFalse();
    }

    [Fact]
    public void RequiresConfirmation_ConfirmDestructivePolicy_DestructivePlan_ShouldReturnTrue()
    {
        var plan = new AIPlan { SafetyLevel = SafetyLevel.Destructive };
        _validator.RequiresConfirmation(plan, ConfirmationPolicy.ConfirmDestructive).Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_ConfirmDestructivePolicy_SafePlan_ShouldReturnFalse()
    {
        var plan = new AIPlan { SafetyLevel = SafetyLevel.Safe };
        _validator.RequiresConfirmation(plan, ConfirmationPolicy.ConfirmDestructive).Should().BeFalse();
    }
}
