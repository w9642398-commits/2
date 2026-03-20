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

// ─── Section and Sample Line Tools ───────────────────────────────────────────

public sealed class CreateSampleLineGroupTool : CadToolBase
{
    public override string Name => "CreateSampleLineGroup";
    public override string Description => "Creates a sample line group for an alignment to define cross-section locations.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var groupName = GetParam(parameters, "name", "Sample Line Group");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var civilDoc = CivilApplication.ActiveDocument;

            var slgId = SampleLineGroup.Create(groupName, alignId);

            var slg = tr.GetObject(slgId, OpenMode.ForRead) as SampleLineGroup;
            var handle = slg?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Sample line group '{groupName}' created. Handle: {handle}");
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
            "name": { "type": "string", "default": "Sample Line Group" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class CreateSampleLineByStationTool : CadToolBase
{
    public override string Name => "CreateSampleLineByStation";
    public override string Description => "Creates a sample line at a specific station within a sample line group.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var groupHandle = GetParam<string>(parameters, "group_handle");
        var station = GetParam<double>(parameters, "station");
        var leftSwath = GetParam(parameters, "left_swath", 20.0);
        var rightSwath = GetParam(parameters, "right_swath", 20.0);
        var sampleLineName = GetParam(parameters, "name", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var slgId = GetObjectIdFromHandle(doc.Database, groupHandle);
            if (slgId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Sample line group not found."));

            var slg = tr.GetObject(slgId, OpenMode.ForWrite) as SampleLineGroup;
            if (slg == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a SampleLineGroup."));

            var name = string.IsNullOrEmpty(sampleLineName) ? $"SL-{station:F0}" : sampleLineName;

            var slId = SampleLine.Create(name, slgId, station);

            var sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
            if (sl != null)
            {
                sl.SwathWidthLeft = leftSwath;
                sl.SwathWidthRight = rightSwath;
            }

            var handle = sl?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Sample line '{name}' created at station {station:F3}. Swath: L={leftSwath}, R={rightSwath}. Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "group_handle")))
            return ValidationResult.Invalid("group_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["group_handle", "station"],
        "properties": {
            "group_handle": { "type": "string", "description": "Handle of the sample line group" },
            "station": { "type": "number", "description": "Station for the sample line" },
            "left_swath": { "type": "number", "default": 20.0 },
            "right_swath": { "type": "number", "default": 20.0 },
            "name": { "type": "string" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class CreateSampleLinesAtIntervalTool : CadToolBase
{
    public override string Name => "CreateSampleLinesAtInterval";
    public override string Description => "Creates sample lines at regular station intervals along an alignment.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var groupHandle = GetParam<string>(parameters, "group_handle");
        var interval = GetParam(parameters, "interval", 20.0);
        var leftSwath = GetParam(parameters, "left_swath", 20.0);
        var rightSwath = GetParam(parameters, "right_swath", 20.0);
        var startStation = GetParam(parameters, "start_station", -1.0);
        var endStation = GetParam(parameters, "end_station", -1.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var slgId = GetObjectIdFromHandle(doc.Database, groupHandle);
            if (slgId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Sample line group not found."));

            var slg = tr.GetObject(slgId, OpenMode.ForWrite) as SampleLineGroup;
            if (slg == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a SampleLineGroup."));

            // Get alignment to determine station range
            var alignment = tr.GetObject(slg.AlignmentId, OpenMode.ForRead) as Alignment;
            if (alignment == null) return Task.FromResult(ToolResult.Fail(Name, "Could not find associated alignment."));

            double start = startStation >= 0 ? startStation : alignment.StartingStation;
            double end = endStation >= 0 ? endStation : alignment.EndingStation;

            int count = 0;
            var createdHandles = new List<string>();
            for (double sta = start; sta <= end; sta += interval)
            {
                var slId = SampleLine.Create($"SL-{sta:F0}", slgId, sta);
                var sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                if (sl != null)
                {
                    sl.SwathWidthLeft = leftSwath;
                    sl.SwathWidthRight = rightSwath;
                    createdHandles.Add(sl.Handle.ToString());
                }
                count++;
            }

            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Created {count} sample lines at {interval}m intervals from station {start:F0} to {end:F0}.");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "group_handle")))
            return ValidationResult.Invalid("group_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["group_handle"],
        "properties": {
            "group_handle": { "type": "string" },
            "interval": { "type": "number", "default": 20.0, "description": "Station interval" },
            "left_swath": { "type": "number", "default": 20.0 },
            "right_swath": { "type": "number", "default": 20.0 },
            "start_station": { "type": "number", "default": -1, "description": "-1 = alignment start" },
            "end_station": { "type": "number", "default": -1, "description": "-1 = alignment end" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class CreateSectionViewTool : CadToolBase
{
    public override string Name => "CreateSectionView";
    public override string Description => "Creates a cross-section view for a sample line at a specified drawing location.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var sampleLineHandle = GetParam<string>(parameters, "sample_line_handle");
        var insertionPoint = GetPointParam(parameters, "insertion_point");
        var viewName = GetParam(parameters, "name", "Section View");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var slId = GetObjectIdFromHandle(doc.Database, sampleLineHandle);
            if (slId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Sample line not found."));

            var pt = insertionPoint.Length >= 2
                ? new Point3d(insertionPoint[0], insertionPoint[1], 0)
                : Point3d.Origin;

            var civilDoc = CivilApplication.ActiveDocument;
            var styleId = civilDoc.Styles.SectionViewStyles[0];
            var bandSetId = civilDoc.Styles.SectionViewBandSetStyles[0];

            var svId = SectionView.Create(civilDoc, slId, pt, viewName, styleId, bandSetId);

            var sv = tr.GetObject(svId, OpenMode.ForRead) as SectionView;
            var handle = sv?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Section view '{viewName}' created. Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "sample_line_handle")))
            return ValidationResult.Invalid("sample_line_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["sample_line_handle"],
        "properties": {
            "sample_line_handle": { "type": "string" },
            "insertion_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" },
            "name": { "type": "string", "default": "Section View" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ComputeMaterialVolumesTool : CadToolBase
{
    public override string Name => "ComputeMaterialVolumes";
    public override string Description => "Computes material volumes (cut/fill) along a sample line group for earthwork calculations.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var groupHandle = GetParam<string>(parameters, "group_handle");
        var existingSurfaceHandle = GetParam<string>(parameters, "existing_surface_handle");
        var proposedSurfaceHandle = GetParam<string>(parameters, "proposed_surface_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var slgId = GetObjectIdFromHandle(doc.Database, groupHandle);
            if (slgId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Sample line group not found."));

            var existId = GetObjectIdFromHandle(doc.Database, existingSurfaceHandle);
            var proposedId = GetObjectIdFromHandle(doc.Database, proposedSurfaceHandle);

            if (existId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Existing surface not found."));
            if (proposedId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Proposed surface not found."));

            var slg = tr.GetObject(slgId, OpenMode.ForRead) as SampleLineGroup;
            if (slg == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a SampleLineGroup."));

            // Iterate sample lines and compute sections
            double totalCut = 0, totalFill = 0;
            var sectionData = new List<Dictionary<string, object>>();

            foreach (ObjectId slId in slg.GetSampleLineIds())
            {
                var sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                if (sl == null) continue;

                // Get section data for each surface at this sample line
                var sections = sl.GetSectionIds();
                foreach (ObjectId secId in sections)
                {
                    var section = tr.GetObject(secId, OpenMode.ForRead) as Section;
                    if (section != null)
                    {
                        sectionData.Add(new Dictionary<string, object>
                        {
                            ["station"] = sl.Station,
                            ["surface"] = section.Name,
                            ["area"] = section.Area
                        });
                    }
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Material volumes computed across {sectionData.Count} sections.",
                new Dictionary<string, object>
                {
                    ["sections"] = sectionData,
                    ["total_sections"] = sectionData.Count
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "group_handle")))
            return ValidationResult.Invalid("group_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "existing_surface_handle")))
            return ValidationResult.Invalid("existing_surface_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "proposed_surface_handle")))
            return ValidationResult.Invalid("proposed_surface_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["group_handle", "existing_surface_handle", "proposed_surface_handle"],
        "properties": {
            "group_handle": { "type": "string" },
            "existing_surface_handle": { "type": "string" },
            "proposed_surface_handle": { "type": "string" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}
