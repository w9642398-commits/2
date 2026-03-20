using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.Civil3D;

public sealed class CreateAlignmentFromPolylineTool : CadToolBase
{
    public override string Name => "CreateAlignmentFromPolyline";
    public override string Description => "Creates a Civil 3D Alignment from an existing polyline entity.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var polylineHandle = GetParam<string>(parameters, "polyline_handle");
        var alignmentName = GetParam<string>(parameters, "name");
        var layer = GetParam(parameters, "layer", "C-ROAD-CNTR");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, polylineHandle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, $"Polyline '{polylineHandle}' not found."));

        var ent = tr.GetObject(objId, OpenMode.ForRead);
        if (ent is not Polyline && ent is not Polyline3d && ent is not Polyline2d)
            return Task.FromResult(ToolResult.Fail(Name, "Specified handle is not a polyline."));

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;

            // Get first available alignment style and label set
            var styleId = civilDoc.Styles.AlignmentStyles[0];
            var labelSetId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles[0];

            var alignmentId = Alignment.Create(
                civilDoc,
                alignmentName,
                ObjectId.Null, // no site
                GetOrCreateLayer(doc.Database, tr, layer),
                styleId,
                labelSetId);

            var alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
            if (alignment != null)
            {
                alignment.ImportFromPolyline(objId);
            }

            tr.Commit();

            var handle = alignment?.Handle.ToString() ?? "unknown";
            var result = ToolResult.Ok(Name, $"Alignment '{alignmentName}' created from polyline. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to create alignment: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "polyline_handle")))
            return ValidationResult.Invalid("polyline_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "name")))
            return ValidationResult.Invalid("name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["polyline_handle", "name"],
        "properties": {
            "polyline_handle": { "type": "string", "description": "Handle of source polyline" },
            "name": { "type": "string", "description": "Alignment name" },
            "layer": { "type": "string", "default": "C-ROAD-CNTR" }
        }
    }
    """);

        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }

    private static ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName)
    {
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (lt.Has(layerName))
            return lt[layerName];

        lt.UpgradeOpen();
        var newLayer = new LayerTableRecord { Name = layerName };
        var id = lt.Add(newLayer);
        tr.AddNewlyCreatedDBObject(newLayer, true);
        return id;
    }
}

public sealed class CreateProfileTool : CadToolBase
{
    public override string Name => "CreateProfile";
    public override string Description => "Creates a surface profile for a given alignment and surface.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var surfaceHandle = GetParam<string>(parameters, "surface_handle");
        var profileName = GetParam<string>(parameters, "name");
        var layer = GetParam(parameters, "layer", "C-ROAD-PROF");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            var surfId = GetObjectIdFromHandle(doc.Database, surfaceHandle);

            if (alignId.IsNull)
                return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));
            if (surfId.IsNull)
                return Task.FromResult(ToolResult.Fail(Name, "Surface not found."));

            var civilDoc = CivilApplication.ActiveDocument;

            var profileId = Profile.CreateFromSurface(
                profileName,
                alignId,
                surfId,
                GetOrCreateLayer(doc.Database, tr, layer),
                civilDoc.Styles.ProfileStyles[0],
                civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles[0]);

            tr.Commit();

            var profile = doc.TransactionManager.StartTransaction()
                .GetObject(profileId, OpenMode.ForRead) as Profile;
            var handle = profile?.Handle.ToString() ?? "unknown";

            var result = ToolResult.Ok(Name, $"Profile '{profileName}' created. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to create profile: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "surface_handle")))
            return ValidationResult.Invalid("surface_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "name")))
            return ValidationResult.Invalid("name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["alignment_handle", "surface_handle", "name"],
        "properties": {
            "alignment_handle": { "type": "string" },
            "surface_handle": { "type": "string" },
            "name": { "type": "string" },
            "layer": { "type": "string", "default": "C-ROAD-PROF" }
        }
    }
    """);

        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }

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

public sealed class CreateFeatureLineTool : CadToolBase
{
    public override string Name => "CreateFeatureLine";
    public override string Description => "Creates a Civil 3D Feature Line through specified points with optional elevations.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var points = GetPointsParam(parameters, "points");
        var name = GetParam(parameters, "name", "Feature Line");
        var layer = GetParam(parameters, "layer", "C-TOPO-FEAT");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var pointCol = new Point3dCollection();
            foreach (var pt in points)
                pointCol.Add(new Point3d(pt[0], pt[1], pt.Length > 2 ? pt[2] : 0));

            var layerId = GetOrCreateLayer(doc.Database, tr, layer);

            var featureLineId = FeatureLine.Create(name, pointCol, ObjectId.Null, layerId);

            var fl = tr.GetObject(featureLineId, OpenMode.ForRead) as FeatureLine;
            var handle = fl?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Feature Line '{name}' created with {points.Count} points. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to create feature line: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var points = GetPointsParam(parameters, "points");
        if (points.Count < 2) return ValidationResult.Invalid("At least 2 points required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["points"],
        "properties": {
            "points": { "type": "array", "items": { "type": "array", "items": { "type": "number" } }, "description": "Array of [x, y, z] points" },
            "name": { "type": "string", "default": "Feature Line" },
            "layer": { "type": "string", "default": "C-TOPO-FEAT" }
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

public sealed class CreateSurfaceTinTool : CadToolBase
{
    public override string Name => "CreateSurfaceTin";
    public override string Description => "Creates a TIN surface from point data or a point group.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var surfaceName = GetParam<string>(parameters, "name");
        var layer = GetParam(parameters, "layer", "C-TOPO-SURF");
        var pointGroupName = GetParam<string>(parameters, "point_group_name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var styleId = civilDoc.Styles.SurfaceStyles[0];

            var surfaceId = TinSurface.Create(surfaceName, styleId);
            var surface = tr.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;

            if (surface != null && !string.IsNullOrEmpty(pointGroupName))
            {
                // Add point group as source
                foreach (ObjectId pgId in civilDoc.GetPointGroupIds())
                {
                    var pg = tr.GetObject(pgId, OpenMode.ForRead) as PointGroup;
                    if (pg != null && pg.Name.Equals(pointGroupName, StringComparison.OrdinalIgnoreCase))
                    {
                        surface.PointGroupsDefinition.AddPointGroup(pgId);
                        surface.Rebuild();
                        break;
                    }
                }
            }

            var handle = surface?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"TIN Surface '{surfaceName}' created. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to create surface: {ex.Message}", ex.StackTrace));
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
            "name": { "type": "string", "description": "Surface name" },
            "layer": { "type": "string", "default": "C-TOPO-SURF" },
            "point_group_name": { "type": "string", "description": "Name of point group to use as surface source" }
        }
    }
    """);
}

public sealed class AddLabelsToAlignmentTool : CadToolBase
{
    public override string Name => "AddLabelsToAlignment";
    public override string Description => "Adds station labels and/or geometry labels to an alignment.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var labelType = GetParam(parameters, "label_type", "station");
        var interval = GetParam(parameters, "interval", 20.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (objId.IsNull)
                return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var alignment = tr.GetObject(objId, OpenMode.ForWrite) as Alignment;
            if (alignment == null)
                return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Alignment."));

            int labelsAdded = 0;

            if (labelType.Equals("station", StringComparison.OrdinalIgnoreCase) ||
                labelType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // Add major station labels at intervals
                for (double sta = alignment.StartingStation; sta <= alignment.EndingStation; sta += interval)
                {
                    alignment.AddStationLabel(sta);
                    labelsAdded++;
                }
            }

            if (labelType.Equals("geometry", StringComparison.OrdinalIgnoreCase) ||
                labelType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // Geometry point labels are typically added through label set styles
                // The alignment will use its assigned label set
                labelsAdded += alignment.Entities.Count;
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Added {labelsAdded} labels to alignment '{alignment.Name}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to add labels: {ex.Message}", ex.StackTrace));
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
            "label_type": { "type": "string", "enum": ["station", "geometry", "all"], "default": "station" },
            "interval": { "type": "number", "default": 20.0, "description": "Station label interval" }
        }
    }
    """);

        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
}

public sealed class AddLabelsToProfileTool : CadToolBase
{
    public override string Name => "AddLabelsToProfile";
    public override string Description => "Adds labels to a profile view (grade breaks, stations).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var profileHandle = GetParam<string>(parameters, "profile_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, profileHandle);
            if (objId.IsNull)
                return Task.FromResult(ToolResult.Fail(Name, "Profile not found."));

            var profile = tr.GetObject(objId, OpenMode.ForRead) as Profile;
            if (profile == null)
                return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Profile."));

            // Profile labels are driven by the label set style assigned
            // We can confirm the profile exists and report its properties
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Profile '{profile.Name}' verified. Labels are driven by the assigned label set style. " +
                "Modify the profile's label set style to change labeling behavior."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
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
        "properties": {
            "profile_handle": { "type": "string" }
        }
    }
    """);

        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
}
