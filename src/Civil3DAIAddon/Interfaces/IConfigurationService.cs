namespace Civil3DAIAddon.Interfaces;

public interface IConfigurationService
{
    AddonConfiguration Load();
    void Save(AddonConfiguration config);
    string GetApiKey();
    void SetApiKey(string apiKey);
}

public sealed class AddonConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = "gpt-4.1";
    public string ClassificationModel { get; set; } = "gpt-4.1-mini";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxTokens { get; set; } = 16384;
    public bool DryRunByDefault { get; set; } = true;
    public ConfirmationPolicy ConfirmationPolicy { get; set; } = ConfirmationPolicy.ConfirmDestructive;
    public int MaxContextEntities { get; set; } = 200;
    public bool IncludeViewportScreenshot { get; set; } = false;
    public string LogLevel { get; set; } = "Information";
    public string LogFilePath { get; set; } = string.Empty;
}
