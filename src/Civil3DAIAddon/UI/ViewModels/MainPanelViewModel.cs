using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Drawing;
using Civil3DAIAddon.Services.AI;

namespace Civil3DAIAddon.UI.ViewModels;

public sealed class MainPanelViewModel : ViewModelBase
{
    private readonly IAIOrchestrator _orchestrator;
    private readonly IDrawingContextExtractor _contextExtractor;
    private readonly IActionLogger _logger;
    private readonly IConfigurationService _configService;

    private string _promptText = string.Empty;
    private string _statusText = "Ready";
    private bool _isBusy;
    private ExecutionMode _currentMode = ExecutionMode.DryRun;
    private ContextScope _currentScope = ContextScope.Selection;
    private AIPlan? _currentPlan;
    private ExecutionReport? _lastReport;
    private string _streamingOutput = string.Empty;
    private string _activePanel = "Chat";
    private string _drawingSummary = string.Empty;
    private string _selectionSummary = string.Empty;
    private string _safetyWarnings = string.Empty;

    public MainPanelViewModel(
        IAIOrchestrator orchestrator,
        IDrawingContextExtractor contextExtractor,
        IActionLogger logger,
        IConfigurationService configService)
    {
        _orchestrator = orchestrator;
        _contextExtractor = contextExtractor;
        _logger = logger;
        _configService = configService;

        var config = _configService.Load();
        _currentMode = config.DryRunByDefault ? ExecutionMode.DryRun : ExecutionMode.Execute;

        _orchestrator.OnStreamingToken += token =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                StreamingOutput += token;
            });
        };

        _orchestrator.OnStatusUpdate += status =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusText = status;
            });
        };

        SendCommand = new AsyncRelayCommand(ExecutePromptAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(PromptText));
        CancelCommand = new RelayCommand(CancelExecution, () => IsBusy);
        ConfirmPlanCommand = new AsyncRelayCommand(ConfirmPlanAsync, () => CurrentPlan != null && !IsBusy);
        RollbackCommand = new AsyncRelayCommand(RollbackAsync, () => LastReport != null && !IsBusy);
        ClearChatCommand = new RelayCommand(ClearChat);
        RefreshContextCommand = new RelayCommand(RefreshContext);

        // Wire up confirmation workflow
        if (_orchestrator is Services.AI.AIOrchestrator concreteOrchestrator)
        {
            concreteOrchestrator.OnConfirmationRequired += async (plan, safety) =>
            {
                var tcs = new TaskCompletionSource<bool>();
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var warnings = safety.Warnings.Count > 0
                        ? $"\nWarnings:\n- {string.Join("\n- ", safety.Warnings)}"
                        : "";
                    var msg = $"Plan: {plan.Intent}\n" +
                              $"Safety Level: {plan.SafetyLevel}\n" +
                              $"Steps: {plan.OrderedSteps.Count}{warnings}\n\n" +
                              "Do you want to proceed?";

                    SafetyWarnings = warnings;

                    var result = System.Windows.MessageBox.Show(
                        msg, "Confirm Execution",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    tcs.SetResult(result == System.Windows.MessageBoxResult.Yes);
                });
                return await tcs.Task;
            };
        }

        RefreshContext();
    }

    public string PromptText
    {
        get => _promptText;
        set => SetProperty(ref _promptText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ExecutionMode CurrentMode
    {
        get => _currentMode;
        set => SetProperty(ref _currentMode, value);
    }

    public ContextScope CurrentScope
    {
        get => _currentScope;
        set => SetProperty(ref _currentScope, value);
    }

    public AIPlan? CurrentPlan
    {
        get => _currentPlan;
        set => SetProperty(ref _currentPlan, value);
    }

    public ExecutionReport? LastReport
    {
        get => _lastReport;
        set => SetProperty(ref _lastReport, value);
    }

    public string StreamingOutput
    {
        get => _streamingOutput;
        set => SetProperty(ref _streamingOutput, value);
    }

    public string ActivePanel
    {
        get => _activePanel;
        set => SetProperty(ref _activePanel, value);
    }

    public string DrawingSummary
    {
        get => _drawingSummary;
        set => SetProperty(ref _drawingSummary, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        set => SetProperty(ref _selectionSummary, value);
    }

    public string SafetyWarnings
    {
        get => _safetyWarnings;
        set => SetProperty(ref _safetyWarnings, value);
    }

    public ObservableCollection<ConversationMessage> ConversationHistory { get; } = new();
    public ObservableCollection<StepExecutionResult> ExecutionSteps { get; } = new();
    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    public ObservableCollection<string> ModifiedObjects { get; } = new();

    public ICommand SendCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmPlanCommand { get; }
    public ICommand RollbackCommand { get; }
    public ICommand ClearChatCommand { get; }
    public ICommand RefreshContextCommand { get; }

    private CancellationTokenSource? _cts;

    private async Task ExecutePromptAsync()
    {
        if (string.IsNullOrWhiteSpace(PromptText)) return;

        IsBusy = true;
        StreamingOutput = string.Empty;
        StatusText = "Processing prompt...";
        _cts = new CancellationTokenSource();

        var userMessage = new ConversationMessage
        {
            Role = "user",
            Content = PromptText,
            Timestamp = DateTime.UtcNow
        };
        ConversationHistory.Add(userMessage);
        _logger.LogUserInput(PromptText);

        var prompt = PromptText;
        PromptText = string.Empty;

        try
        {
            var report = await _orchestrator.ProcessPromptAsync(
                prompt, CurrentScope, CurrentMode, _cts.Token);

            LastReport = report;
            CurrentPlan = report.Plan;

            ConversationHistory.Add(new ConversationMessage
            {
                Role = "assistant",
                Content = report.Summary,
                Timestamp = DateTime.UtcNow
            });

            ExecutionSteps.Clear();
            foreach (var step in report.StepResults)
                ExecutionSteps.Add(step);

            ModifiedObjects.Clear();
            foreach (var step in report.StepResults)
            {
                if (step.Result != null)
                {
                    foreach (var h in step.Result.CreatedHandles)
                        ModifiedObjects.Add($"[Created] {h}");
                    foreach (var h in step.Result.ModifiedHandles)
                        ModifiedObjects.Add($"[Modified] {h}");
                    foreach (var h in step.Result.DeletedHandles)
                        ModifiedObjects.Add($"[Deleted] {h}");
                }
            }

            StatusText = report.OverallSuccess ? "Completed successfully" : "Completed with errors";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled by user";
            ConversationHistory.Add(new ConversationMessage
            {
                Role = "system",
                Content = "Operation cancelled.",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            ConversationHistory.Add(new ConversationMessage
            {
                Role = "system",
                Content = $"Error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelExecution()
    {
        _cts?.Cancel();
    }

    private async Task ConfirmPlanAsync()
    {
        if (CurrentPlan == null) return;

        IsBusy = true;
        StatusText = "Executing confirmed plan...";

        try
        {
            var report = await _orchestrator.ExecutePlanAsync(
                CurrentPlan, ExecutionMode.Execute);

            LastReport = report;
            StatusText = report.OverallSuccess ? "Plan executed successfully" : "Plan executed with errors";
        }
        catch (Exception ex)
        {
            StatusText = $"Execution error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RollbackAsync()
    {
        try
        {
            var report = await _orchestrator.ExecutePlanAsync(
                new AIPlan
                {
                    Intent = "Rollback",
                    OrderedSteps = new List<PlannedStep>
                    {
                        new() { StepNumber = 1, ToolName = "RollbackTransaction", Parameters = new(), Description = "Undo last operation" }
                    },
                    RequiredTools = new List<string> { "RollbackTransaction" },
                    SafetyLevel = SafetyLevel.Destructive,
                },
                ExecutionMode.Execute);

            StatusText = "Rollback executed. You can also use Ctrl+Z for additional undo.";
        }
        catch (Exception ex)
        {
            StatusText = $"Rollback failed: {ex.Message}. Use Ctrl+Z in Civil 3D.";
        }
    }

    private void ClearChat()
    {
        ConversationHistory.Clear();
        ExecutionSteps.Clear();
        ModifiedObjects.Clear();
        StreamingOutput = string.Empty;
        CurrentPlan = null;
        LastReport = null;
        StatusText = "Ready";
    }

    private void RefreshContext()
    {
        try
        {
            var snapshot = _contextExtractor.ExtractSnapshot(CurrentScope);
            DrawingSummary = $"File: {snapshot.FileName}\n" +
                             $"Units: {snapshot.Units}\n" +
                             $"Layers: {snapshot.Layers.Count}\n" +
                             $"Total entities: {snapshot.TotalEntityCount}\n" +
                             $"Civil objects: {snapshot.CivilObjects.Count}";

            var selected = snapshot.SelectedEntities;
            SelectionSummary = selected.Count > 0
                ? $"Selected: {selected.Count} entities\n" +
                  string.Join("\n", selected.Take(10).Select(e => $"  {e.Type} [{e.Handle}] on {e.Layer}"))
                  + (selected.Count > 10 ? $"\n  ...and {selected.Count - 10} more" : "")
                : "No selection";
        }
        catch
        {
            DrawingSummary = "No active document";
            SelectionSummary = "N/A";
        }
    }

    public void ShowHistoryPanel() => ActivePanel = "History";
    public void ShowLogsPanel()
    {
        ActivePanel = "Logs";
        LogEntries.Clear();
        foreach (var entry in _logger.GetRecentEntries(200))
            LogEntries.Add(entry);
    }
}
