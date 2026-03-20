using Civil3DAIAddon.Models.AI;

namespace Civil3DAIAddon.Interfaces;

public interface IOpenAIClient
{
    Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamResponseAsync(AIRequest request, CancellationToken ct = default);
    Task<string> ClassifyIntentAsync(string userPrompt, CancellationToken ct = default);
    bool IsConfigured { get; }
}
