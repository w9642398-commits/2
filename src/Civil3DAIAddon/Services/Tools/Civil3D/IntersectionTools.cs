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

// ─── Intersection Tools ──────────────────────────────────────────────────────

public sealed class CreateIntersectionTool : CadToolBase
{
    public override string Name => "CreateIntersection";
    public override string Description => "Creates a Civil 3D intersection between two alignments at their crossing point.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var mainAlignmentHandle = GetParam<string>(parameters, "main_alignment_handle");
        var crossAlignmentHandle = GetParam<string>(parameters, "cross_alignment_handle");
        var intersectionName = GetParam(parameters, "name", "Intersection");
        var layer = GetParam(parameters, "layer", "C-ROAD-INTR");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var mainId = GetObjectIdFromHandle(doc.Database, mainAlignmentHandle);
            var crossId = GetObjectIdFromHandle(doc.Database, crossAlignmentHandle);

            if (mainId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Main alignment not found."));
            if (crossId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Cross alignment not found."));

            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);

            var intId = Intersection.Create(civilDoc, mainId, crossId, intersectionName, layerId);

            var intersection = tr.GetObject(intId, OpenMode.ForRead) as Intersection;
            var handle = intersection?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Intersection '{intersectionName}' created. Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "main_alignment_handle")))
            return ValidationResult.Invalid("main_alignment_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "cross_alignment_handle")))
            return ValidationResult.Invalid("cross_alignment_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["main_alignment_handle", "cross_alignment_handle"],
        "properties": {
            "main_alignment_handle": { "type": "string", "description": "Handle of main (through) alignment" },
            "cross_alignment_handle": { "type": "string", "description": "Handle of crossing alignment" },
            "name": { "type": "string", "default": "Intersection" },
            "layer": { "type": "string", "default": "C-ROAD-INTR" }
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

public sealed class QueryIntersectionInfoTool : CadToolBase
{
    public override string Name => "QueryIntersectionInfo";
    public override string Description => "Returns information about a Civil 3D intersection, including curb returns and corridors.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var intersectionHandle = GetParam<string>(parameters, "intersection_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var intId = GetObjectIdFromHandle(doc.Database, intersectionHandle);
            if (intId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Intersection not found."));

            var intersection = tr.GetObject(intId, OpenMode.ForRead) as Intersection;
            if (intersection == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Intersection."));

            var data = new Dictionary<string, object>
            {
                ["name"] = intersection.Name,
                ["handle"] = intersection.Handle.ToString(),
                ["location_x"] = intersection.Location.X,
                ["location_y"] = intersection.Location.Y,
                ["main_alignment"] = intersection.MainAlignmentId.Handle.ToString(),
                ["cross_alignment"] = intersection.CrossAlignmentId.Handle.ToString()
            };

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Intersection '{intersection.Name}' at ({intersection.Location.X:F2}, {intersection.Location.Y:F2}).",
                data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "intersection_handle")))
            return ValidationResult.Invalid("intersection_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["intersection_handle"],
        "properties": { "intersection_handle": { "type": "string" } }
    }
    """);

}

public sealed class CreateRoundaboutTool : CadToolBase
{
    public override string Name => "CreateRoundabout";
    public override string Description => "Creates a roundabout design at an intersection location with specified geometry parameters.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var centerPoint = GetPointParam(parameters, "center");
        var inscribedRadius = GetParam(parameters, "inscribed_radius", 20.0);
        var circulatingWidth = GetParam(parameters, "circulating_width", 6.0);
        var name = GetParam(parameters, "name", "Roundabout");
        var layer = GetParam(parameters, "layer", "C-ROAD-RNDB");
        var approachHandles = GetParam(parameters, "approach_alignment_handles", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);

            if (centerPoint.Length < 2)
                return Task.FromResult(ToolResult.Fail(Name, "center point requires [x, y]."));

            var center = new Point3d(centerPoint[0], centerPoint[1], 0);

            // Create the central island as a circle
            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            // Inner island circle
            var innerCircle = new Circle(center, Vector3d.ZAxis, inscribedRadius - circulatingWidth);
            innerCircle.Layer = layer;
            btr.AppendEntity(innerCircle);
            tr.AddNewlyCreatedDBObject(innerCircle, true);

            // Outer boundary circle
            var outerCircle = new Circle(center, Vector3d.ZAxis, inscribedRadius);
            outerCircle.Layer = layer;
            btr.AppendEntity(outerCircle);
            tr.AddNewlyCreatedDBObject(outerCircle, true);

            // Create circulatory alignment from outer circle
            var styleId = civilDoc.Styles.AlignmentStyles[0];
            var labelSetId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles[0];

            var circAlignId = Alignment.Create(civilDoc, $"{name} - Circulatory",
                ObjectId.Null, layerId, styleId, labelSetId);
            var circAlign = tr.GetObject(circAlignId, OpenMode.ForWrite) as Alignment;

            if (circAlign != null)
            {
                // Create circular alignment geometry
                var startPt = new Point2d(center.X + inscribedRadius, center.Y);
                circAlign.Entities.AddFixedCurve(
                    new Point2d(center.X, center.Y),
                    inscribedRadius,
                    true);
            }

            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Roundabout '{name}' created at ({center.X:F2}, {center.Y:F2}), R={inscribedRadius}m, width={circulatingWidth}m.");
            result.CreatedHandles.Add(innerCircle.Handle.ToString());
            result.CreatedHandles.Add(outerCircle.Handle.ToString());
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (GetPointParam(parameters, "center").Length < 2)
            return ValidationResult.Invalid("center requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["center"],
        "properties": {
            "center": { "type": "array", "items": { "type": "number" }, "description": "[x, y] center of roundabout" },
            "inscribed_radius": { "type": "number", "default": 20.0, "description": "Inscribed circle radius (m)" },
            "circulating_width": { "type": "number", "default": 6.0, "description": "Width of circulating roadway (m)" },
            "name": { "type": "string", "default": "Roundabout" },
            "layer": { "type": "string", "default": "C-ROAD-RNDB" },
            "approach_alignment_handles": { "type": "string", "description": "Comma-separated handles of approach alignments" }
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
