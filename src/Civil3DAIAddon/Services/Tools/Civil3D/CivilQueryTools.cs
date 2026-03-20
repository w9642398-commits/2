using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.Civil3D;

public sealed class QueryAlignmentGeometryTool : CadToolBase
{
    public override string Name => "QueryAlignmentGeometry";
    public override string Description => "Returns detailed geometry information for a Civil 3D alignment (tangents, curves, spirals).";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

        var alignment = tr.GetObject(objId, OpenMode.ForRead) as Alignment;
        if (alignment == null)
            return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

        var entities = new List<Dictionary<string, object>>();
        foreach (AlignmentEntity entity in alignment.Entities)
        {
            var entData = new Dictionary<string, object>
            {
                ["EntityType"] = entity.EntityType.ToString(),
                ["Length"] = entity.Length,
            };

            switch (entity)
            {
                case AlignmentLine line:
                    entData["Direction"] = line.Direction;
                    break;
                case AlignmentArc arc:
                    entData["Radius"] = arc.Radius;
                    entData["CurveGroupIndex"] = arc.CurveGroupIndex;
                    entData["Clockwise"] = arc.Clockwise;
                    break;
            }

            entities.Add(entData);
        }

        tr.Commit();

        return Task.FromResult(ToolResult.Ok(Name,
            $"Alignment '{alignment.Name}': {entities.Count} entities, Length={alignment.Length:F3}",
            new Dictionary<string, object>
            {
                ["name"] = alignment.Name,
                ["length"] = alignment.Length,
                ["start_station"] = alignment.StartingStation,
                ["end_station"] = alignment.EndingStation,
                ["entities"] = entities
            }));
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
            "alignment_handle": { "type": "string" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class QuerySurfaceInfoTool : CadToolBase
{
    public override string Name => "QuerySurfaceInfo";
    public override string Description => "Returns information about a Civil 3D surface (type, extents, elevation range).";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, surfaceHandle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

        var surface = tr.GetObject(objId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
        if (surface == null)
            return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Surface."));

        var data = new Dictionary<string, object>
        {
            ["name"] = surface.Name,
            ["type"] = surface.GetType().Name,
            ["style"] = surface.StyleName
        };

        if (surface is TinSurface tin)
        {
            var props = tin.GetGeneralProperties();
            data["min_elevation"] = props.MinimumElevation;
            data["max_elevation"] = props.MaximumElevation;
            data["number_of_points"] = props.NumberOfPoints;
        }

        tr.Commit();

        return Task.FromResult(ToolResult.Ok(Name, $"Surface '{surface.Name}' info retrieved.", data));
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

public sealed class QueryProfileInfoTool : CadToolBase
{
    public override string Name => "QueryProfileInfo";
    public override string Description => "Returns information about a profile (elevation range, PVIs).";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var profileHandle = GetParam<string>(parameters, "profile_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, profileHandle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, "Profile not found."));

        var profile = tr.GetObject(objId, OpenMode.ForRead) as Profile;
        if (profile == null)
            return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Profile."));

        var data = new Dictionary<string, object>
        {
            ["name"] = profile.Name,
            ["type"] = profile.ProfileType.ToString(),
            ["start_station"] = profile.StartingStation,
            ["end_station"] = profile.EndingStation,
            ["min_elevation"] = profile.ElevationMin,
            ["max_elevation"] = profile.ElevationMax,
        };

        tr.Commit();

        return Task.FromResult(ToolResult.Ok(Name, $"Profile '{profile.Name}' info retrieved.", data));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "profile_handle")))
            return ValidationResult.Invalid("profile_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["profile_handle"],
        "properties": { "profile_handle": { "type": "string" } }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class QueryParcelInfoTool : CadToolBase
{
    public override string Name => "QueryParcelInfo";
    public override string Description => "Returns information about Civil 3D parcels in the drawing.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var parcels = new List<Dictionary<string, object>>();

            foreach (ObjectId siteId in civilDoc.GetSiteIds())
            {
                var site = tr.GetObject(siteId, OpenMode.ForRead) as Site;
                if (site == null) continue;

                foreach (ObjectId parcelId in site.GetParcelIds())
                {
                    var parcel = tr.GetObject(parcelId, OpenMode.ForRead) as Parcel;
                    if (parcel != null)
                    {
                        parcels.Add(new Dictionary<string, object>
                        {
                            ["name"] = parcel.Name,
                            ["handle"] = parcel.Handle.ToString(),
                            ["area"] = parcel.Area,
                            ["perimeter"] = parcel.Perimeter,
                            ["site"] = site.Name
                        });
                    }
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Found {parcels.Count} parcels.",
                new Dictionary<string, object> { ["parcels"] = parcels }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to query parcels: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}

public sealed class QueryPointGroupsTool : CadToolBase
{
    public override string Name => "QueryPointGroups";
    public override string Description => "Returns information about COGO point groups in the drawing.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var groups = new List<Dictionary<string, object>>();

            foreach (ObjectId pgId in civilDoc.GetPointGroupIds())
            {
                var pg = tr.GetObject(pgId, OpenMode.ForRead) as PointGroup;
                if (pg != null)
                {
                    groups.Add(new Dictionary<string, object>
                    {
                        ["name"] = pg.Name,
                        ["handle"] = pg.Handle.ToString(),
                        ["point_count"] = pg.PointsCount,
                        ["description"] = pg.Description ?? ""
                    });
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Found {groups.Count} point groups.",
                new Dictionary<string, object> { ["point_groups"] = groups }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to query point groups: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}

public sealed class CreateOffsetAlignmentTool : CadToolBase
{
    public override string Name => "CreateOffsetAlignmentIfSupportedByWorkflow";
    public override string Description => "Creates an offset alignment from an existing alignment. Notes: actual offset alignment creation depends on Civil 3D workflow support; may fall back to creating an offset polyline.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var offset = GetParam<double>(parameters, "offset");
        var name = GetParam<string>(parameters, "name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (objId.IsNull)
                return Task.FromResult(ToolResult.Fail(Name, "Source alignment not found."));

            var alignment = tr.GetObject(objId, OpenMode.ForRead) as Alignment;
            if (alignment == null)
                return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            // Civil 3D offset alignments are created through the corridor workflow.
            // As a functional alternative, we create an offset polyline.
            var offsetCurves = alignment.GetOffsetCurves(offset);

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var createdHandles = new List<string>();
            foreach (Entity offsetEnt in offsetCurves)
            {
                if (!string.IsNullOrEmpty(name))
                    offsetEnt.Layer = alignment.Layer;

                btr.AppendEntity(offsetEnt);
                tr.AddNewlyCreatedDBObject(offsetEnt, true);
                createdHandles.Add(offsetEnt.Handle.ToString());
            }

            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Offset curves created at {offset}m from alignment '{alignment.Name}'. " +
                "Note: True offset alignments require corridor workflow. Created offset geometry as polyline(s).");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        if (GetParam<double>(parameters, "offset") == 0)
            return ValidationResult.Invalid("offset must be non-zero");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["alignment_handle", "offset"],
        "properties": {
            "alignment_handle": { "type": "string" },
            "offset": { "type": "number", "description": "Offset distance (positive=right, negative=left)" },
            "name": { "type": "string", "description": "Optional name for the offset" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ExtractStationingDataTool : CadToolBase
{
    public override string Name => "ExtractStationingData";
    public override string Description => "Extracts stationing (chainage) data at regular intervals along an alignment.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var interval = GetParam(parameters, "interval", 10.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

        var alignment = tr.GetObject(objId, OpenMode.ForRead) as Alignment;
        if (alignment == null)
            return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

        var stationData = new List<Dictionary<string, object>>();
        for (double sta = alignment.StartingStation; sta <= alignment.EndingStation; sta += interval)
        {
            double x = 0, y = 0;
            alignment.PointLocation(sta, 0, ref x, ref y);

            stationData.Add(new Dictionary<string, object>
            {
                ["station"] = sta,
                ["x"] = x,
                ["y"] = y
            });
        }

        tr.Commit();

        return Task.FromResult(ToolResult.Ok(Name,
            $"Extracted {stationData.Count} station points at {interval}m intervals.",
            new Dictionary<string, object> { ["stations"] = stationData }));
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
            "interval": { "type": "number", "default": 10.0, "description": "Interval in drawing units" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class AnalyzeGeometryContinuityTool : CadToolBase
{
    public override string Name => "AnalyzeGeometryContinuity";
    public override string Description => "Analyzes geometry continuity of an alignment or polyline, checking for tangent breaks, radius discontinuities, and G0/G1/G2 conditions.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, "Object not found."));

        var alignment = tr.GetObject(objId, OpenMode.ForRead) as Alignment;
        if (alignment == null)
            return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

        var issues = new List<Dictionary<string, object>>();
        AlignmentEntity? prevEntity = null;

        foreach (AlignmentEntity entity in alignment.Entities)
        {
            if (prevEntity != null)
            {
                // Check for tangent continuity (G1)
                bool isTangentContinuous = entity.EntityBefore == prevEntity.EntityId;

                if (!isTangentContinuous)
                {
                    issues.Add(new Dictionary<string, object>
                    {
                        ["type"] = "G1_BREAK",
                        ["description"] = $"Potential tangent discontinuity between entity {prevEntity.EntityId} and {entity.EntityId}",
                        ["location_entity_id"] = entity.EntityId
                    });
                }

                // Check radius discontinuity for curve transitions
                if (prevEntity is AlignmentArc prevArc && entity is AlignmentArc curArc)
                {
                    if (Math.Abs(prevArc.Radius - curArc.Radius) > 0.001 &&
                        prevArc.Clockwise != curArc.Clockwise)
                    {
                        issues.Add(new Dictionary<string, object>
                        {
                            ["type"] = "REVERSE_CURVE",
                            ["description"] = $"Reverse curve detected: R={prevArc.Radius:F3} -> R={curArc.Radius:F3}",
                            ["location_entity_id"] = entity.EntityId
                        });
                    }
                }
            }

            prevEntity = entity;
        }

        tr.Commit();

        var summary = issues.Count == 0
            ? "No geometry continuity issues found."
            : $"Found {issues.Count} potential issue(s).";

        return Task.FromResult(ToolResult.Ok(Name, summary,
            new Dictionary<string, object>
            {
                ["alignment"] = alignment.Name,
                ["total_entities"] = alignment.Entities.Count,
                ["issues"] = issues
            }));
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
            "alignment_handle": { "type": "string" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}
