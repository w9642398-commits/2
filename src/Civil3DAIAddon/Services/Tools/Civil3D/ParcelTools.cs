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

// ─── Parcel Tools ────────────────────────────────────────────────────────────

public sealed class CreateSiteTool : CadToolBase
{
    public override string Name => "CreateSite";
    public override string Description => "Creates a Civil 3D site for organizing parcels, alignments, and grading groups.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var siteName = GetParam<string>(parameters, "name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var siteId = Site.Create(civilDoc, siteName);

            var site = tr.GetObject(siteId, OpenMode.ForRead) as Site;
            var handle = site?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Site '{siteName}' created. Handle: {handle}");
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
            "name": { "type": "string", "description": "Site name" }
        }
    }
    """);
}

public sealed class CreateParcelBySegmentsTool : CadToolBase
{
    public override string Name => "CreateParcelBySegments";
    public override string Description => "Creates a parcel from a closed polyline boundary within a site.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var siteHandle = GetParam<string>(parameters, "site_handle");
        var polylineHandle = GetParam<string>(parameters, "polyline_handle");
        var parcelName = GetParam(parameters, "name", "Parcel");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var siteId = GetObjectIdFromHandle(doc.Database, siteHandle);
            if (siteId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Site not found."));

            var polyId = GetObjectIdFromHandle(doc.Database, polylineHandle);
            if (polyId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Polyline not found."));

            var civilDoc = CivilApplication.ActiveDocument;
            var styleId = civilDoc.Styles.ParcelStyles[0];
            var labelStyleId = civilDoc.Styles.LabelStyles.ParcelLabelStyles.AreaLabelStyles[0];

            // Create parcel from polyline entity
            var parcelIds = Parcel.CreateFromEntities(
                civilDoc,
                siteId,
                new ObjectIdCollection { polyId },
                parcelName,
                styleId,
                labelStyleId);

            var createdHandles = new List<string>();
            foreach (ObjectId pid in parcelIds)
            {
                var p = tr.GetObject(pid, OpenMode.ForRead) as Parcel;
                if (p != null) createdHandles.Add(p.Handle.ToString());
            }

            tr.Commit();

            var result = ToolResult.Ok(Name, $"Created {createdHandles.Count} parcel(s) in site.");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "site_handle")))
            return ValidationResult.Invalid("site_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "polyline_handle")))
            return ValidationResult.Invalid("polyline_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["site_handle", "polyline_handle"],
        "properties": {
            "site_handle": { "type": "string" },
            "polyline_handle": { "type": "string", "description": "Handle of closed polyline" },
            "name": { "type": "string", "default": "Parcel" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class QueryParcelDetailsTool : CadToolBase
{
    public override string Name => "QueryParcelDetails";
    public override string Description => "Returns detailed information about a specific parcel (area, perimeter, segments, label data).";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var parcelHandle = GetParam<string>(parameters, "parcel_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var parcelId = GetObjectIdFromHandle(doc.Database, parcelHandle);
            if (parcelId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Parcel not found."));

            var parcel = tr.GetObject(parcelId, OpenMode.ForRead) as Parcel;
            if (parcel == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Parcel."));

            var segments = new List<Dictionary<string, object>>();
            foreach (ParcelSegment seg in parcel.Segments)
            {
                segments.Add(new Dictionary<string, object>
                {
                    ["type"] = seg.EntityType.ToString(),
                    ["length"] = seg.Length,
                    ["direction"] = seg.Direction
                });
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Parcel '{parcel.Name}': area={parcel.Area:F2}m², perimeter={parcel.Perimeter:F2}m.",
                new Dictionary<string, object>
                {
                    ["name"] = parcel.Name,
                    ["handle"] = parcel.Handle.ToString(),
                    ["area"] = parcel.Area,
                    ["perimeter"] = parcel.Perimeter,
                    ["number"] = parcel.Number,
                    ["segments"] = segments,
                    ["segment_count"] = segments.Count
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "parcel_handle")))
            return ValidationResult.Invalid("parcel_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["parcel_handle"],
        "properties": { "parcel_handle": { "type": "string" } }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class RenumberParcelsTool : CadToolBase
{
    public override string Name => "RenumberParcels";
    public override string Description => "Renumbers all parcels in a site starting from a specified number.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var siteHandle = GetParam<string>(parameters, "site_handle");
        var startNumber = GetParam(parameters, "start_number", 1);
        var prefix = GetParam(parameters, "prefix", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var siteId = GetObjectIdFromHandle(doc.Database, siteHandle);
            if (siteId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Site not found."));

            var site = tr.GetObject(siteId, OpenMode.ForRead) as Site;
            if (site == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Site."));

            int number = startNumber;
            int count = 0;
            foreach (ObjectId parcelId in site.GetParcelIds())
            {
                var parcel = tr.GetObject(parcelId, OpenMode.ForWrite) as Parcel;
                if (parcel != null)
                {
                    parcel.Number = number;
                    if (!string.IsNullOrEmpty(prefix))
                        parcel.Name = $"{prefix}{number}";
                    number++;
                    count++;
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Renumbered {count} parcels starting from {startNumber}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "site_handle")))
            return ValidationResult.Invalid("site_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["site_handle"],
        "properties": {
            "site_handle": { "type": "string" },
            "start_number": { "type": "integer", "default": 1 },
            "prefix": { "type": "string", "default": "", "description": "Name prefix (e.g., 'Lot ')" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}
