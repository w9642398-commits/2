using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Drawing;

namespace Civil3DAIAddon.Interfaces;

public interface IAIOrchestrator
{
    Task<AIResponse> GeneratePlanAsync(AIRequest request, CancellationToken ct = default);
    Task<ExecutionReport> ExecutePlanAsync(AIPlan plan, ExecutionMode mode, CancellationToken ct = default);
    Task<ExecutionReport> ProcessPromptAsync(string userPrompt, ContextScope scope, ExecutionMode mode, CancellationToken ct = default);
    event Action<string>? OnStreamingToken;
    event Action<string>? OnStatusUpdate;
}
