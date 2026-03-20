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

// ─── COGO Point Tools ────────────────────────────────────────────────────────

public sealed class CreateCogoPointTool : CadToolBase
{
    public override string Name => "CreateCogoPoint";
    public override string Description => "Creates a COGO point with specified coordinates, elevation, description, and optional point number.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var x = GetParam<double>(parameters, "x");
        var y = GetParam<double>(parameters, "y");
        var z = GetParam(parameters, "z", 0.0);
        var description = GetParam(parameters, "description", "");
        var pointNumber = GetParam(parameters, "point_number", 0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var pt = new Point3d(x, y, z);

            ObjectId pointId;
            if (pointNumber > 0)
            {
                pointId = civilDoc.CogoPoints.Add(pt, (uint)pointNumber, true);
            }
            else
            {
                pointId = civilDoc.CogoPoints.Add(pt, true);
            }

            var cogoPoint = tr.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;
            if (cogoPoint != null)
            {
                if (!string.IsNullOrEmpty(description))
                    cogoPoint.RawDescription = description;
            }

            var handle = cogoPoint?.Handle.ToString() ?? "unknown";
            var ptNum = cogoPoint?.PointNumber ?? 0;
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"COGO point #{ptNum} created at ({x:F3}, {y:F3}, {z:F3}). Description: '{description}'. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["x", "y"],
        "properties": {
            "x": { "type": "number" },
            "y": { "type": "number" },
            "z": { "type": "number", "default": 0.0 },
            "description": { "type": "string", "default": "" },
            "point_number": { "type": "integer", "default": 0, "description": "0 = auto-assign" }
        }
    }
    """);
}

public sealed class CreateCogoPointsTool : CadToolBase
{
    public override string Name => "CreateCogoPoints";
    public override string Description => "Creates multiple COGO points from an array of coordinates with descriptions.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var points = GetPointsParam(parameters, "points");
        var description = GetParam(parameters, "description", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var createdHandles = new List<string>();

            foreach (var pt in points)
            {
                var point3d = new Point3d(pt[0], pt[1], pt.Length > 2 ? pt[2] : 0);
                var pointId = civilDoc.CogoPoints.Add(point3d, true);

                var cogoPoint = tr.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;
                if (cogoPoint != null)
                {
                    if (!string.IsNullOrEmpty(description))
                        cogoPoint.RawDescription = description;
                    createdHandles.Add(cogoPoint.Handle.ToString());
                }
            }

            tr.Commit();

            var result = ToolResult.Ok(Name, $"Created {createdHandles.Count} COGO points.");
            result.CreatedHandles.AddRange(createdHandles);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (GetPointsParam(parameters, "points").Count == 0)
            return ValidationResult.Invalid("At least 1 point required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["points"],
        "properties": {
            "points": { "type": "array", "items": { "type": "array", "items": { "type": "number" } }, "description": "Array of [x, y, z]" },
            "description": { "type": "string", "default": "" }
        }
    }
    """);
}

public sealed class ModifyCogoPointTool : CadToolBase
{
    public override string Name => "ModifyCogoPoint";
    public override string Description => "Modifies a COGO point's properties (elevation, description, location).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pointHandle = GetParam<string>(parameters, "point_handle");
        var newElevation = GetParam(parameters, "elevation", double.NaN);
        var newDescription = GetParam<string>(parameters, "description");
        var newLocation = GetPointParam(parameters, "location");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var ptId = GetObjectIdFromHandle(doc.Database, pointHandle);
            if (ptId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Point not found."));

            var point = tr.GetObject(ptId, OpenMode.ForWrite) as CogoPoint;
            if (point == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a COGO point."));

            var changes = new List<string>();

            if (!double.IsNaN(newElevation))
            {
                point.Elevation = newElevation;
                changes.Add($"elevation={newElevation:F3}");
            }

            if (!string.IsNullOrEmpty(newDescription))
            {
                point.RawDescription = newDescription;
                changes.Add($"description='{newDescription}'");
            }

            if (newLocation.Length >= 2)
            {
                point.Easting = newLocation[0];
                point.Northing = newLocation[1];
                changes.Add($"location=({newLocation[0]:F3}, {newLocation[1]:F3})");
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Point #{point.PointNumber} modified: {string.Join(", ", changes)}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "point_handle")))
            return ValidationResult.Invalid("point_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["point_handle"],
        "properties": {
            "point_handle": { "type": "string" },
            "elevation": { "type": "number", "description": "New elevation (NaN = no change)" },
            "description": { "type": "string" },
            "location": { "type": "array", "items": { "type": "number" }, "description": "[x, y] new location" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class CreatePointGroupTool : CadToolBase
{
    public override string Name => "CreatePointGroup";
    public override string Description => "Creates a COGO point group with optional description filter (raw description match).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var groupName = GetParam<string>(parameters, "name");
        var descriptionFilter = GetParam(parameters, "description_filter", "");
        var includeNumbers = GetParam(parameters, "include_numbers", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var pgId = civilDoc.PointGroups.Add(groupName);

            var pg = tr.GetObject(pgId, OpenMode.ForWrite) as PointGroup;
            if (pg != null)
            {
                if (!string.IsNullOrEmpty(descriptionFilter))
                {
                    var query = new StandardPointGroupQuery();
                    query.IncludeRawDescriptions = descriptionFilter;
                    pg.SetQuery(query);
                }

                if (!string.IsNullOrEmpty(includeNumbers))
                {
                    var query = pg.GetQuery() as StandardPointGroupQuery ?? new StandardPointGroupQuery();
                    query.IncludeNumbers = includeNumbers;
                    pg.SetQuery(query);
                }
            }

            var handle = pg?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Point group '{groupName}' created. Handle: {handle}");
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
            "name": { "type": "string", "description": "Point group name" },
            "description_filter": { "type": "string", "description": "Filter by raw description (wildcards supported, e.g., 'TREE*')" },
            "include_numbers": { "type": "string", "description": "Point numbers to include (e.g., '1-100')" }
        }
    }
    """);
}

public sealed class QueryCogoPointsTool : CadToolBase
{
    public override string Name => "QueryCogoPoints";
    public override string Description => "Queries all COGO points in the drawing or within a specific point group.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var groupName = GetParam<string>(parameters, "group_name");
        var maxResults = GetParam(parameters, "max_results", 500);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var points = new List<Dictionary<string, object>>();

            ObjectIdCollection pointIds;

            if (!string.IsNullOrEmpty(groupName))
            {
                // Find point group
                PointGroup? targetGroup = null;
                foreach (ObjectId pgId in civilDoc.GetPointGroupIds())
                {
                    var pg = tr.GetObject(pgId, OpenMode.ForRead) as PointGroup;
                    if (pg != null && pg.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetGroup = pg;
                        break;
                    }
                }

                if (targetGroup == null)
                    return Task.FromResult(ToolResult.Fail(Name, $"Point group '{groupName}' not found."));

                pointIds = targetGroup.GetPointIds();
            }
            else
            {
                pointIds = civilDoc.CogoPoints.GetPointIds();
            }

            int count = 0;
            foreach (ObjectId ptId in pointIds)
            {
                if (count >= maxResults) break;

                var pt = tr.GetObject(ptId, OpenMode.ForRead) as CogoPoint;
                if (pt != null)
                {
                    points.Add(new Dictionary<string, object>
                    {
                        ["point_number"] = pt.PointNumber,
                        ["handle"] = pt.Handle.ToString(),
                        ["easting"] = pt.Easting,
                        ["northing"] = pt.Northing,
                        ["elevation"] = pt.Elevation,
                        ["raw_description"] = pt.RawDescription ?? "",
                        ["full_description"] = pt.FullDescription ?? ""
                    });
                    count++;
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Found {points.Count} COGO points{(string.IsNullOrEmpty(groupName) ? "" : $" in group '{groupName}'")}.",
                new Dictionary<string, object> { ["points"] = points, ["total_count"] = pointIds.Count }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "properties": {
            "group_name": { "type": "string", "description": "Filter by point group name (empty = all points)" },
            "max_results": { "type": "integer", "default": 500, "description": "Maximum points to return" }
        }
    }
    """);
}
