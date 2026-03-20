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

// ─── Profile Advanced Tools ──────────────────────────────────────────────────

public sealed class CreateLayoutProfileTool : CadToolBase
{
    public override string Name => "CreateLayoutProfile";
    public override string Description => "Creates a layout (design) profile with PVI points for vertical alignment design.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var profileName = GetParam<string>(parameters, "name");
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var layer = GetParam(parameters, "layer", "C-ROAD-PROF");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);
            var styleId = civilDoc.Styles.ProfileStyles[0];
            var labelSetId = civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles[0];

            var profileId = Profile.CreateByLayout(
                profileName,
                alignId,
                layerId,
                styleId,
                labelSetId);

            var profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
            var handle = profile?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Layout profile '{profileName}' created. Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name", "alignment_handle"],
        "properties": {
            "name": { "type": "string" },
            "alignment_handle": { "type": "string" },
            "layer": { "type": "string", "default": "C-ROAD-PROF" }
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

public sealed class AddPVIToProfileTool : CadToolBase
{
    public override string Name => "AddPVIToProfile";
    public override string Description => "Adds a PVI (Point of Vertical Intersection) to a layout profile.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var profileHandle = GetParam<string>(parameters, "profile_handle");
        var station = GetParam<double>(parameters, "station");
        var elevation = GetParam<double>(parameters, "elevation");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var profId = GetObjectIdFromHandle(doc.Database, profileHandle);
            if (profId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Profile not found."));

            var profile = tr.GetObject(profId, OpenMode.ForWrite) as Profile;
            if (profile == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Profile."));

            if (profile.ProfileType != ProfileType.FGProfile)
                return Task.FromResult(ToolResult.Fail(Name, "PVI can only be added to layout (FG) profiles."));

            profile.PVIs.AddPVI(new Point2d(station, elevation));
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"PVI added at station {station:F3}, elevation {elevation:F3}."));
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
        "required": ["profile_handle", "station", "elevation"],
        "properties": {
            "profile_handle": { "type": "string" },
            "station": { "type": "number", "description": "Station (chainage) of the PVI" },
            "elevation": { "type": "number", "description": "Elevation of the PVI" }
        }
    }
    """);

}

public sealed class AddVerticalCurveTool : CadToolBase
{
    public override string Name => "AddVerticalCurve";
    public override string Description => "Adds a vertical curve (parabolic or circular) at a PVI point on a layout profile.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var profileHandle = GetParam<string>(parameters, "profile_handle");
        var pviIndex = GetParam(parameters, "pvi_index", 0);
        var curveLength = GetParam<double>(parameters, "curve_length");
        var curveType = GetParam(parameters, "curve_type", "Parabolic");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var profId = GetObjectIdFromHandle(doc.Database, profileHandle);
            if (profId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Profile not found."));

            var profile = tr.GetObject(profId, OpenMode.ForWrite) as Profile;
            if (profile == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Profile."));

            // Get the entity at the PVI
            int currentIdx = 0;
            foreach (ProfileEntity entity in profile.Entities)
            {
                if (entity.EntityType == ProfileEntityType.Tangent)
                {
                    if (currentIdx == pviIndex)
                    {
                        // Apply vertical curve
                        var vcType = curveType.ToLower() == "circular"
                            ? VerticalCurveType.Circular
                            : VerticalCurveType.Parabolic;

                        profile.Entities.AddFixedSymmetricParabola(
                            entity.EntityId,
                            curveLength);
                        break;
                    }
                    currentIdx++;
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Vertical curve (L={curveLength}) added at PVI index {pviIndex}."));
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
        if (GetParam<double>(parameters, "curve_length") <= 0)
            return ValidationResult.Invalid("curve_length must be positive");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["profile_handle", "curve_length"],
        "properties": {
            "profile_handle": { "type": "string" },
            "pvi_index": { "type": "integer", "default": 0, "description": "Index of the PVI to add curve at" },
            "curve_length": { "type": "number", "description": "Vertical curve length" },
            "curve_type": { "type": "string", "enum": ["Parabolic", "Circular"], "default": "Parabolic" }
        }
    }
    """);

}

public sealed class CreateProfileViewTool : CadToolBase
{
    public override string Name => "CreateProfileView";
    public override string Description => "Creates a profile view (the graphical display of profiles) at a specified drawing location.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var insertionPoint = GetPointParam(parameters, "insertion_point");
        var viewName = GetParam(parameters, "name", "Profile View");
        var layer = GetParam(parameters, "layer", "C-ROAD-PROF");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var pt = insertionPoint.Length >= 2
                ? new Point3d(insertionPoint[0], insertionPoint[1], 0)
                : Point3d.Origin;

            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);
            var styleId = civilDoc.Styles.ProfileViewStyles[0];
            var bandSetId = civilDoc.Styles.ProfileViewBandSetStyles[0];

            var pvId = ProfileView.Create(
                civilDoc,
                alignId,
                pt,
                viewName,
                styleId,
                bandSetId);

            var pv = tr.GetObject(pvId, OpenMode.ForRead) as ProfileView;
            var handle = pv?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Profile view '{viewName}' created at ({pt.X:F2}, {pt.Y:F2}). Handle: {handle}");
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
            "insertion_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" },
            "name": { "type": "string", "default": "Profile View" },
            "layer": { "type": "string", "default": "C-ROAD-PROF" }
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

public sealed class QueryProfilePVIsTool : CadToolBase
{
    public override string Name => "QueryProfilePVIs";
    public override string Description => "Returns all PVI (Point of Vertical Intersection) data for a layout profile.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var profileHandle = GetParam<string>(parameters, "profile_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var profId = GetObjectIdFromHandle(doc.Database, profileHandle);
            if (profId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Profile not found."));

            var profile = tr.GetObject(profId, OpenMode.ForRead) as Profile;
            if (profile == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Profile."));

            var pvis = new List<Dictionary<string, object>>();
            foreach (ProfilePVI pvi in profile.PVIs)
            {
                pvis.Add(new Dictionary<string, object>
                {
                    ["station"] = pvi.Station,
                    ["elevation"] = pvi.Elevation,
                    ["grade_in"] = pvi.GradeIn,
                    ["grade_out"] = pvi.GradeOut
                });
            }

            var entities = new List<Dictionary<string, object>>();
            foreach (ProfileEntity entity in profile.Entities)
            {
                entities.Add(new Dictionary<string, object>
                {
                    ["type"] = entity.EntityType.ToString(),
                    ["length"] = entity.Length
                });
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Profile '{profile.Name}': {pvis.Count} PVIs, {entities.Count} entities.",
                new Dictionary<string, object>
                {
                    ["name"] = profile.Name,
                    ["profile_type"] = profile.ProfileType.ToString(),
                    ["pvis"] = pvis,
                    ["entities"] = entities
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
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
        "properties": { "profile_handle": { "type": "string" } }
    }
    """);

}
