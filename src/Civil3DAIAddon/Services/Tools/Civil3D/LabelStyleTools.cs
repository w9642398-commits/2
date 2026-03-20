using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.Civil3D;

// ─── Label Style and Data Shortcut Tools ─────────────────────────────────────

public sealed class QueryLabelStylesTool : CadToolBase
{
    public override string Name => "QueryLabelStyles";
    public override string Description => "Lists all available label styles for a specified Civil 3D object type (alignment, surface, profile, parcel, pipe).";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var objectType = GetParam(parameters, "object_type", "alignment");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var styles = new List<Dictionary<string, object>>();

            switch (objectType.ToLower())
            {
                case "alignment":
                    foreach (ObjectId id in civilDoc.Styles.LabelStyles.AlignmentLabelStyles.GetStationLabelStyleIds())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as LabelStyle;
                        if (style != null)
                            styles.Add(new Dictionary<string, object> { ["name"] = style.Name, ["type"] = "Station" });
                    }
                    break;

                case "surface":
                    foreach (ObjectId id in civilDoc.Styles.LabelStyles.SurfaceLabelStyles.GetContourLabelStyleIds())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as LabelStyle;
                        if (style != null)
                            styles.Add(new Dictionary<string, object> { ["name"] = style.Name, ["type"] = "Contour" });
                    }
                    break;

                case "profile":
                    foreach (ObjectId id in civilDoc.Styles.LabelStyles.ProfileLabelStyles.GetCrestCurveLabelStyleIds())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as LabelStyle;
                        if (style != null)
                            styles.Add(new Dictionary<string, object> { ["name"] = style.Name, ["type"] = "CrestCurve" });
                    }
                    break;

                case "parcel":
                    foreach (ObjectId id in civilDoc.Styles.LabelStyles.ParcelLabelStyles.AreaLabelStyles)
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as LabelStyle;
                        if (style != null)
                            styles.Add(new Dictionary<string, object> { ["name"] = style.Name, ["type"] = "Area" });
                    }
                    break;

                case "pipe":
                    foreach (ObjectId id in civilDoc.Styles.LabelStyles.PipeLabelStyles.GetPipePlanLabelStyleIds())
                    {
                        var style = tr.GetObject(id, OpenMode.ForRead) as LabelStyle;
                        if (style != null)
                            styles.Add(new Dictionary<string, object> { ["name"] = style.Name, ["type"] = "PipePlan" });
                    }
                    break;
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Found {styles.Count} label styles for '{objectType}'.",
                new Dictionary<string, object> { ["styles"] = styles }));
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
            "object_type": { "type": "string", "enum": ["alignment", "surface", "profile", "parcel", "pipe"], "default": "alignment" }
        }
    }
    """);
}

public sealed class QueryObjectStylesTool : CadToolBase
{
    public override string Name => "QueryObjectStyles";
    public override string Description => "Lists all available object styles for a Civil 3D type (alignment, surface, profile, pipe, structure).";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var objectType = GetParam(parameters, "object_type", "alignment");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var styles = new List<Dictionary<string, object>>();

            ObjectIdCollection styleIds = objectType.ToLower() switch
            {
                "alignment" => civilDoc.Styles.AlignmentStyles,
                "surface" => civilDoc.Styles.SurfaceStyles,
                "profile" => civilDoc.Styles.ProfileStyles,
                "profile_view" => civilDoc.Styles.ProfileViewStyles,
                "section_view" => civilDoc.Styles.SectionViewStyles,
                "pipe" => civilDoc.Styles.PipeStyles,
                "structure" => civilDoc.Styles.StructureStyles,
                "assembly" => civilDoc.Styles.AssemblyStyles,
                "parcel" => civilDoc.Styles.ParcelStyles,
                _ => new ObjectIdCollection()
            };

            foreach (ObjectId id in styleIds)
            {
                var style = tr.GetObject(id, OpenMode.ForRead) as StyleBase;
                if (style != null)
                    styles.Add(new Dictionary<string, object> { ["name"] = style.Name, ["handle"] = style.Handle.ToString() });
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Found {styles.Count} {objectType} styles.",
                new Dictionary<string, object> { ["styles"] = styles }));
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
            "object_type": { "type": "string", "enum": ["alignment", "surface", "profile", "profile_view", "section_view", "pipe", "structure", "assembly", "parcel"], "default": "alignment" }
        }
    }
    """);
}

public sealed class SetObjectStyleTool : CadToolBase
{
    public override string Name => "SetObjectStyle";
    public override string Description => "Changes the display style of a Civil 3D object (alignment, surface, profile, etc.).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var objectHandle = GetParam<string>(parameters, "object_handle");
        var styleName = GetParam<string>(parameters, "style_name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, objectHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Object not found."));

            var entity = tr.GetObject(objId, OpenMode.ForWrite);
            var civilDoc = CivilApplication.ActiveDocument;

            if (entity is Alignment alignment)
            {
                foreach (ObjectId sid in civilDoc.Styles.AlignmentStyles)
                {
                    var style = tr.GetObject(sid, OpenMode.ForRead) as AlignmentStyle;
                    if (style != null && style.Name.Equals(styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        alignment.StyleId = sid;
                        break;
                    }
                }
            }
            else if (entity is Autodesk.Civil.DatabaseServices.Surface surface)
            {
                foreach (ObjectId sid in civilDoc.Styles.SurfaceStyles)
                {
                    var style = tr.GetObject(sid, OpenMode.ForRead) as SurfaceStyle;
                    if (style != null && style.Name.Equals(styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        surface.StyleId = sid;
                        break;
                    }
                }
            }
            else if (entity is Profile profile)
            {
                foreach (ObjectId sid in civilDoc.Styles.ProfileStyles)
                {
                    var style = tr.GetObject(sid, OpenMode.ForRead) as ProfileStyle;
                    if (style != null && style.Name.Equals(styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        profile.StyleId = sid;
                        break;
                    }
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Object style changed to '{styleName}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "object_handle")))
            return ValidationResult.Invalid("object_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "style_name")))
            return ValidationResult.Invalid("style_name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["object_handle", "style_name"],
        "properties": {
            "object_handle": { "type": "string" },
            "style_name": { "type": "string", "description": "Name of the target style" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class CreateDataShortcutTool : CadToolBase
{
    public override string Name => "CreateDataShortcut";
    public override string Description => "Creates a data shortcut for a Civil 3D object to share it across drawings in a project.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var objectHandle = GetParam<string>(parameters, "object_handle");
        var shortcutName = GetParam<string>(parameters, "shortcut_name");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, objectHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Object not found."));

            var civilDoc = CivilApplication.ActiveDocument;

            // Data shortcuts are managed through the project system
            // Use ExportToDataShortcuts for supported object types
            var entity = tr.GetObject(objId, OpenMode.ForRead);

            if (entity is Alignment || entity is Autodesk.Civil.DatabaseServices.Surface || entity is Profile || entity is Network)
            {
                civilDoc.ExportToDataShortcuts(new ObjectIdCollection { objId });
                tr.Commit();
                return Task.FromResult(ToolResult.Ok(Name,
                    $"Data shortcut created for object. Name: '{shortcutName ?? "auto"}'."));
            }

            return Task.FromResult(ToolResult.Fail(Name,
                "Only Alignments, Surfaces, Profiles, and Pipe Networks support data shortcuts."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "object_handle")))
            return ValidationResult.Invalid("object_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["object_handle"],
        "properties": {
            "object_handle": { "type": "string" },
            "shortcut_name": { "type": "string", "description": "Name for the data shortcut" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ImportDataReferenceTool : CadToolBase
{
    public override string Name => "ImportDataReference";
    public override string Description => "Imports a data reference (shortcut) from a project into the current drawing.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var objectName = GetParam<string>(parameters, "object_name");
        var objectType = GetParam(parameters, "object_type", "alignment");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;

            // Data references are imported using the CivilDocument methods
            // The specific API depends on the object type
            doc.SendStringToExecute($"_AeccCreateSurfaceReference\n{objectName}\n", true, false, false);

            return Task.FromResult(ToolResult.Ok(Name,
                $"Data reference import initiated for '{objectName}' ({objectType})."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "object_name")))
            return ValidationResult.Invalid("object_name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["object_name"],
        "properties": {
            "object_name": { "type": "string", "description": "Name of the shared object to import" },
            "object_type": { "type": "string", "enum": ["alignment", "surface", "profile", "pipe_network"], "default": "alignment" }
        }
    }
    """);
}
