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

// ─── Surface Advanced Tools ──────────────────────────────────────────────────

public sealed class AddSurfaceBreaklinesTool : CadToolBase
{
    public override string Name => "AddSurfaceBreaklines";
    public override string Description => "Adds breaklines to a TIN surface from polylines or feature lines (standard, wall, or proximity).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");
        var entityHandles = GetParam<string>(parameters, "entity_handles"); // comma-separated
        var breaklineType = GetParam(parameters, "breakline_type", "Standard");
        var description = GetParam(parameters, "description", "Breakline");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
            if (surfId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

            var surface = tr.GetObject(surfId, OpenMode.ForWrite) as TinSurface;
            if (surface == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a TIN Surface."));

            var handles = entityHandles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var entityIds = new ObjectIdCollection();
            foreach (var h in handles)
            {
                var eid = GetObjectIdFromHandle(doc.Database, h);
                if (!eid.IsNull) entityIds.Add(eid);
            }

            if (entityIds.Count == 0)
                return Task.FromResult(ToolResult.Fail(Name, "No valid entities found for breaklines."));

            surface.BreaklinesDefinition.AddStandardBreaklines(entityIds, 1.0, 0.0, 0.0, 0.0);
            surface.Rebuild();
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Added {entityIds.Count} breakline(s) to surface '{surface.Name}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "surface_handle")))
            return ValidationResult.Invalid("surface_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "entity_handles")))
            return ValidationResult.Invalid("entity_handles is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["surface_handle", "entity_handles"],
        "properties": {
            "surface_handle": { "type": "string" },
            "entity_handles": { "type": "string", "description": "Comma-separated handles of polylines/feature lines" },
            "breakline_type": { "type": "string", "enum": ["Standard", "Wall", "Proximity"], "default": "Standard" },
            "description": { "type": "string", "default": "Breakline" }
        }
    }
    """);

}

public sealed class AddSurfaceBoundaryTool : CadToolBase
{
    public override string Name => "AddSurfaceBoundary";
    public override string Description => "Adds a boundary (outer, hide, show, or data clip) to a TIN surface from a closed polyline.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");
        var polylineHandle = GetParam<string>(parameters, "polyline_handle");
        var boundaryType = GetParam(parameters, "boundary_type", "Outer");
        var boundaryName = GetParam(parameters, "name", "Boundary");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
            var polyId = GetObjectIdFromHandle(doc.Database, polylineHandle);

            if (surfId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));
            if (polyId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Polyline not found."));

            var surface = tr.GetObject(surfId, OpenMode.ForWrite) as TinSurface;
            if (surface == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a TIN Surface."));

            var entityIds = new ObjectIdCollection { polyId };

            var bType = boundaryType.ToLower() switch
            {
                "outer" => SurfaceBoundaryType.Outer,
                "hide" => SurfaceBoundaryType.Hide,
                "show" => SurfaceBoundaryType.Show,
                "data_clip" => SurfaceBoundaryType.DataClip,
                _ => SurfaceBoundaryType.Outer
            };

            surface.BoundariesDefinition.AddBoundaries(entityIds, 1.0, bType, true);
            surface.Rebuild();
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Boundary '{boundaryName}' ({boundaryType}) added to surface '{surface.Name}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "surface_handle")))
            return ValidationResult.Invalid("surface_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "polyline_handle")))
            return ValidationResult.Invalid("polyline_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["surface_handle", "polyline_handle"],
        "properties": {
            "surface_handle": { "type": "string" },
            "polyline_handle": { "type": "string", "description": "Handle of closed polyline for boundary" },
            "boundary_type": { "type": "string", "enum": ["Outer", "Hide", "Show", "DataClip"], "default": "Outer" },
            "name": { "type": "string", "default": "Boundary" }
        }
    }
    """);

}

public sealed class CreateVolumeSurfaceTool : CadToolBase
{
    public override string Name => "CreateVolumeSurface";
    public override string Description => "Creates a TIN volume surface comparing a base surface and a comparison surface for cut/fill analysis.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceName = GetParam<string>(parameters, "name");
        var baseSurfaceHandle = GetParam<string>(parameters, "base_surface_handle");
        var comparisonSurfaceHandle = GetParam<string>(parameters, "comparison_surface_handle");
        var layer = GetParam(parameters, "layer", "C-TOPO-VOLM");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var baseId = GetObjectIdFromHandle(doc.Database, baseSurfaceHandle);
            var compId = GetObjectIdFromHandle(doc.Database, comparisonSurfaceHandle);

            if (baseId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Base surface not found."));
            if (compId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Comparison surface not found."));

            var civilDoc = CivilApplication.ActiveDocument;
            var styleId = civilDoc.Styles.SurfaceStyles[0];

            var volSurfId = TinVolumeSurface.Create(surfaceName, baseId, compId, styleId);

            var volSurf = tr.GetObject(volSurfId, OpenMode.ForRead) as TinVolumeSurface;
            var handle = volSurf?.Handle.ToString() ?? "unknown";

            var data = new Dictionary<string, object>
            {
                ["name"] = surfaceName,
                ["handle"] = handle
            };

            if (volSurf != null)
            {
                var volProps = volSurf.GetVolumeProperties();
                data["cut_volume"] = volProps.UnadjustedCutVolume;
                data["fill_volume"] = volProps.UnadjustedFillVolume;
                data["net_volume"] = volProps.UnadjustedCutVolume - volProps.UnadjustedFillVolume;
            }

            tr.Commit();

            var result = ToolResult.Ok(Name, $"Volume surface '{surfaceName}' created. Handle: {handle}", data);
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "base_surface_handle")))
            return ValidationResult.Invalid("base_surface_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "comparison_surface_handle")))
            return ValidationResult.Invalid("comparison_surface_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name", "base_surface_handle", "comparison_surface_handle"],
        "properties": {
            "name": { "type": "string" },
            "base_surface_handle": { "type": "string", "description": "Handle of base (existing) surface" },
            "comparison_surface_handle": { "type": "string", "description": "Handle of comparison (proposed) surface" },
            "layer": { "type": "string", "default": "C-TOPO-VOLM" }
        }
    }
    """);

}

public sealed class AnalyzeSurfaceSlopeTool : CadToolBase
{
    public override string Name => "AnalyzeSurfaceSlope";
    public override string Description => "Analyzes slope distribution across a surface and returns slope statistics.";
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

            // Analyze slope across triangles
            var triangles = surface.GetTriangles(false);
            double maxSlope = 0, minSlope = double.MaxValue, totalSlope = 0;
            int count = 0;

            foreach (var tri in triangles)
            {
                var slope = Math.Abs(tri.Slope);
                if (slope > maxSlope) maxSlope = slope;
                if (slope < minSlope) minSlope = slope;
                totalSlope += slope;
                count++;
            }

            var avgSlope = count > 0 ? totalSlope / count : 0;

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Surface '{surface.Name}' slope analysis: min={minSlope:F2}%, avg={avgSlope:F2}%, max={maxSlope:F2}%.",
                new Dictionary<string, object>
                {
                    ["name"] = surface.Name,
                    ["min_slope_percent"] = minSlope,
                    ["max_slope_percent"] = maxSlope,
                    ["average_slope_percent"] = avgSlope,
                    ["triangle_count"] = count,
                    ["min_elevation"] = props.MinimumElevation,
                    ["max_elevation"] = props.MaximumElevation
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

}

public sealed class GetSurfaceElevationAtPointTool : CadToolBase
{
    public override string Name => "GetSurfaceElevationAtPoint";
    public override string Description => "Returns the elevation of a surface at a specific X,Y coordinate.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");
        var x = GetParam<double>(parameters, "x");
        var y = GetParam<double>(parameters, "y");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
            if (surfId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

            var surface = tr.GetObject(surfId, OpenMode.ForRead) as TinSurface;
            if (surface == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a TIN Surface."));

            var elevation = surface.FindElevationAtXY(x, y);
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Elevation at ({x:F3}, {y:F3}) = {elevation:F3}",
                new Dictionary<string, object>
                {
                    ["x"] = x,
                    ["y"] = y,
                    ["elevation"] = elevation
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
        "required": ["surface_handle", "x", "y"],
        "properties": {
            "surface_handle": { "type": "string" },
            "x": { "type": "number" },
            "y": { "type": "number" }
        }
    }
    """);

}

public sealed class AddPointsToSurfaceTool : CadToolBase
{
    public override string Name => "AddPointsToSurface";
    public override string Description => "Adds individual points to a TIN surface definition.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");
        var points = GetPointsParam(parameters, "points");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
            if (surfId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

            var surface = tr.GetObject(surfId, OpenMode.ForWrite) as TinSurface;
            if (surface == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a TIN Surface."));

            var pointCol = new Point3dCollection();
            foreach (var pt in points)
                pointCol.Add(new Point3d(pt[0], pt[1], pt.Length > 2 ? pt[2] : 0));

            surface.AddPoints(pointCol);
            surface.Rebuild();
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Added {points.Count} points to surface '{surface.Name}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "surface_handle")))
            return ValidationResult.Invalid("surface_handle is required");
        var pts = GetPointsParam(parameters, "points");
        if (pts.Count == 0) return ValidationResult.Invalid("At least 1 point required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["surface_handle", "points"],
        "properties": {
            "surface_handle": { "type": "string" },
            "points": { "type": "array", "items": { "type": "array", "items": { "type": "number" } }, "description": "Array of [x, y, z] points" }
        }
    }
    """);

}

public sealed class PasteSurfaceTool : CadToolBase
{
    public override string Name => "PasteSurface";
    public override string Description => "Pastes one surface onto another (e.g., paste corridor surface onto existing ground).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var targetSurfaceHandle = GetParam<string>(parameters, "target_surface_handle");
        var sourceSurfaceHandle = GetParam<string>(parameters, "source_surface_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var targetId = GetObjectIdFromHandle(doc.Database, targetSurfaceHandle);
            var sourceId = GetObjectIdFromHandle(doc.Database, sourceSurfaceHandle);

            if (targetId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Target surface not found."));
            if (sourceId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Source surface not found."));

            var target = tr.GetObject(targetId, OpenMode.ForWrite) as TinSurface;
            if (target == null) return Task.FromResult(ToolResult.Fail(Name, "Target handle is not a TIN Surface."));

            target.PasteSurface(sourceId);
            target.Rebuild();
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Surface pasted onto '{target.Name}' successfully."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "target_surface_handle")))
            return ValidationResult.Invalid("target_surface_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "source_surface_handle")))
            return ValidationResult.Invalid("source_surface_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["target_surface_handle", "source_surface_handle"],
        "properties": {
            "target_surface_handle": { "type": "string", "description": "Handle of surface to paste into" },
            "source_surface_handle": { "type": "string", "description": "Handle of surface to paste from" }
        }
    }
    """);

}

public sealed class ExtractSurfaceContoursTool : CadToolBase
{
    public override string Name => "ExtractSurfaceContours";
    public override string Description => "Extracts contour data from a surface at specified intervals.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");
        var majorInterval = GetParam(parameters, "major_interval", 5.0);
        var minorInterval = GetParam(parameters, "minor_interval", 1.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
            if (surfId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

            var surface = tr.GetObject(surfId, OpenMode.ForRead) as TinSurface;
            if (surface == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a TIN Surface."));

            var props = surface.GetGeneralProperties();
            double minElev = props.MinimumElevation;
            double maxElev = props.MaximumElevation;

            // Calculate contour levels
            var majorContours = new List<double>();
            var minorContours = new List<double>();

            for (double e = Math.Ceiling(minElev / majorInterval) * majorInterval; e <= maxElev; e += majorInterval)
                majorContours.Add(e);

            for (double e = Math.Ceiling(minElev / minorInterval) * minorInterval; e <= maxElev; e += minorInterval)
            {
                if (!majorContours.Contains(e))
                    minorContours.Add(e);
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Surface '{surface.Name}' contours: {majorContours.Count} major ({majorInterval}m), {minorContours.Count} minor ({minorInterval}m). Elevation range: {minElev:F2} - {maxElev:F2}.",
                new Dictionary<string, object>
                {
                    ["min_elevation"] = minElev,
                    ["max_elevation"] = maxElev,
                    ["major_interval"] = majorInterval,
                    ["minor_interval"] = minorInterval,
                    ["major_contour_elevations"] = majorContours,
                    ["minor_contour_count"] = minorContours.Count
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
        "properties": {
            "surface_handle": { "type": "string" },
            "major_interval": { "type": "number", "default": 5.0, "description": "Major contour interval" },
            "minor_interval": { "type": "number", "default": 1.0, "description": "Minor contour interval" }
        }
    }
    """);

}
