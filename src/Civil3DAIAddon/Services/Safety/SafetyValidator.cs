using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;

namespace Civil3DAIAddon.Services.Safety;

public sealed class SafetyValidator : ISafetyValidator
{
    private static readonly HashSet<string> DestructiveTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "EraseEntity",
        "RollbackTransaction",
        "DeleteLayer"
    };

    private static readonly HashSet<string> XrefRelatedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "xref", "external reference", "attached", "overlay"
    };

    public SafetyValidationResult ValidatePlan(AIPlan plan)
    {
        var warnings = new List<string>();
        var violations = new List<string>();

        // Check for unknown tools
        foreach (var toolName in plan.RequiredTools)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                violations.Add("Plan contains an empty tool name.");
        }

        // Check steps
        foreach (var step in plan.OrderedSteps)
        {
            if (string.IsNullOrWhiteSpace(step.ToolName))
            {
                violations.Add($"Step {step.StepNumber} has no tool_name.");
                continue;
            }

            // Check for destructive operations
            if (DestructiveTools.Contains(step.ToolName))
            {
                warnings.Add($"Step {step.StepNumber}: '{step.ToolName}' is a destructive operation.");
            }

            // Check for XREF modifications
            if (step.Parameters.Any(p =>
                    p.Value?.ToString()?.IndexOf("xref", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                violations.Add($"Step {step.StepNumber}: Detected potential XREF modification. This is not allowed without explicit handling.");
            }

            // Validate dependencies
            foreach (var dep in step.DependsOn)
            {
                if (!plan.OrderedSteps.Any(s => s.StepNumber == dep))
                    violations.Add($"Step {step.StepNumber} depends on non-existent step {dep}.");
            }
        }

        // Check for locked layer modifications
        foreach (var target in plan.TargetObjects)
        {
            if (target.Description?.Contains("locked", StringComparison.OrdinalIgnoreCase) == true)
            {
                warnings.Add($"Target object on potentially locked layer: {target.Layer}");
            }
        }

        // Safety level escalation
        if (plan.SafetyLevel == SafetyLevel.Critical)
        {
            warnings.Add("Plan is marked as CRITICAL safety level. Extra caution required.");
        }

        if (violations.Count > 0)
        {
            return new SafetyValidationResult
            {
                IsApproved = false,
                Violations = violations,
                Warnings = warnings,
                Summary = $"Plan rejected: {violations.Count} violation(s) found."
            };
        }

        return new SafetyValidationResult
        {
            IsApproved = true,
            Warnings = warnings,
            RequiresUserConfirmation = plan.ConfirmationRequired || warnings.Count > 0,
            Summary = warnings.Count > 0
                ? $"Plan approved with {warnings.Count} warning(s)."
                : "Plan approved."
        };
    }

    public SafetyValidationResult ValidateStep(PlannedStep step, ICadTool tool)
    {
        var warnings = new List<string>();

        // Validate tool safety level
        if (tool.SafetyLevel >= SafetyLevel.Destructive)
        {
            warnings.Add($"Tool '{tool.Name}' is destructive (safety level: {tool.SafetyLevel}).");
        }

        // Validate parameter presence
        var validation = tool.ValidateParameters(step.Parameters);
        if (!validation.IsValid)
        {
            return SafetyValidationResult.Rejected(validation.Errors.ToArray());
        }

        return SafetyValidationResult.Approved(warnings.ToArray());
    }

    public bool IsDestructiveOperation(string toolName)
    {
        return DestructiveTools.Contains(toolName);
    }

    public bool RequiresConfirmation(AIPlan plan, ConfirmationPolicy policy)
    {
        return policy switch
        {
            ConfirmationPolicy.AlwaysConfirm => true,
            ConfirmationPolicy.AutoExecute => false,
            ConfirmationPolicy.ConfirmDestructive =>
                plan.SafetyLevel >= SafetyLevel.Destructive ||
                plan.OrderedSteps.Any(s => DestructiveTools.Contains(s.ToolName)),
            _ => true
        };
    }
}
