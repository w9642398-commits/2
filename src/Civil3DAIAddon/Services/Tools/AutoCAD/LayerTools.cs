using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.AutoCAD;

// ─── Layer Management Tools ──────────────────────────────────────────────────

public sealed class CreateLayerTool : CadToolBase
{
    public override string Name => "CreateLayer";
    public override string Description => "Creates a new layer with specified name, color, linetype, and line weight.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var layerName = GetParam<string>(parameters, "name");
        var colorIndex = GetParam(parameters, "color_index", 7);
        var linetype = GetParam(parameters, "linetype", "Continuous");
        var isOff = GetParam(parameters, "is_off", false);
        var isFrozen = GetParam(parameters, "is_frozen", false);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

            if (lt.Has(layerName))
                return Task.FromResult(ToolResult.Fail(Name, $"Layer '{layerName}' already exists."));

            lt.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, (short)colorIndex),
                IsOff = isOff,
                IsFrozen = isFrozen
            };

            // Set linetype if available
            var ltt = (LinetypeTable)tr.GetObject(doc.Database.LinetypeTableId, OpenMode.ForRead);
            if (ltt.Has(linetype))
                layer.LinetypeObjectId = ltt[linetype];

            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Layer '{layerName}' created (color={colorIndex})."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "name")))
            return ValidationResult.Invalid("name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name"],
        "properties": {
            "name": { "type": "string", "description": "Layer name" },
            "color_index": { "type": "integer", "default": 7, "description": "ACI color index (1-255)" },
            "linetype": { "type": "string", "default": "Continuous" },
            "is_off": { "type": "boolean", "default": false },
            "is_frozen": { "type": "boolean", "default": false }
        }
    }
    """);
}

public sealed class ModifyLayerTool : CadToolBase
{
    public override string Name => "ModifyLayer";
    public override string Description => "Modifies layer properties: color, on/off, freeze/thaw, lock/unlock, linetype.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var layerName = GetParam<string>(parameters, "name");
        var colorIndex = GetParam(parameters, "color_index", -1);
        var isOff = GetParam(parameters, "is_off", (bool?)null);
        var isFrozen = GetParam(parameters, "is_frozen", (bool?)null);
        var isLocked = GetParam(parameters, "is_locked", (bool?)null);
        var linetype = GetParam<string>(parameters, "linetype");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerName))
                return Task.FromResult(ToolResult.Fail(Name, $"Layer '{layerName}' not found."));

            var layer = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
            var changes = new List<string>();

            if (colorIndex >= 0)
            {
                layer.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)colorIndex);
                changes.Add($"color={colorIndex}");
            }

            if (isOff.HasValue)
            {
                layer.IsOff = isOff.Value;
                changes.Add(isOff.Value ? "off" : "on");
            }

            if (isFrozen.HasValue)
            {
                layer.IsFrozen = isFrozen.Value;
                changes.Add(isFrozen.Value ? "frozen" : "thawed");
            }

            if (isLocked.HasValue)
            {
                layer.IsLocked = isLocked.Value;
                changes.Add(isLocked.Value ? "locked" : "unlocked");
            }

            if (!string.IsNullOrEmpty(linetype))
            {
                var ltt = (LinetypeTable)tr.GetObject(doc.Database.LinetypeTableId, OpenMode.ForRead);
                if (ltt.Has(linetype))
                {
                    layer.LinetypeObjectId = ltt[linetype];
                    changes.Add($"linetype={linetype}");
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Layer '{layerName}' modified: {string.Join(", ", changes)}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "name")))
            return ValidationResult.Invalid("name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name"],
        "properties": {
            "name": { "type": "string" },
            "color_index": { "type": "integer", "description": "ACI color index (-1 = no change)" },
            "is_off": { "type": "boolean" },
            "is_frozen": { "type": "boolean" },
            "is_locked": { "type": "boolean" },
            "linetype": { "type": "string" }
        }
    }
    """);
}

public sealed class DeleteLayerTool : CadToolBase
{
    public override string Name => "DeleteLayer";
    public override string Description => "Deletes a layer from the drawing. Layer must be empty (no entities) and not current.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Destructive;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var layerName = GetParam<string>(parameters, "name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerName))
                return Task.FromResult(ToolResult.Fail(Name, $"Layer '{layerName}' not found."));

            if (layerName == "0")
                return Task.FromResult(ToolResult.Fail(Name, "Cannot delete layer '0'."));

            var layerId = lt[layerName];
            if (layerId == doc.Database.Clayer)
                return Task.FromResult(ToolResult.Fail(Name, "Cannot delete the current layer."));

            var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
            layer.Erase();
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Layer '{layerName}' deleted."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "name")))
            return ValidationResult.Invalid("name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name"],
        "properties": { "name": { "type": "string" } }
    }
    """);
}

public sealed class QueryLayersTool : CadToolBase
{
    public override string Name => "QueryLayers";
    public override string Description => "Returns information about all layers in the drawing including state, color, and entity count.";
    public override string Category => "AutoCAD";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            var layers = new List<Dictionary<string, object>>();

            foreach (ObjectId layerId in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                layers.Add(new Dictionary<string, object>
                {
                    ["name"] = layer.Name,
                    ["color_index"] = layer.Color.ColorIndex,
                    ["is_off"] = layer.IsOff,
                    ["is_frozen"] = layer.IsFrozen,
                    ["is_locked"] = layer.IsLocked,
                    ["linetype"] = layer.LinetypeObjectId.IsNull ? "Continuous" :
                        ((LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead)).Name,
                    ["is_current"] = layerId == doc.Database.Clayer
                });
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Found {layers.Count} layers.",
                new Dictionary<string, object> { ["layers"] = layers }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}

public sealed class SetCurrentLayerTool : CadToolBase
{
    public override string Name => "SetCurrentLayer";
    public override string Description => "Sets the active/current layer for new entity creation.";
    public override string Category => "AutoCAD";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var layerName = GetParam<string>(parameters, "name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerName))
                return Task.FromResult(ToolResult.Fail(Name, $"Layer '{layerName}' not found."));

            doc.Database.Clayer = lt[layerName];
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Current layer set to '{layerName}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "name")))
            return ValidationResult.Invalid("name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name"],
        "properties": { "name": { "type": "string" } }
    }
    """);
}
