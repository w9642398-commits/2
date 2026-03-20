namespace Civil3DAIAddon.Models.AI;

public sealed class AIResponse
{
    public AIPlan? Plan { get; set; }
    public string? RawResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess => Plan != null && string.IsNullOrEmpty(ErrorMessage);
    public bool NeedsClarification => !string.IsNullOrEmpty(Plan?.ClarificationNeeded);
    public int TokensUsed { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
}
