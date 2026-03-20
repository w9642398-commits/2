using System.Windows.Input;
using Civil3DAIAddon.Interfaces;

namespace Civil3DAIAddon.UI.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private AddonConfiguration _config;

    private string _apiKey = string.Empty;
    private string _primaryModel = string.Empty;
    private string _classificationModel = string.Empty;
    private int _timeoutSeconds;
    private int _maxTokens;
    private bool _dryRunByDefault;
    private ConfirmationPolicy _confirmationPolicy;
    private int _maxContextEntities;
    private bool _includeScreenshot;
    private string _logFilePath = string.Empty;
    private string _logLevel = "Information";

    public SettingsViewModel(IConfigurationService configService)
    {
        _configService = configService;
        _config = _configService.Load();
        LoadFromConfig();

        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(Reset);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string PrimaryModel
    {
        get => _primaryModel;
        set => SetProperty(ref _primaryModel, value);
    }

    public string ClassificationModel
    {
        get => _classificationModel;
        set => SetProperty(ref _classificationModel, value);
    }

    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetProperty(ref _timeoutSeconds, value);
    }

    public int MaxTokens
    {
        get => _maxTokens;
        set => SetProperty(ref _maxTokens, value);
    }

    public bool DryRunByDefault
    {
        get => _dryRunByDefault;
        set => SetProperty(ref _dryRunByDefault, value);
    }

    public ConfirmationPolicy ConfirmationPolicy
    {
        get => _confirmationPolicy;
        set => SetProperty(ref _confirmationPolicy, value);
    }

    public int MaxContextEntities
    {
        get => _maxContextEntities;
        set => SetProperty(ref _maxContextEntities, value);
    }

    public bool IncludeScreenshot
    {
        get => _includeScreenshot;
        set => SetProperty(ref _includeScreenshot, value);
    }

    public string LogFilePath
    {
        get => _logFilePath;
        set => SetProperty(ref _logFilePath, value);
    }

    public string LogLevel
    {
        get => _logLevel;
        set => SetProperty(ref _logLevel, value);
    }

    public string ConnectionStatus { get; private set; } = string.Empty;

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand TestConnectionCommand { get; }

    private void LoadFromConfig()
    {
        ApiKey = _config.ApiKey;
        PrimaryModel = _config.PrimaryModel;
        ClassificationModel = _config.ClassificationModel;
        TimeoutSeconds = _config.TimeoutSeconds;
        MaxTokens = _config.MaxTokens;
        DryRunByDefault = _config.DryRunByDefault;
        ConfirmationPolicy = _config.ConfirmationPolicy;
        MaxContextEntities = _config.MaxContextEntities;
        IncludeScreenshot = _config.IncludeViewportScreenshot;
        LogFilePath = _config.LogFilePath;
        LogLevel = _config.LogLevel;
    }

    private void Save()
    {
        _config.PrimaryModel = PrimaryModel;
        _config.ClassificationModel = ClassificationModel;
        _config.TimeoutSeconds = TimeoutSeconds;
        _config.MaxTokens = MaxTokens;
        _config.DryRunByDefault = DryRunByDefault;
        _config.ConfirmationPolicy = ConfirmationPolicy;
        _config.MaxContextEntities = MaxContextEntities;
        _config.IncludeViewportScreenshot = IncludeScreenshot;
        _config.LogFilePath = LogFilePath;
        _config.LogLevel = LogLevel;

        if (!string.IsNullOrWhiteSpace(ApiKey))
            _configService.SetApiKey(ApiKey);

        _configService.Save(_config);
    }

    private void Reset()
    {
        _config = new AddonConfiguration();
        LoadFromConfig();
    }

    private async Task TestConnectionAsync()
    {
        ConnectionStatus = "Testing...";
        OnPropertyChanged(nameof(ConnectionStatus));

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
            http.Timeout = TimeSpan.FromSeconds(10);

            var response = await http.GetAsync("https://api.openai.com/v1/models");
            ConnectionStatus = response.IsSuccessStatusCode
                ? "Connection successful"
                : $"Connection failed: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Connection failed: {ex.Message}";
        }

        OnPropertyChanged(nameof(ConnectionStatus));
    }
}
