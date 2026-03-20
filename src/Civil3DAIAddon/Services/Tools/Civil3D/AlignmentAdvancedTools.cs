using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.Civil3D;

// ─── Alignment Advanced Tools ────────────────────────────────────────────────

public sealed class CreateAlignmentByLayoutTool : CadToolBase
{
    public override string Name => "CreateAlignmentByLayout";
    public override string Description => "Creates an empty alignment for manual layout design (add tangents, curves, spirals programmatically).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentName = GetParam<string>(parameters, "name");
        var layer = GetParam(parameters, "layer", "C-ROAD-CNTR");
        var startPoint = GetPointParam(parameters, "start_point");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);
            var styleId = civilDoc.Styles.AlignmentStyles[0];
            var labelSetId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles[0];

            var alignmentId = Alignment.Create(
                civilDoc,
                alignmentName,
                ObjectId.Null,
                layerId,
                styleId,
                labelSetId);

            var alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
            var handle = alignment?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Layout alignment '{alignmentName}' created. Use AddAlignmentTangent/AddAlignmentCurve to build geometry. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
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
            "layer": { "type": "string", "default": "C-ROAD-CNTR" },
            "start_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" }
        }
    }
    """);

    private static ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName)
    {
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (lt.Has(layerName)) return lt[layerName];
        lt.UpgradeOpen();
        var lr = new LayerTableRecord { Name = layerName };
        var id = lt.Add(lr);
        tr.AddNewlyCreatedDBObject(lr, true);
        return id;
    }
}

public sealed class AddAlignmentTangentTool : CadToolBase
{
    public override string Name => "AddAlignmentTangent";
    public override string Description => "Adds a tangent (straight) segment to a layout alignment between two points.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var startPoint = GetPointParam(parameters, "start_point");
        var endPoint = GetPointParam(parameters, "end_point");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var alignment = tr.GetObject(alignId, OpenMode.ForWrite) as Alignment;
            if (alignment == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            var pt1 = new Point2d(startPoint[0], startPoint[1]);
            var pt2 = new Point2d(endPoint[0], endPoint[1]);

            alignment.Entities.AddFixedLine(pt1, pt2);
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Tangent added from ({pt1.X:F2}, {pt1.Y:F2}) to ({pt2.X:F2}, {pt2.Y:F2})."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        if (GetPointParam(parameters, "start_point").Length < 2)
            return ValidationResult.Invalid("start_point requires [x, y]");
        if (GetPointParam(parameters, "end_point").Length < 2)
            return ValidationResult.Invalid("end_point requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["alignment_handle", "start_point", "end_point"],
        "properties": {
            "alignment_handle": { "type": "string" },
            "start_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" },
            "end_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class AddAlignmentCurveTool : CadToolBase
{
    public override string Name => "AddAlignmentCurve";
    public override string Description => "Adds a curve (arc) to a layout alignment with center, radius, and pass-through point or between two entities.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var center = GetPointParam(parameters, "center");
        var radius = GetParam<double>(parameters, "radius");
        var passThrough = GetPointParam(parameters, "pass_through_point");
        var clockwise = GetParam(parameters, "clockwise", false);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var alignment = tr.GetObject(alignId, OpenMode.ForWrite) as Alignment;
            if (alignment == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            if (center.Length >= 2 && passThrough.Length >= 2)
            {
                var centerPt = new Point2d(center[0], center[1]);
                var throughPt = new Point2d(passThrough[0], passThrough[1]);

                alignment.Entities.AddFixedCurve(
                    alignment.Entities.LastEntity,
                    centerPt,
                    throughPt,
                    clockwise);
            }
            else if (center.Length >= 2 && radius > 0)
            {
                var centerPt = new Point2d(center[0], center[1]);

                alignment.Entities.AddFixedCurve(
                    alignment.Entities.LastEntity,
                    centerPt,
                    radius,
                    clockwise);
            }
            else
            {
                return Task.FromResult(ToolResult.Fail(Name, "Provide center + radius or center + pass_through_point."));
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Curve (R={radius:F2}) added to alignment."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["alignment_handle"],
        "properties": {
            "alignment_handle": { "type": "string" },
            "center": { "type": "array", "items": { "type": "number" }, "description": "[x, y] center of curve" },
            "radius": { "type": "number", "description": "Curve radius" },
            "pass_through_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" },
            "clockwise": { "type": "boolean", "default": false }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class AddAlignmentSpiralTool : CadToolBase
{
    public override string Name => "AddAlignmentSpiral";
    public override string Description => "Adds a spiral (clothoid) transition curve to a layout alignment.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var spiralLength = GetParam<double>(parameters, "spiral_length");
        var radius = GetParam<double>(parameters, "radius");
        var spiralType = GetParam(parameters, "spiral_type", "Clothoid");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var alignment = tr.GetObject(alignId, OpenMode.ForWrite) as Alignment;
            if (alignment == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            var sType = spiralType.ToLower() switch
            {
                "clothoid" => SpiralType.Clothoid,
                "bloss" => SpiralType.Bloss,
                "sinusoidal" => SpiralType.Sinusoidal,
                "cosinusoidal" => SpiralType.Cosinusoidal,
                _ => SpiralType.Clothoid
            };

            // Add spiral between the last two entities
            if (alignment.Entities.Count >= 2)
            {
                alignment.Entities.AddFloatSpiral(
                    alignment.Entities.LastEntity,
                    radius,
                    spiralLength,
                    sType,
                    true);
            }
            else
            {
                return Task.FromResult(ToolResult.Fail(Name,
                    "Alignment needs at least 2 entities before adding a spiral transition."));
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Spiral ({spiralType}, L={spiralLength}, R={radius}) added to alignment."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        if (GetParam<double>(parameters, "spiral_length") <= 0)
            return ValidationResult.Invalid("spiral_length must be positive");
        if (GetParam<double>(parameters, "radius") <= 0)
            return ValidationResult.Invalid("radius must be positive");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["alignment_handle", "spiral_length", "radius"],
        "properties": {
            "alignment_handle": { "type": "string" },
            "spiral_length": { "type": "number", "description": "Length of the spiral transition" },
            "radius": { "type": "number", "description": "End radius of the spiral" },
            "spiral_type": { "type": "string", "enum": ["Clothoid", "Bloss", "Sinusoidal", "Cosinusoidal"], "default": "Clothoid" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class SetAlignmentSuperelevationTool : CadToolBase
{
    public override string Name => "SetAlignmentSuperelevation";
    public override string Description => "Configures superelevation parameters for an alignment (cross slope, pivot point, transition method).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var normalCrownSlope = GetParam(parameters, "normal_crown_slope", -2.0);
        var maxSuperRate = GetParam(parameters, "max_super_rate", 8.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var alignment = tr.GetObject(alignId, OpenMode.ForWrite) as Alignment;
            if (alignment == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            // Calculate superelevation – this uses the alignment's built-in superelevation calculation
            alignment.CalculateSuperelevation(normalCrownSlope, maxSuperRate);

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Superelevation calculated for alignment '{alignment.Name}': normal crown={normalCrownSlope}%, max super={maxSuperRate}%."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["alignment_handle"],
        "properties": {
            "alignment_handle": { "type": "string" },
            "normal_crown_slope": { "type": "number", "default": -2.0, "description": "Normal cross slope (%)" },
            "max_super_rate": { "type": "number", "default": 8.0, "description": "Maximum superelevation rate (%)" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ModifyAlignmentGeometryTool : CadToolBase
{
    public override string Name => "ModifyAlignmentGeometry";
    public override string Description => "Modifies alignment geometry: reverse direction, change station equations, or update curve parameters.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var operation = GetParam<string>(parameters, "operation");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var alignment = tr.GetObject(alignId, OpenMode.ForWrite) as Alignment;
            if (alignment == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            string resultMsg;

            switch (operation?.ToLower())
            {
                case "reverse":
                    alignment.ReverseDirection();
                    resultMsg = "Alignment direction reversed.";
                    break;

                case "set_start_station":
                    var startSta = GetParam(parameters, "start_station", 0.0);
                    alignment.ReferencePointStation = startSta;
                    resultMsg = $"Start station set to {startSta:F3}.";
                    break;

                case "add_station_equation":
                    var rawStation = GetParam<double>(parameters, "raw_station");
                    var stationAhead = GetParam<double>(parameters, "station_ahead");
                    alignment.StationEquations.Add(rawStation, stationAhead);
                    resultMsg = $"Station equation added: raw={rawStation:F3}, ahead={stationAhead:F3}.";
                    break;

                default:
                    return Task.FromResult(ToolResult.Fail(Name, $"Unknown operation: {operation}"));
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Alignment '{alignment.Name}': {resultMsg}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "operation")))
            return ValidationResult.Invalid("operation is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["alignment_handle", "operation"],
        "properties": {
            "alignment_handle": { "type": "string" },
            "operation": { "type": "string", "enum": ["reverse", "set_start_station", "add_station_equation"] },
            "start_station": { "type": "number", "description": "For set_start_station" },
            "raw_station": { "type": "number", "description": "For add_station_equation" },
            "station_ahead": { "type": "number", "description": "For add_station_equation" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}
