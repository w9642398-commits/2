using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Civil3DAIAddon.Interfaces;

namespace Civil3DAIAddon.Services.Configuration;

public sealed class ConfigurationService : IConfigurationService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Civil3DAIAddon");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "settings.json");
    private static readonly string KeyFilePath = Path.Combine(ConfigDirectory, ".apikey");

    private AddonConfiguration? _cached;

    public AddonConfiguration Load()
    {
        if (_cached != null)
            return _cached;

        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                _cached = JsonConvert.DeserializeObject<AddonConfiguration>(json) ?? new AddonConfiguration();
            }
            else
            {
                _cached = new AddonConfiguration();
            }
        }
        catch
        {
            _cached = new AddonConfiguration();
        }

        _cached.ApiKey = GetApiKey();
        return _cached;
    }

    public void Save(AddonConfiguration config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            // Don't persist API key in main config
            var toSave = new AddonConfiguration
            {
                PrimaryModel = config.PrimaryModel,
                ClassificationModel = config.ClassificationModel,
                TimeoutSeconds = config.TimeoutSeconds,
                MaxTokens = config.MaxTokens,
                DryRunByDefault = config.DryRunByDefault,
                ConfirmationPolicy = config.ConfirmationPolicy,
                MaxContextEntities = config.MaxContextEntities,
                IncludeViewportScreenshot = config.IncludeViewportScreenshot,
                LogLevel = config.LogLevel,
                LogFilePath = config.LogFilePath,
            };

            var json = JsonConvert.SerializeObject(toSave, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
            _cached = config;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
        }
    }

    public string GetApiKey()
    {
        try
        {
            if (!File.Exists(KeyFilePath))
                return string.Empty;

            var encrypted = File.ReadAllBytes(KeyFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    public void SetApiKey(string apiKey)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            var bytes = Encoding.UTF8.GetBytes(apiKey);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFilePath, encrypted);

            if (_cached != null)
                _cached.ApiKey = apiKey;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save API key: {ex.Message}");
        }
    }
}
