using System.Windows;
using System.Windows.Controls;
using Civil3DAIAddon.Bootstrap;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Civil3DAIAddon.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();

        var configService = AddonInitializer.Services.GetRequiredService<IConfigurationService>();
        _viewModel = new SettingsViewModel(configService);
        DataContext = _viewModel;

        // PasswordBox doesn't support binding — sync manually
        Loaded += OnLoaded;
        ApiKeyBox.PasswordChanged += OnApiKeyPasswordChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApiKeyBox.Password = _viewModel.ApiKey;
    }

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ApiKey = ApiKeyBox.Password;
    }
}
