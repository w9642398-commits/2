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

// ─── Grading Tools ───────────────────────────────────────────────────────────

public sealed class CreateGradingGroupTool : CadToolBase
{
    public override string Name => "CreateGradingGroup";
    public override string Description => "Creates a grading group associated with a surface for automatic surface updates.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var groupName = GetParam<string>(parameters, "name");
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;

            var surfId = ObjectId.Null;
            if (!string.IsNullOrEmpty(surfaceHandle))
                surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);

            // Grading groups are site-based; use the first site or create one
            ObjectId siteId;
            var siteIds = civilDoc.GetSiteIds();
            if (siteIds.Count > 0)
            {
                siteId = siteIds[0];
            }
            else
            {
                siteId = Site.Create(civilDoc, "Default Site");
            }

            var gradingGroupId = GradingGroup.Create(groupName, siteId, surfId);

            var group = tr.GetObject(gradingGroupId, OpenMode.ForRead) as GradingGroup;
            var handle = group?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Grading group '{groupName}' created. Handle: {handle}");
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
            "name": { "type": "string", "description": "Grading group name" },
            "surface_handle": { "type": "string", "description": "Handle of target surface for automatic updates" }
        }
    }
    """);

}

public sealed class CreateGradingBySlope : CadToolBase
{
    public override string Name => "CreateGradingBySlope";
    public override string Description => "Creates a grading object from a feature line using a slope target (cut/fill slope ratio).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var featureLineHandle = GetParam<string>(parameters, "feature_line_handle");
        var gradingGroupHandle = GetParam<string>(parameters, "grading_group_handle");
        var cutSlope = GetParam(parameters, "cut_slope", 2.0);
        var fillSlope = GetParam(parameters, "fill_slope", 3.0);
        var targetSurfaceHandle = GetParam<string>(parameters, "target_surface_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var flId = GetObjectIdFromHandle(doc.Database, featureLineHandle);
            if (flId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Feature line not found."));

            var ggId = GetObjectIdFromHandle(doc.Database, gradingGroupHandle);
            if (ggId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Grading group not found."));

            var targetId = ObjectId.Null;
            if (!string.IsNullOrEmpty(targetSurfaceHandle))
                targetId = GetObjectIdFromHandle(doc.Database, targetSurfaceHandle);

            var gradingGroup = tr.GetObject(ggId, OpenMode.ForWrite) as GradingGroup;
            if (gradingGroup == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a GradingGroup."));

            // Create grading with slope criteria
            var gradingId = Grading.Create(
                flId,
                ggId,
                GradingTargetType.Surface,
                targetId,
                GradingSlopeFormatType.SlopeRatio,
                cutSlope,
                GradingSlopeFormatType.SlopeRatio,
                fillSlope);

            var grading = tr.GetObject(gradingId, OpenMode.ForRead) as Grading;
            var handle = grading?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Grading created with cut slope 1:{cutSlope}, fill slope 1:{fillSlope}. Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "feature_line_handle")))
            return ValidationResult.Invalid("feature_line_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "grading_group_handle")))
            return ValidationResult.Invalid("grading_group_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["feature_line_handle", "grading_group_handle"],
        "properties": {
            "feature_line_handle": { "type": "string", "description": "Handle of the source feature line" },
            "grading_group_handle": { "type": "string", "description": "Handle of the grading group" },
            "cut_slope": { "type": "number", "default": 2.0, "description": "Cut slope ratio (e.g., 2.0 = 1:2)" },
            "fill_slope": { "type": "number", "default": 3.0, "description": "Fill slope ratio (e.g., 3.0 = 1:3)" },
            "target_surface_handle": { "type": "string", "description": "Handle of target surface" }
        }
    }
    """);

}

public sealed class CreateGradingByDistanceTool : CadToolBase
{
    public override string Name => "CreateGradingByDistance";
    public override string Description => "Creates a grading object using a fixed horizontal distance and slope.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var featureLineHandle = GetParam<string>(parameters, "feature_line_handle");
        var gradingGroupHandle = GetParam<string>(parameters, "grading_group_handle");
        var distance = GetParam(parameters, "distance", 10.0);
        var slope = GetParam(parameters, "slope", -2.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var flId = GetObjectIdFromHandle(doc.Database, featureLineHandle);
            if (flId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Feature line not found."));

            var ggId = GetObjectIdFromHandle(doc.Database, gradingGroupHandle);
            if (ggId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Grading group not found."));

            var gradingId = Grading.Create(
                flId,
                ggId,
                GradingTargetType.Distance,
                ObjectId.Null,
                GradingSlopeFormatType.SlopeRatio,
                Math.Abs(slope),
                GradingSlopeFormatType.SlopeRatio,
                Math.Abs(slope),
                distance);

            var grading = tr.GetObject(gradingId, OpenMode.ForRead) as Grading;
            var handle = grading?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Grading created with distance={distance}, slope=1:{Math.Abs(slope)}. Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "feature_line_handle")))
            return ValidationResult.Invalid("feature_line_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "grading_group_handle")))
            return ValidationResult.Invalid("grading_group_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["feature_line_handle", "grading_group_handle"],
        "properties": {
            "feature_line_handle": { "type": "string" },
            "grading_group_handle": { "type": "string" },
            "distance": { "type": "number", "default": 10.0, "description": "Horizontal projection distance" },
            "slope": { "type": "number", "default": -2.0, "description": "Grade slope (negative = downward)" }
        }
    }
    """);

}

public sealed class ModifyFeatureLineElevationsTool : CadToolBase
{
    public override string Name => "ModifyFeatureLineElevations";
    public override string Description => "Modifies elevations on a feature line: set all, raise/lower, set from surface, or set individual points.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var featureLineHandle = GetParam<string>(parameters, "feature_line_handle");
        var operation = GetParam(parameters, "operation", "set_all");
        var elevation = GetParam(parameters, "elevation", 0.0);
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var flId = GetObjectIdFromHandle(doc.Database, featureLineHandle);
            if (flId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Feature line not found."));

            var fl = tr.GetObject(flId, OpenMode.ForWrite) as FeatureLine;
            if (fl == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a FeatureLine."));

            string resultMsg;

            switch (operation.ToLower())
            {
                case "set_all":
                    for (int i = 0; i < fl.GetPoints(FeatureLinePointType.AllPoints).Count; i++)
                    {
                        fl.SetPointElevation(i, elevation);
                    }
                    resultMsg = $"Set all points to elevation {elevation:F3}.";
                    break;

                case "raise":
                    for (int i = 0; i < fl.GetPoints(FeatureLinePointType.AllPoints).Count; i++)
                    {
                        var current = fl.GetPoints(FeatureLinePointType.AllPoints)[i].Z;
                        fl.SetPointElevation(i, current + elevation);
                    }
                    resultMsg = $"Raised all points by {elevation:F3}.";
                    break;

                case "lower":
                    for (int i = 0; i < fl.GetPoints(FeatureLinePointType.AllPoints).Count; i++)
                    {
                        var current = fl.GetPoints(FeatureLinePointType.AllPoints)[i].Z;
                        fl.SetPointElevation(i, current - elevation);
                    }
                    resultMsg = $"Lowered all points by {elevation:F3}.";
                    break;

                case "from_surface":
                    if (string.IsNullOrEmpty(surfaceHandle))
                        return Task.FromResult(ToolResult.Fail(Name, "surface_handle required for from_surface operation."));

                    var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
                    if (surfId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

                    fl.AssignElevationsFromSurface(surfId);
                    resultMsg = "Elevations assigned from surface.";
                    break;

                default:
                    return Task.FromResult(ToolResult.Fail(Name, $"Unknown operation: {operation}"));
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Feature line '{fl.Name}' modified: {resultMsg}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "feature_line_handle")))
            return ValidationResult.Invalid("feature_line_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["feature_line_handle"],
        "properties": {
            "feature_line_handle": { "type": "string" },
            "operation": { "type": "string", "enum": ["set_all", "raise", "lower", "from_surface"], "default": "set_all" },
            "elevation": { "type": "number", "default": 0.0, "description": "Target elevation or delta value" },
            "surface_handle": { "type": "string", "description": "Surface handle (required for from_surface)" }
        }
    }
    """);

}
