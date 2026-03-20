using System.Windows;
using Civil3DAIAddon.Bootstrap;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Civil3DAIAddon.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        var configService = AddonInitializer.Services.GetRequiredService<IConfigurationService>();
        DataContext = new SettingsViewModel(configService);
    }
}
