using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Services.Tools.AutoCAD;
using Civil3DAIAddon.Services.Tools.Civil3D;

namespace Civil3DAIAddon.Services.Tools;

public sealed class ToolRegistrationService
{
    private readonly IToolRegistry _registry;
    private readonly IDrawingContextExtractor _contextExtractor;
    private readonly IActionLogger _logger;

    public ToolRegistrationService(
        IToolRegistry registry,
        IDrawingContextExtractor contextExtractor,
        IActionLogger logger)
    {
        _registry = registry;
        _contextExtractor = contextExtractor;
        _logger = logger;
    }

    public void RegisterAllTools()
    {
        // Context / Query tools
        _registry.RegisterTool(new GetActiveDocumentContextTool(_contextExtractor));
        _registry.RegisterTool(new GetCurrentSelectionTool(_contextExtractor));
        _registry.RegisterTool(new GetVisibleEntitiesTool(_contextExtractor));
        _registry.RegisterTool(new QueryEntitiesByTypeTool(_contextExtractor));
        _registry.RegisterTool(new QueryEntitiesByLayerTool(_contextExtractor));
        _registry.RegisterTool(new QueryCivilObjectsTool(_contextExtractor));

        // AutoCAD creation tools
        _registry.RegisterTool(new CreateLineTool());
        _registry.RegisterTool(new CreatePolylineTool());
        _registry.RegisterTool(new CreateArcTool());
        _registry.RegisterTool(new CreateCircleTool());
        _registry.RegisterTool(new CreateTextTool());
        _registry.RegisterTool(new CreateMTextTool());
        _registry.RegisterTool(new CreateBlockReferenceTool());

        // AutoCAD modification tools
        _registry.RegisterTool(new MoveEntityTool());
        _registry.RegisterTool(new CopyEntityTool());
        _registry.RegisterTool(new RotateEntityTool());
        _registry.RegisterTool(new EraseEntityTool());
        _registry.RegisterTool(new ChangeLayerTool());
        _registry.RegisterTool(new SetPropertiesTool());
        _registry.RegisterTool(new ZoomToObjectsTool());

        // Transaction tools
        _registry.RegisterTool(new StartUndoScopeTool());
        _registry.RegisterTool(new CommitTransactionTool());
        _registry.RegisterTool(new RollbackTransactionTool());

        // Civil 3D tools
        _registry.RegisterTool(new CreateAlignmentFromPolylineTool());
        _registry.RegisterTool(new CreateProfileTool());
        _registry.RegisterTool(new CreateFeatureLineTool());
        _registry.RegisterTool(new CreateSurfaceTinTool());
        _registry.RegisterTool(new AddLabelsToAlignmentTool());
        _registry.RegisterTool(new AddLabelsToProfileTool());
        _registry.RegisterTool(new QueryAlignmentGeometryTool());
        _registry.RegisterTool(new QuerySurfaceInfoTool());
        _registry.RegisterTool(new QueryProfileInfoTool());
        _registry.RegisterTool(new QueryParcelInfoTool());
        _registry.RegisterTool(new QueryPointGroupsTool());
        _registry.RegisterTool(new CreateOffsetAlignmentTool());
        _registry.RegisterTool(new ExtractStationingDataTool());
        _registry.RegisterTool(new AnalyzeGeometryContinuityTool());

        _logger.LogUserInput($"[SYSTEM] Registered {_registry.GetAllTools().Count} tools.");
    }
}
