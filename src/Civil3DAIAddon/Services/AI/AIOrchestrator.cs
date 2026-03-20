using System.Diagnostics;
using Newtonsoft.Json;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Drawing;

namespace Civil3DAIAddon.Services.AI;

public sealed class AIOrchestrator : IAIOrchestrator
{
    private readonly IOpenAIClient _aiClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly IDrawingContextExtractor _contextExtractor;
    private readonly ISafetyValidator _safetyValidator;
    private readonly IActionLogger _logger;
    private readonly IConfigurationService _configService;

    private readonly List<ConversationMessage> _conversationHistory = new();

    public event Action<string>? OnStreamingToken;
    public event Action<string>? OnStatusUpdate;
    public event Func<AIPlan, SafetyValidationResult, Task<bool>>? OnConfirmationRequired;

    public AIOrchestrator(
        IOpenAIClient aiClient,
        IToolRegistry toolRegistry,
        IDrawingContextExtractor contextExtractor,
        ISafetyValidator safetyValidator,
        IActionLogger logger,
        IConfigurationService configService)
    {
        _aiClient = aiClient;
        _toolRegistry = toolRegistry;
        _contextExtractor = contextExtractor;
        _safetyValidator = safetyValidator;
        _logger = logger;
        _configService = configService;
    }

    public async Task<ExecutionReport> ProcessPromptAsync(
        string userPrompt, ContextScope scope, ExecutionMode mode, CancellationToken ct = default)
    {
        var report = new ExecutionReport
        {
            UserPrompt = userPrompt,
            Mode = mode
        };

        var sw = Stopwatch.StartNew();

        try
        {
            // Step 1: Classify intent (fast, small model)
            OnStatusUpdate?.Invoke("Classifying intent...");
            var intentCategory = await _aiClient.ClassifyIntentAsync(userPrompt, ct);
            _logger.LogToolExecution(report.RequestId, "IntentClassifier", userPrompt, intentCategory);

            // Step 2: Extract drawing context
            OnStatusUpdate?.Invoke("Extracting drawing context...");
            var snapshot = _contextExtractor.ExtractSnapshot(scope);

            // Step 3: Build AI request with conversation history
            var request = new AIRequest
            {
                UserPrompt = userPrompt,
                DrawingSnapshot = snapshot,
                AvailableTools = _toolRegistry.GetToolDefinitions().ToList(),
                Mode = mode,
                ConversationHistory = _conversationHistory.TakeLast(10).ToList()
            };

            // Step 4: Generate plan from AI
            OnStatusUpdate?.Invoke("Generating execution plan...");
            var aiResponse = await GeneratePlanAsync(request, ct);

            if (!aiResponse.IsSuccess)
            {
                report.StepResults.Add(new StepExecutionResult
                {
                    StepNumber = 0,
                    ToolName = "AI",
                    Description = "Plan generation",
                    Success = false,
                    ErrorMessage = aiResponse.ErrorMessage
                });
                return report;
            }

            var plan = aiResponse.Plan;
            if (plan == null)
            {
                report.StepResults.Add(new StepExecutionResult
                {
                    StepNumber = 0,
                    ToolName = "AI",
                    Description = "Plan generation",
                    Success = false,
                    ErrorMessage = "AI returned success but plan was null."
                });
                return report;
            }
            report.Plan = plan;
            _logger.LogPlan(report.RequestId, JsonConvert.SerializeObject(plan, Formatting.Indented));

            // Update conversation history
            _conversationHistory.Add(new ConversationMessage { Role = "user", Content = userPrompt });
            _conversationHistory.Add(new ConversationMessage { Role = "assistant", Content = aiResponse.RawResponse ?? "" });

            // Step 5: Handle clarification
            if (aiResponse.NeedsClarification)
            {
                report.StepResults.Add(new StepExecutionResult
                {
                    StepNumber = 0,
                    ToolName = "AI",
                    Description = "Clarification needed",
                    Success = true,
                    ErrorMessage = plan.ClarificationNeeded
                });
                return report;
            }

            // Step 6: Validate plan safety
            OnStatusUpdate?.Invoke("Validating plan safety...");
            var safetyResult = _safetyValidator.ValidatePlan(plan);
            if (!safetyResult.IsApproved)
            {
                report.StepResults.Add(new StepExecutionResult
                {
                    StepNumber = 0,
                    ToolName = "SafetyValidator",
                    Description = "Safety validation failed",
                    Success = false,
                    ErrorMessage = string.Join("; ", safetyResult.Violations)
                });
                return report;
            }

            // Step 6b: Pre-execution validation - verify all required tools exist
            OnStatusUpdate?.Invoke("Pre-execution validation...");
            foreach (var requiredTool in plan.RequiredTools)
            {
                if (!_toolRegistry.HasTool(requiredTool))
                {
                    report.StepResults.Add(new StepExecutionResult
                    {
                        StepNumber = 0,
                        ToolName = "PreValidator",
                        Description = "Pre-execution validation failed",
                        Success = false,
                        ErrorMessage = $"Required tool '{requiredTool}' is not available in the tool registry."
                    });
                    return report;
                }
            }

            // Step 6c: Confirmation workflow
            var config = _configService.Load();
            if (_safetyValidator.RequiresConfirmation(plan, config.ConfirmationPolicy) && mode == ExecutionMode.Execute)
            {
                if (OnConfirmationRequired != null)
                {
                    var confirmed = await OnConfirmationRequired.Invoke(plan, safetyResult);
                    if (!confirmed)
                    {
                        report.StepResults.Add(new StepExecutionResult
                        {
                            StepNumber = 0,
                            ToolName = "Confirmation",
                            Description = "User declined execution",
                            Success = false,
                            ErrorMessage = "Execution cancelled by user during confirmation."
                        });
                        return report;
                    }
                }
            }

            // Step 7: Execute plan
            var executionReport = await ExecutePlanAsync(plan, mode, ct);
            report.StepResults = executionReport.StepResults;
            report.WasRolledBack = executionReport.WasRolledBack;

            // Step 8: Post-execution validation
            if (mode == ExecutionMode.Execute && !executionReport.WasRolledBack)
            {
                OnStatusUpdate?.Invoke("Post-execution validation...");
                var postValidation = ValidatePostExecution(plan, executionReport);
                if (!string.IsNullOrEmpty(postValidation))
                {
                    report.StepResults.Add(new StepExecutionResult
                    {
                        StepNumber = plan.OrderedSteps.Count + 1,
                        ToolName = "PostValidator",
                        Description = "Post-execution validation",
                        Success = true,
                        ErrorMessage = postValidation
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(report.RequestId, "ProcessPrompt", ex);
            report.StepResults.Add(new StepExecutionResult
            {
                StepNumber = 0,
                ToolName = "Orchestrator",
                Description = "Unexpected error",
                Success = false,
                ErrorMessage = ex.Message
            });
        }
        finally
        {
            sw.Stop();
            report.TotalDuration = sw.Elapsed;
            _logger.LogReport(report);
            OnStatusUpdate?.Invoke(report.OverallSuccess ? "Completed" : "Completed with errors");
        }

        return report;
    }

    public async Task<AIResponse> GeneratePlanAsync(AIRequest request, CancellationToken ct = default)
    {
        if (!_aiClient.IsConfigured)
        {
            return new AIResponse { ErrorMessage = "OpenAI API key not configured. Open Settings to configure." };
        }

        // Stream tokens for UI feedback
        var fullResponse = new System.Text.StringBuilder();
        await foreach (var token in _aiClient.StreamResponseAsync(request, ct))
        {
            fullResponse.Append(token);
            OnStreamingToken?.Invoke(token);
        }

        var responseText = fullResponse.ToString();

        try
        {
            var plan = JsonConvert.DeserializeObject<AIPlan>(responseText);
            return new AIResponse
            {
                Plan = plan,
                RawResponse = responseText,
                ModelUsed = _configService.Load().PrimaryModel
            };
        }
        catch (JsonException ex)
        {
            return new AIResponse
            {
                ErrorMessage = $"Failed to parse plan JSON: {ex.Message}",
                RawResponse = responseText
            };
        }
    }

    public async Task<ExecutionReport> ExecutePlanAsync(
        AIPlan plan, ExecutionMode mode, CancellationToken ct = default)
    {
        var report = new ExecutionReport
        {
            Plan = plan,
            Mode = mode
        };

        var sw = Stopwatch.StartNew();

        // Start undo scope
        var undoTool = _toolRegistry.GetTool("StartUndoScope");
        if (undoTool != null && mode == ExecutionMode.Execute)
        {
            await undoTool.ExecuteAsync(new Dictionary<string, object>
            {
                ["scope_name"] = $"AI: {plan.Intent}"
            });
        }

        bool anyFailed = false;

        foreach (var step in plan.OrderedSteps.OrderBy(s => s.StepNumber))
        {
            ct.ThrowIfCancellationRequested();

            OnStatusUpdate?.Invoke($"Step {step.StepNumber}: {step.Description}");

            var stepResult = new StepExecutionResult
            {
                StepNumber = step.StepNumber,
                ToolName = step.ToolName,
                Description = step.Description
            };

            var stepSw = Stopwatch.StartNew();

            try
            {
                var tool = _toolRegistry.GetTool(step.ToolName);
                if (tool == null)
                {
                    stepResult.Success = false;
                    stepResult.ErrorMessage = $"Tool '{step.ToolName}' not found in registry.";
                    anyFailed = true;
                    report.StepResults.Add(stepResult);
                    continue;
                }

                // Validate step safety
                var stepSafety = _safetyValidator.ValidateStep(step, tool);
                if (!stepSafety.IsApproved)
                {
                    stepResult.Success = false;
                    stepResult.ErrorMessage = $"Safety: {string.Join("; ", stepSafety.Violations)}";
                    anyFailed = true;
                    report.StepResults.Add(stepResult);
                    continue;
                }

                // Validate parameters
                var validation = tool.ValidateParameters(step.Parameters);
                if (!validation.IsValid)
                {
                    stepResult.Success = false;
                    stepResult.ErrorMessage = $"Validation: {string.Join("; ", validation.Errors)}";
                    anyFailed = true;
                    report.StepResults.Add(stepResult);
                    continue;
                }

                if (mode == ExecutionMode.DryRun)
                {
                    // Dry run - just validate, don't execute
                    stepResult.Success = true;
                    stepResult.ErrorMessage = "[DRY RUN] Would execute: " + step.Description;
                    _logger.LogToolExecution(report.RequestId, step.ToolName,
                        JsonConvert.SerializeObject(step.Parameters), "[DRY RUN]");
                }
                else
                {
                    // Execute
                    var result = await tool.ExecuteAsync(step.Parameters);
                    stepResult.Success = result.Success;
                    stepResult.Result = result;
                    stepResult.ErrorMessage = result.Success ? null : result.Message;

                    _logger.LogToolExecution(report.RequestId, step.ToolName,
                        JsonConvert.SerializeObject(step.Parameters),
                        JsonConvert.SerializeObject(result));

                    if (!result.Success)
                        anyFailed = true;
                }
            }
            catch (Exception ex)
            {
                stepResult.Success = false;
                stepResult.ErrorMessage = ex.Message;
                anyFailed = true;
                _logger.LogError(report.RequestId, $"Step {step.StepNumber}: {step.ToolName}", ex);
            }
            finally
            {
                stepSw.Stop();
                stepResult.Duration = stepSw.Elapsed;
                report.StepResults.Add(stepResult);
            }

            // If a step fails and it's critical, stop execution
            if (anyFailed && plan.SafetyLevel >= SafetyLevel.Destructive)
            {
                OnStatusUpdate?.Invoke("Critical step failed, halting execution.");
                break;
            }
        }

        // End undo scope
        var commitTool = _toolRegistry.GetTool("CommitTransaction");
        if (commitTool != null && mode == ExecutionMode.Execute)
        {
            await commitTool.ExecuteAsync(new Dictionary<string, object>());
        }

        sw.Stop();
        report.TotalDuration = sw.Elapsed;

        return report;
    }

    private string? ValidatePostExecution(AIPlan plan, ExecutionReport report)
    {
        var issues = new List<string>();

        // Check if all steps succeeded
        var failedSteps = report.StepResults.Where(s => !s.Success).ToList();
        if (failedSteps.Count > 0)
        {
            issues.Add($"{failedSteps.Count} step(s) failed: {string.Join(", ", failedSteps.Select(s => s.ToolName))}");
        }

        // Verify created/modified object counts match expectations
        var totalCreated = report.StepResults
            .Where(s => s.Result != null)
            .Sum(s => s.Result!.CreatedHandles.Count);
        var totalModified = report.StepResults
            .Where(s => s.Result != null)
            .Sum(s => s.Result!.ModifiedHandles.Count);
        var totalDeleted = report.StepResults
            .Where(s => s.Result != null)
            .Sum(s => s.Result!.DeletedHandles.Count);

        // Run validation rules from plan
        foreach (var rule in plan.ValidationRules)
        {
            if (!string.IsNullOrEmpty(rule.CheckTool) && _toolRegistry.HasTool(rule.CheckTool))
            {
                // Could run the check tool here for advanced validation
                issues.Add($"Validation rule '{rule.Description}' requires manual check with tool '{rule.CheckTool}'.");
            }
        }

        if (issues.Count == 0)
        {
            return $"Post-validation OK. Created: {totalCreated}, Modified: {totalModified}, Deleted: {totalDeleted}.";
        }

        return $"Post-validation notes: {string.Join("; ", issues)}. Created: {totalCreated}, Modified: {totalModified}, Deleted: {totalDeleted}.";
    }
}
