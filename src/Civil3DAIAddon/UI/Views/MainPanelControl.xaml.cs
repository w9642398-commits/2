using System.Windows.Controls;
using Civil3DAIAddon.Bootstrap;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Civil3DAIAddon.UI.Views;

public partial class MainPanelControl : UserControl
{
    public MainPanelControl()
    {
        InitializeComponent();

        var orchestrator = AddonInitializer.Services.GetRequiredService<IAIOrchestrator>();
        var extractor = AddonInitializer.Services.GetRequiredService<IDrawingContextExtractor>();
        var logger = AddonInitializer.Services.GetRequiredService<IActionLogger>();
        var config = AddonInitializer.Services.GetRequiredService<IConfigurationService>();

        DataContext = new MainPanelViewModel(orchestrator, extractor, logger, config);
    }

    public MainPanelViewModel? ViewModel => DataContext as MainPanelViewModel;
}
