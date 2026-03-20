using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAddon.Ribbon;
using Civil3DAIAddon.Services.Configuration;
using Civil3DAIAddon.Services.Logging;
using Civil3DAIAddon.Services.AI;
using Civil3DAIAddon.Services.Drawing;
using Civil3DAIAddon.Services.Safety;
using Civil3DAIAddon.Services.Tools;
using Civil3DAIAddon.Interfaces;
using Microsoft.Extensions.DependencyInjection;

[assembly: ExtensionApplication(typeof(Civil3DAIAddon.Bootstrap.AddonInitializer))]
[assembly: CommandClass(typeof(Civil3DAIAddon.Bootstrap.AddonCommands))]

namespace Civil3DAIAddon.Bootstrap;

public sealed class AddonInitializer : IExtensionApplication
{
    private static ServiceProvider? _serviceProvider;

    public static IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("Addon not initialized");

    public void Initialize()
    {
        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            Application.Idle += OnApplicationIdle;

            var logger = _serviceProvider.GetRequiredService<IActionLogger>();
            logger.LogUserInput("[SYSTEM] Civil3D AI Addon initialized successfully.");
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor
                .WriteMessage($"\n[Civil3D AI Addon] Initialization error: {ex.Message}");
        }
    }

    public void Terminate()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IActionLogger, ActionLogger>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<ISafetyValidator, SafetyValidator>();
        services.AddSingleton<IDrawingContextExtractor, DrawingContextExtractor>();
        services.AddSingleton<IOpenAIClient, OpenAIClient>();
        services.AddSingleton<IAIOrchestrator, AIOrchestrator>();

        services.AddSingleton<RibbonBuilder>();
        services.AddSingleton<ToolRegistrationService>();
    }

    private void OnApplicationIdle(object? sender, EventArgs e)
    {
        Application.Idle -= OnApplicationIdle;

        try
        {
            // Register tools
            var toolRegistrar = _serviceProvider!.GetRequiredService<ToolRegistrationService>();
            toolRegistrar.RegisterAllTools();

            // Build ribbon
            var ribbonBuilder = _serviceProvider!.GetRequiredService<RibbonBuilder>();
            ribbonBuilder.BuildRibbon();
        }
        catch (System.Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor
                .WriteMessage($"\n[Civil3D AI Addon] Ribbon/Tools setup error: {ex.Message}");
        }
    }
}
