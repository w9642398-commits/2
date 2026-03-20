using Civil3DAIAddon.Models.AI;

namespace Civil3DAIAddon.Interfaces;

public interface ISafetyValidator
{
    SafetyValidationResult ValidatePlan(AIPlan plan);
    SafetyValidationResult ValidateStep(PlannedStep step, ICadTool tool);
    bool IsDestructiveOperation(string toolName);
    bool RequiresConfirmation(AIPlan plan, ConfirmationPolicy policy);
}

public sealed class SafetyValidationResult
{
    public bool IsApproved { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Violations { get; set; } = new();
    public bool RequiresUserConfirmation { get; set; }
    public string Summary { get; set; } = string.Empty;

    public static SafetyValidationResult Approved(params string[] warnings) =>
        new() { IsApproved = true, Warnings = warnings.ToList() };

    public static SafetyValidationResult Rejected(params string[] violations) =>
        new() { IsApproved = false, Violations = violations.ToList() };
}

public enum ConfirmationPolicy
{
    AlwaysConfirm,
    ConfirmDestructive,
    AutoExecute
}
