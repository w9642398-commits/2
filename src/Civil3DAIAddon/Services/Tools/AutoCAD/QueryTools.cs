using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.AutoCAD;

public sealed class GetActiveDocumentContextTool : CadToolBase
{
    private readonly IDrawingContextExtractor _extractor;

    public GetActiveDocumentContextTool(IDrawingContextExtractor extractor) => _extractor = extractor;

    public override string Name => "GetActiveDocumentContext";
    public override string Description => "Returns a snapshot of the active drawing including file name, units, layers, entity count, and Civil 3D objects.";
    public override string Category => "Query";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var scope = GetParam(parameters, "scope", "AllDrawing");
        var contextScope = Enum.TryParse<ContextScope>(scope, true, out var s) ? s : ContextScope.AllDrawing;
        var snapshot = _extractor.ExtractSnapshot(contextScope);

        return Task.FromResult(ToolResult.Ok(Name, "Document context extracted.", new Dictionary<string, object>
        {
            ["snapshot"] = snapshot
        }));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "properties": {
            "scope": { "type": "string", "enum": ["Selection", "ModelSpace", "CurrentView", "AllDrawing"] }
        }
    }
    """);
}

public sealed class GetCurrentSelectionTool : CadToolBase
{
    private readonly IDrawingContextExtractor _extractor;

    public GetCurrentSelectionTool(IDrawingContextExtractor extractor) => _extractor = extractor;

    public override string Name => "GetCurrentSelection";
    public override string Description => "Returns all currently selected entities with their properties.";
    public override string Category => "Query";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entities = _extractor.GetSelectedEntities();
        return Task.FromResult(ToolResult.Ok(Name, $"Found {entities.Count} selected entities.", new Dictionary<string, object>
        {
            ["entities"] = entities
        }));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}

public sealed class GetVisibleEntitiesTool : CadToolBase
{
    private readonly IDrawingContextExtractor _extractor;

    public GetVisibleEntitiesTool(IDrawingContextExtractor extractor) => _extractor = extractor;

    public override string Name => "GetVisibleEntities";
    public override string Description => "Returns visible entities in model space, up to a maximum count.";
    public override string Category => "Query";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var maxCount = GetParam(parameters, "max_count", 200);
        var entities = _extractor.GetVisibleEntities(maxCount);
        return Task.FromResult(ToolResult.Ok(Name, $"Found {entities.Count} visible entities.", new Dictionary<string, object>
        {
            ["entities"] = entities
        }));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "properties": {
            "max_count": { "type": "integer", "description": "Maximum number of entities to return", "default": 200 }
        }
    }
    """);
}

public sealed class QueryEntitiesByTypeTool : CadToolBase
{
    private readonly IDrawingContextExtractor _extractor;

    public QueryEntitiesByTypeTool(IDrawingContextExtractor extractor) => _extractor = extractor;

    public override string Name => "QueryEntitiesByType";
    public override string Description => "Returns all entities of a given type (Line, Polyline, Circle, Arc, etc.).";
    public override string Category => "Query";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var typeName = GetParam<string>(parameters, "type_name");
        var entities = _extractor.GetEntitiesByType(typeName);
        return Task.FromResult(ToolResult.Ok(Name, $"Found {entities.Count} entities of type '{typeName}'.", new Dictionary<string, object>
        {
            ["entities"] = entities
        }));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var typeName = GetParam<string>(parameters, "type_name");
        return string.IsNullOrWhiteSpace(typeName)
            ? ValidationResult.Invalid("type_name is required")
            : ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["type_name"],
        "properties": {
            "type_name": { "type": "string", "description": "Entity type name (Line, Polyline, Circle, Arc, DBText, MText, etc.)" }
        }
    }
    """);
}

public sealed class QueryEntitiesByLayerTool : CadToolBase
{
    private readonly IDrawingContextExtractor _extractor;

    public QueryEntitiesByLayerTool(IDrawingContextExtractor extractor) => _extractor = extractor;

    public override string Name => "QueryEntitiesByLayer";
    public override string Description => "Returns all entities on a specified layer.";
    public override string Category => "Query";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var layerName = GetParam<string>(parameters, "layer_name");
        var entities = _extractor.GetEntitiesByLayer(layerName);
        return Task.FromResult(ToolResult.Ok(Name, $"Found {entities.Count} entities on layer '{layerName}'.", new Dictionary<string, object>
        {
            ["entities"] = entities
        }));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var layerName = GetParam<string>(parameters, "layer_name");
        return string.IsNullOrWhiteSpace(layerName)
            ? ValidationResult.Invalid("layer_name is required")
            : ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["layer_name"],
        "properties": {
            "layer_name": { "type": "string", "description": "Layer name to query" }
        }
    }
    """);
}

public sealed class QueryCivilObjectsTool : CadToolBase
{
    private readonly IDrawingContextExtractor _extractor;

    public QueryCivilObjectsTool(IDrawingContextExtractor extractor) => _extractor = extractor;

    public override string Name => "QueryCivilObjects";
    public override string Description => "Returns Civil 3D objects (alignments, surfaces, profiles, etc.) in the current drawing.";
    public override string Category => "Query";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var objects = _extractor.GetCivilObjects();
        return Task.FromResult(ToolResult.Ok(Name, $"Found {objects.Count} Civil 3D objects.", new Dictionary<string, object>
        {
            ["civil_objects"] = objects
        }));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}
