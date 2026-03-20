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
        // ── Context / Query tools ────────────────────────────────────────
        _registry.RegisterTool(new GetActiveDocumentContextTool(_contextExtractor));
        _registry.RegisterTool(new GetCurrentSelectionTool(_contextExtractor));
        _registry.RegisterTool(new GetVisibleEntitiesTool(_contextExtractor));
        _registry.RegisterTool(new QueryEntitiesByTypeTool(_contextExtractor));
        _registry.RegisterTool(new QueryEntitiesByLayerTool(_contextExtractor));
        _registry.RegisterTool(new QueryCivilObjectsTool(_contextExtractor));

        // ── AutoCAD creation tools ───────────────────────────────────────
        _registry.RegisterTool(new CreateLineTool());
        _registry.RegisterTool(new CreatePolylineTool());
        _registry.RegisterTool(new CreateArcTool());
        _registry.RegisterTool(new CreateCircleTool());
        _registry.RegisterTool(new CreateTextTool());
        _registry.RegisterTool(new CreateMTextTool());
        _registry.RegisterTool(new CreateBlockReferenceTool());

        // ── AutoCAD modification tools ───────────────────────────────────
        _registry.RegisterTool(new MoveEntityTool());
        _registry.RegisterTool(new CopyEntityTool());
        _registry.RegisterTool(new RotateEntityTool());
        _registry.RegisterTool(new EraseEntityTool());
        _registry.RegisterTool(new ChangeLayerTool());
        _registry.RegisterTool(new SetPropertiesTool());
        _registry.RegisterTool(new ZoomToObjectsTool());

        // ── AutoCAD advanced modification tools ──────────────────────────
        _registry.RegisterTool(new MirrorEntityTool());
        _registry.RegisterTool(new ScaleEntityTool());
        _registry.RegisterTool(new ArrayEntityTool());
        _registry.RegisterTool(new OffsetEntityTool());
        _registry.RegisterTool(new FilletEntitiesTool());
        _registry.RegisterTool(new ChamferEntitiesTool());
        _registry.RegisterTool(new TrimEntityTool());
        _registry.RegisterTool(new ExtendEntityTool());
        _registry.RegisterTool(new ExplodeEntityTool());
        _registry.RegisterTool(new MeasureDistanceTool());

        // ── AutoCAD layer management tools ───────────────────────────────
        _registry.RegisterTool(new CreateLayerTool());
        _registry.RegisterTool(new ModifyLayerTool());
        _registry.RegisterTool(new DeleteLayerTool());
        _registry.RegisterTool(new QueryLayersTool());
        _registry.RegisterTool(new SetCurrentLayerTool());

        // ── AutoCAD dimension & annotation tools ─────────────────────────
        _registry.RegisterTool(new CreateAlignedDimensionTool());
        _registry.RegisterTool(new CreateLinearDimensionTool());
        _registry.RegisterTool(new CreateRadialDimensionTool());
        _registry.RegisterTool(new CreateHatchTool());
        _registry.RegisterTool(new CreateLeaderTool());

        // ── Transaction tools ────────────────────────────────────────────
        _registry.RegisterTool(new StartUndoScopeTool());
        _registry.RegisterTool(new CommitTransactionTool());
        _registry.RegisterTool(new RollbackTransactionTool());

        // ── Civil 3D – Alignment tools ───────────────────────────────────
        _registry.RegisterTool(new CreateAlignmentFromPolylineTool());
        _registry.RegisterTool(new CreateAlignmentByLayoutTool());
        _registry.RegisterTool(new AddAlignmentTangentTool());
        _registry.RegisterTool(new AddAlignmentCurveTool());
        _registry.RegisterTool(new AddAlignmentSpiralTool());
        _registry.RegisterTool(new SetAlignmentSuperelevationTool());
        _registry.RegisterTool(new ModifyAlignmentGeometryTool());
        _registry.RegisterTool(new CreateOffsetAlignmentTool());
        _registry.RegisterTool(new QueryAlignmentGeometryTool());
        _registry.RegisterTool(new ExtractStationingDataTool());
        _registry.RegisterTool(new AnalyzeGeometryContinuityTool());
        _registry.RegisterTool(new AddLabelsToAlignmentTool());

        // ── Civil 3D – Profile tools ─────────────────────────────────────
        _registry.RegisterTool(new CreateProfileTool());
        _registry.RegisterTool(new CreateLayoutProfileTool());
        _registry.RegisterTool(new AddPVIToProfileTool());
        _registry.RegisterTool(new AddVerticalCurveTool());
        _registry.RegisterTool(new CreateProfileViewTool());
        _registry.RegisterTool(new QueryProfileInfoTool());
        _registry.RegisterTool(new QueryProfilePVIsTool());
        _registry.RegisterTool(new AddLabelsToProfileTool());

        // ── Civil 3D – Surface tools ─────────────────────────────────────
        _registry.RegisterTool(new CreateSurfaceTinTool());
        _registry.RegisterTool(new AddSurfaceBreaklinesTool());
        _registry.RegisterTool(new AddSurfaceBoundaryTool());
        _registry.RegisterTool(new CreateVolumeSurfaceTool());
        _registry.RegisterTool(new AnalyzeSurfaceSlopeTool());
        _registry.RegisterTool(new GetSurfaceElevationAtPointTool());
        _registry.RegisterTool(new AddPointsToSurfaceTool());
        _registry.RegisterTool(new PasteSurfaceTool());
        _registry.RegisterTool(new ExtractSurfaceContoursTool());
        _registry.RegisterTool(new QuerySurfaceInfoTool());

        // ── Civil 3D – Corridor tools ────────────────────────────────────
        _registry.RegisterTool(new CreateAssemblyTool());
        _registry.RegisterTool(new AddSubassemblyTool());
        _registry.RegisterTool(new CreateCorridorTool());
        _registry.RegisterTool(new AddCorridorBaselineTool());
        _registry.RegisterTool(new SetCorridorFrequencyTool());
        _registry.RegisterTool(new ExtractCorridorSurfaceTool());
        _registry.RegisterTool(new RebuildCorridorTool());
        _registry.RegisterTool(new QueryCorridorInfoTool());

        // ── Civil 3D – Pipe Network tools ────────────────────────────────
        _registry.RegisterTool(new CreatePipeNetworkTool());
        _registry.RegisterTool(new AddPipeToNetworkTool());
        _registry.RegisterTool(new AddStructureToNetworkTool());
        _registry.RegisterTool(new QueryPipeNetworkTool());
        _registry.RegisterTool(new CheckPipeInterferenceTool());
        _registry.RegisterTool(new CreatePressureNetworkTool());

        // ── Civil 3D – Grading tools ─────────────────────────────────────
        _registry.RegisterTool(new CreateGradingGroupTool());
        _registry.RegisterTool(new CreateGradingBySlope());
        _registry.RegisterTool(new CreateGradingByDistanceTool());
        _registry.RegisterTool(new ModifyFeatureLineElevationsTool());
        _registry.RegisterTool(new CreateFeatureLineTool());

        // ── Civil 3D – Section & Sample Line tools ───────────────────────
        _registry.RegisterTool(new CreateSampleLineGroupTool());
        _registry.RegisterTool(new CreateSampleLineByStationTool());
        _registry.RegisterTool(new CreateSampleLinesAtIntervalTool());
        _registry.RegisterTool(new CreateSectionViewTool());
        _registry.RegisterTool(new ComputeMaterialVolumesTool());

        // ── Civil 3D – COGO Point tools ──────────────────────────────────
        _registry.RegisterTool(new CreateCogoPointTool());
        _registry.RegisterTool(new CreateCogoPointsTool());
        _registry.RegisterTool(new ModifyCogoPointTool());
        _registry.RegisterTool(new CreatePointGroupTool());
        _registry.RegisterTool(new QueryCogoPointsTool());
        _registry.RegisterTool(new QueryPointGroupsTool());

        // ── Civil 3D – Parcel tools ──────────────────────────────────────
        _registry.RegisterTool(new CreateSiteTool());
        _registry.RegisterTool(new CreateParcelBySegmentsTool());
        _registry.RegisterTool(new QueryParcelInfoTool());
        _registry.RegisterTool(new QueryParcelDetailsTool());
        _registry.RegisterTool(new RenumberParcelsTool());

        // ── Civil 3D – Intersection & Roundabout tools ───────────────────
        _registry.RegisterTool(new CreateIntersectionTool());
        _registry.RegisterTool(new QueryIntersectionInfoTool());
        _registry.RegisterTool(new CreateRoundaboutTool());

        // ── Civil 3D – Label Style & Data Shortcut tools ─────────────────
        _registry.RegisterTool(new QueryLabelStylesTool());
        _registry.RegisterTool(new QueryObjectStylesTool());
        _registry.RegisterTool(new SetObjectStyleTool());
        _registry.RegisterTool(new CreateDataShortcutTool());
        _registry.RegisterTool(new ImportDataReferenceTool());

        // ── Civil 3D – QTO & Survey tools ────────────────────────────────
        _registry.RegisterTool(new ComputeEarthworkVolumesTool());
        _registry.RegisterTool(new ComputeAlignmentLengthsTool());
        _registry.RegisterTool(new ComputeSurfaceAreaTool());
        _registry.RegisterTool(new ImportSurveyPointsTool());
        _registry.RegisterTool(new ExportSurveyPointsTool());
        _registry.RegisterTool(new CreateSurveyFigureTool());

        _logger.LogUserInput($"[SYSTEM] Registered {_registry.GetAllTools().Count} tools.");
    }
}
