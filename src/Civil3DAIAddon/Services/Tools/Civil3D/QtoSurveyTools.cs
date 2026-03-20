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

// ─── Quantity Takeoff Tools ──────────────────────────────────────────────────

public sealed class ComputeEarthworkVolumesTool : CadToolBase
{
    public override string Name => "ComputeEarthworkVolumes";
    public override string Description => "Computes cut and fill earthwork volumes between two surfaces using composite volume method.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var existingSurfaceHandle = GetParam<string>(parameters, "existing_surface_handle");
        var proposedSurfaceHandle = GetParam<string>(parameters, "proposed_surface_handle");
        var cutFactor = GetParam(parameters, "cut_factor", 1.0);
        var fillFactor = GetParam(parameters, "fill_factor", 1.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var existId = GetObjectIdFromHandle(doc.Database, existingSurfaceHandle);
            var proposedId = GetObjectIdFromHandle(doc.Database, proposedSurfaceHandle);

            if (existId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Existing surface not found."));
            if (proposedId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Proposed surface not found."));

            var civilDoc = CivilApplication.ActiveDocument;

            // Create temporary volume surface for calculation
            var styleId = civilDoc.Styles.SurfaceStyles[0];
            var volSurfId = TinVolumeSurface.Create("_TempVolCalc", existId, proposedId, styleId);
            var volSurf = tr.GetObject(volSurfId, OpenMode.ForRead) as TinVolumeSurface;

            if (volSurf == null)
                return Task.FromResult(ToolResult.Fail(Name, "Failed to create volume surface."));

            var volProps = volSurf.GetVolumeProperties();
            var cutVol = volProps.UnadjustedCutVolume * cutFactor;
            var fillVol = volProps.UnadjustedFillVolume * fillFactor;
            var netVol = cutVol - fillVol;

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Earthwork: Cut={cutVol:F2}m³, Fill={fillVol:F2}m³, Net={netVol:F2}m³",
                new Dictionary<string, object>
                {
                    ["cut_volume"] = cutVol,
                    ["fill_volume"] = fillVol,
                    ["net_volume"] = netVol,
                    ["cut_area"] = volProps.UnadjustedCutArea,
                    ["fill_area"] = volProps.UnadjustedFillArea,
                    ["cut_factor"] = cutFactor,
                    ["fill_factor"] = fillFactor
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "existing_surface_handle")))
            return ValidationResult.Invalid("existing_surface_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "proposed_surface_handle")))
            return ValidationResult.Invalid("proposed_surface_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["existing_surface_handle", "proposed_surface_handle"],
        "properties": {
            "existing_surface_handle": { "type": "string" },
            "proposed_surface_handle": { "type": "string" },
            "cut_factor": { "type": "number", "default": 1.0, "description": "Cut volume expansion/shrinkage factor" },
            "fill_factor": { "type": "number", "default": 1.0, "description": "Fill volume expansion/shrinkage factor" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ComputeAlignmentLengthsTool : CadToolBase
{
    public override string Name => "ComputeAlignmentLengths";
    public override string Description => "Computes total and per-segment lengths for an alignment, useful for material quantity calculations.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var alignment = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
            if (alignment == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            double tangentLength = 0, curveLength = 0, spiralLength = 0;
            var segments = new List<Dictionary<string, object>>();

            foreach (AlignmentEntity entity in alignment.Entities)
            {
                var seg = new Dictionary<string, object>
                {
                    ["type"] = entity.EntityType.ToString(),
                    ["length"] = entity.Length
                };

                switch (entity.EntityType)
                {
                    case AlignmentEntityType.Line:
                        tangentLength += entity.Length;
                        break;
                    case AlignmentEntityType.Arc:
                        curveLength += entity.Length;
                        if (entity is AlignmentArc arc)
                            seg["radius"] = arc.Radius;
                        break;
                    case AlignmentEntityType.Spiral:
                        spiralLength += entity.Length;
                        break;
                }

                segments.Add(seg);
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Alignment '{alignment.Name}': total={alignment.Length:F2}m, tangent={tangentLength:F2}m, curve={curveLength:F2}m, spiral={spiralLength:F2}m.",
                new Dictionary<string, object>
                {
                    ["total_length"] = alignment.Length,
                    ["tangent_length"] = tangentLength,
                    ["curve_length"] = curveLength,
                    ["spiral_length"] = spiralLength,
                    ["segments"] = segments
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
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
        "properties": { "alignment_handle": { "type": "string" } }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ComputeSurfaceAreaTool : CadToolBase
{
    public override string Name => "ComputeSurfaceArea";
    public override string Description => "Computes the 2D and 3D area of a surface for material quantity estimation.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
            if (surfId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

            var surface = tr.GetObject(surfId, OpenMode.ForRead) as TinSurface;
            if (surface == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a TIN Surface."));

            var props = surface.GetGeneralProperties();

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Surface '{surface.Name}': 2D area={props.Area2d:F2}m², 3D area={props.Area3d:F2}m².",
                new Dictionary<string, object>
                {
                    ["name"] = surface.Name,
                    ["area_2d"] = props.Area2d,
                    ["area_3d"] = props.Area3d,
                    ["min_elevation"] = props.MinimumElevation,
                    ["max_elevation"] = props.MaximumElevation,
                    ["number_of_points"] = props.NumberOfPoints,
                    ["number_of_triangles"] = props.NumberOfTriangles
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "surface_handle")))
            return ValidationResult.Invalid("surface_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["surface_handle"],
        "properties": { "surface_handle": { "type": "string" } }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

// ─── Survey Tools ────────────────────────────────────────────────────────────

public sealed class ImportSurveyPointsTool : CadToolBase
{
    public override string Name => "ImportSurveyPoints";
    public override string Description => "Imports survey points from a PNEZD (Point Number, Northing, Easting, Elevation, Description) format file.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var filePath = GetParam<string>(parameters, "file_path");
        var format = GetParam(parameters, "format", "PNEZD");
        var pointGroupName = GetParam(parameters, "point_group_name", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return Task.FromResult(ToolResult.Fail(Name, $"File not found: {filePath}"));

            var civilDoc = CivilApplication.ActiveDocument;

            // Import points using point file format
            civilDoc.CogoPoints.ImportPoints(filePath, format);

            tr.Commit();
            return Task.FromResult(ToolResult.Ok(Name,
                $"Survey points imported from '{System.IO.Path.GetFileName(filePath)}' using format '{format}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "file_path")))
            return ValidationResult.Invalid("file_path is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["file_path"],
        "properties": {
            "file_path": { "type": "string", "description": "Full path to the point data file" },
            "format": { "type": "string", "default": "PNEZD", "description": "Point file format (PNEZD, PENZD, etc.)" },
            "point_group_name": { "type": "string", "description": "Point group to assign imported points to" }
        }
    }
    """);
}

public sealed class ExportSurveyPointsTool : CadToolBase
{
    public override string Name => "ExportSurveyPoints";
    public override string Description => "Exports COGO points to a text file in specified format.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var filePath = GetParam<string>(parameters, "file_path");
        var format = GetParam(parameters, "format", "PNEZD");
        var pointGroupName = GetParam<string>(parameters, "point_group_name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var lines = new List<string>();

            ObjectIdCollection pointIds;
            if (!string.IsNullOrEmpty(pointGroupName))
            {
                PointGroup? pg = null;
                foreach (ObjectId pgId in civilDoc.GetPointGroupIds())
                {
                    var group = tr.GetObject(pgId, OpenMode.ForRead) as PointGroup;
                    if (group?.Name.Equals(pointGroupName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        pg = group;
                        break;
                    }
                }
                pointIds = pg?.GetPointIds() ?? civilDoc.CogoPoints.GetPointIds();
            }
            else
            {
                pointIds = civilDoc.CogoPoints.GetPointIds();
            }

            foreach (ObjectId ptId in pointIds)
            {
                var pt = tr.GetObject(ptId, OpenMode.ForRead) as CogoPoint;
                if (pt != null)
                {
                    lines.Add($"{pt.PointNumber},{pt.Northing:F4},{pt.Easting:F4},{pt.Elevation:F4},{pt.RawDescription}");
                }
            }

            System.IO.File.WriteAllLines(filePath, lines);
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Exported {lines.Count} points to '{System.IO.Path.GetFileName(filePath)}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "file_path")))
            return ValidationResult.Invalid("file_path is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["file_path"],
        "properties": {
            "file_path": { "type": "string", "description": "Output file path" },
            "format": { "type": "string", "default": "PNEZD" },
            "point_group_name": { "type": "string", "description": "Export only this group (empty = all)" }
        }
    }
    """);
}

public sealed class CreateSurveyFigureTool : CadToolBase
{
    public override string Name => "CreateSurveyFigure";
    public override string Description => "Creates a survey figure (breakline/boundary) from COGO points by point numbers.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var figureName = GetParam<string>(parameters, "name");
        var pointNumbers = GetParam<string>(parameters, "point_numbers"); // comma-separated
        var layer = GetParam(parameters, "layer", "V-SURV-FIGU");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var numbers = pointNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var points = new Point3dCollection();
            foreach (var numStr in numbers)
            {
                if (uint.TryParse(numStr, out uint num))
                {
                    foreach (ObjectId ptId in civilDoc.CogoPoints.GetPointIds())
                    {
                        var pt = tr.GetObject(ptId, OpenMode.ForRead) as CogoPoint;
                        if (pt != null && pt.PointNumber == num)
                        {
                            points.Add(new Point3d(pt.Easting, pt.Northing, pt.Elevation));
                            break;
                        }
                    }
                }
            }

            if (points.Count < 2)
                return Task.FromResult(ToolResult.Fail(Name, "Need at least 2 valid point numbers."));

            // Create as a 3D polyline (survey figure representation)
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);
            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var poly3d = new Polyline3d(Poly3dType.SimplePoly, points, false);
            poly3d.Layer = layer;
            btr.AppendEntity(poly3d);
            tr.AddNewlyCreatedDBObject(poly3d, true);

            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Survey figure '{figureName}' created from {points.Count} points. Handle: {poly3d.Handle}");
            result.CreatedHandles.Add(poly3d.Handle.ToString());
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "point_numbers")))
            return ValidationResult.Invalid("point_numbers is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name", "point_numbers"],
        "properties": {
            "name": { "type": "string", "description": "Figure name" },
            "point_numbers": { "type": "string", "description": "Comma-separated COGO point numbers (e.g., '1,2,3,4')" },
            "layer": { "type": "string", "default": "V-SURV-FIGU" }
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
