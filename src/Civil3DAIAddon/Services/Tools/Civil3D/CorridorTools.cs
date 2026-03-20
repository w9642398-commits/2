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

// ─── Corridor Tools ───────────────────────────────────────────────────────────

public sealed class CreateCorridorTool : CadToolBase
{
    public override string Name => "CreateCorridor";
    public override string Description => "Creates a Civil 3D corridor from an alignment, profile, and assembly.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var corridorName = GetParam<string>(parameters, "name");
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var profileHandle = GetParam<string>(parameters, "profile_handle");
        var assemblyHandle = GetParam<string>(parameters, "assembly_handle");
        var layer = GetParam(parameters, "layer", "C-ROAD-CORR");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            var profileId = GetObjectIdFromHandle(doc.Database, profileHandle);
            var assemblyId = GetObjectIdFromHandle(doc.Database, assemblyHandle);

            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));
            if (profileId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Profile not found."));
            if (assemblyId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Assembly not found."));

            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);

            var corridorId = civilDoc.CorridorCollection.Add(corridorName, "corridor-baseline", alignId, profileId, assemblyId, layerId);

            var corridor = tr.GetObject(corridorId, OpenMode.ForRead) as Corridor;
            var handle = corridor?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Corridor '{corridorName}' created. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to create corridor: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "name")))
            return ValidationResult.Invalid("name is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "profile_handle")))
            return ValidationResult.Invalid("profile_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "assembly_handle")))
            return ValidationResult.Invalid("assembly_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["name", "alignment_handle", "profile_handle", "assembly_handle"],
        "properties": {
            "name": { "type": "string", "description": "Corridor name" },
            "alignment_handle": { "type": "string", "description": "Handle of baseline alignment" },
            "profile_handle": { "type": "string", "description": "Handle of baseline profile" },
            "assembly_handle": { "type": "string", "description": "Handle of assembly" },
            "layer": { "type": "string", "default": "C-ROAD-CORR" }
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

public sealed class QueryCorridorInfoTool : CadToolBase
{
    public override string Name => "QueryCorridorInfo";
    public override string Description => "Returns detailed information about a Civil 3D corridor including baselines, regions, and surfaces.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var corridorHandle = GetParam<string>(parameters, "corridor_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, corridorHandle);
            if (objId.IsNull)
                return Task.FromResult(ToolResult.Fail(Name, "Corridor not found."));

            var corridor = tr.GetObject(objId, OpenMode.ForRead) as Corridor;
            if (corridor == null)
                return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Corridor."));

            var baselines = new List<Dictionary<string, object>>();
            foreach (Baseline bl in corridor.Baselines)
            {
                var regions = new List<Dictionary<string, object>>();
                foreach (BaselineRegion region in bl.BaselineRegions)
                {
                    regions.Add(new Dictionary<string, object>
                    {
                        ["name"] = region.Name,
                        ["start_station"] = region.StartStation,
                        ["end_station"] = region.EndStation,
                        ["assembly"] = region.AssemblyId.IsNull ? "none" : region.AssemblyId.Handle.ToString()
                    });
                }

                baselines.Add(new Dictionary<string, object>
                {
                    ["name"] = bl.Name ?? "Baseline",
                    ["alignment_handle"] = bl.AlignmentId.Handle.ToString(),
                    ["profile_handle"] = bl.ProfileId.Handle.ToString(),
                    ["regions"] = regions
                });
            }

            var surfaces = new List<string>();
            foreach (CorridorSurface cs in corridor.CorridorSurfaces)
            {
                surfaces.Add(cs.Name);
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Corridor '{corridor.Name}': {baselines.Count} baselines, {surfaces.Count} surfaces.",
                new Dictionary<string, object>
                {
                    ["name"] = corridor.Name,
                    ["handle"] = corridor.Handle.ToString(),
                    ["baselines"] = baselines,
                    ["surfaces"] = surfaces
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "corridor_handle")))
            return ValidationResult.Invalid("corridor_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["corridor_handle"],
        "properties": {
            "corridor_handle": { "type": "string", "description": "Handle of the corridor" }
        }
    }
    """);

}

public sealed class RebuildCorridorTool : CadToolBase
{
    public override string Name => "RebuildCorridor";
    public override string Description => "Rebuilds a corridor to update it after changes to alignment, profile, or assembly.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var corridorHandle = GetParam<string>(parameters, "corridor_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, corridorHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Corridor not found."));

            var corridor = tr.GetObject(objId, OpenMode.ForWrite) as Corridor;
            if (corridor == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Corridor."));

            corridor.Rebuild();
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name, $"Corridor '{corridor.Name}' rebuilt successfully."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "corridor_handle")))
            return ValidationResult.Invalid("corridor_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["corridor_handle"],
        "properties": {
            "corridor_handle": { "type": "string", "description": "Handle of the corridor to rebuild" }
        }
    }
    """);

}

public sealed class AddCorridorBaselineTool : CadToolBase
{
    public override string Name => "AddCorridorBaseline";
    public override string Description => "Adds a new baseline to an existing corridor with alignment, profile, and assembly.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var corridorHandle = GetParam<string>(parameters, "corridor_handle");
        var alignmentHandle = GetParam<string>(parameters, "alignment_handle");
        var profileHandle = GetParam<string>(parameters, "profile_handle");
        var assemblyHandle = GetParam<string>(parameters, "assembly_handle");
        var baselineName = GetParam(parameters, "baseline_name", "Baseline");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var corrId = GetObjectIdFromHandle(doc.Database, corridorHandle);
            var alignId = GetObjectIdFromHandle(doc.Database, alignmentHandle);
            var profileId = GetObjectIdFromHandle(doc.Database, profileHandle);
            var assemblyId = GetObjectIdFromHandle(doc.Database, assemblyHandle);

            if (corrId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Corridor not found."));
            if (alignId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Alignment not found."));

            var corridor = tr.GetObject(corrId, OpenMode.ForWrite) as Corridor;
            if (corridor == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Corridor."));

            corridor.Baselines.Add(baselineName, alignId, profileId, assemblyId);
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Baseline '{baselineName}' added to corridor '{corridor.Name}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "corridor_handle")))
            return ValidationResult.Invalid("corridor_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "alignment_handle")))
            return ValidationResult.Invalid("alignment_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["corridor_handle", "alignment_handle"],
        "properties": {
            "corridor_handle": { "type": "string" },
            "alignment_handle": { "type": "string" },
            "profile_handle": { "type": "string" },
            "assembly_handle": { "type": "string" },
            "baseline_name": { "type": "string", "default": "Baseline" }
        }
    }
    """);

}

public sealed class SetCorridorFrequencyTool : CadToolBase
{
    public override string Name => "SetCorridorFrequency";
    public override string Description => "Sets the frequency (sampling interval) for a corridor region.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var corridorHandle = GetParam<string>(parameters, "corridor_handle");
        var tangentFreq = GetParam(parameters, "tangent_frequency", 25.0);
        var curveFreq = GetParam(parameters, "curve_frequency", 10.0);
        var spiralFreq = GetParam(parameters, "spiral_frequency", 10.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, corridorHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Corridor not found."));

            var corridor = tr.GetObject(objId, OpenMode.ForWrite) as Corridor;
            if (corridor == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Corridor."));

            foreach (Baseline bl in corridor.Baselines)
            {
                foreach (BaselineRegion region in bl.BaselineRegions)
                {
                    var freq = region.GetCorridorFrequency();
                    freq.TangentFrequency = tangentFreq;
                    freq.CurveFrequency = curveFreq;
                    freq.SpiralFrequency = spiralFreq;
                    region.SetCorridorFrequency(freq);
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Corridor '{corridor.Name}' frequency set: tangent={tangentFreq}, curve={curveFreq}, spiral={spiralFreq}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "corridor_handle")))
            return ValidationResult.Invalid("corridor_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["corridor_handle"],
        "properties": {
            "corridor_handle": { "type": "string" },
            "tangent_frequency": { "type": "number", "default": 25.0, "description": "Sampling interval on tangent segments" },
            "curve_frequency": { "type": "number", "default": 10.0, "description": "Sampling interval on curve segments" },
            "spiral_frequency": { "type": "number", "default": 10.0, "description": "Sampling interval on spiral segments" }
        }
    }
    """);

}

public sealed class CreateAssemblyTool : CadToolBase
{
    public override string Name => "CreateAssembly";
    public override string Description => "Creates a Civil 3D assembly at a specified location for use in corridors.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var assemblyName = GetParam<string>(parameters, "name");
        var insertionPoint = GetPointParam(parameters, "insertion_point");
        var layer = GetParam(parameters, "layer", "C-ROAD-ASSM");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);
            var styleId = civilDoc.Styles.AssemblyStyles[0];

            var pt = insertionPoint.Length >= 2
                ? new Point3d(insertionPoint[0], insertionPoint[1], insertionPoint.Length > 2 ? insertionPoint[2] : 0)
                : Point3d.Origin;

            var assemblyId = Assembly.Create(civilDoc, pt, assemblyName, styleId, layerId);

            var assembly = tr.GetObject(assemblyId, OpenMode.ForRead) as Assembly;
            var handle = assembly?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Assembly '{assemblyName}' created. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to create assembly: {ex.Message}", ex.StackTrace));
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
            "name": { "type": "string", "description": "Assembly name" },
            "insertion_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y] or [x, y, z]" },
            "layer": { "type": "string", "default": "C-ROAD-ASSM" }
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

public sealed class AddSubassemblyTool : CadToolBase
{
    public override string Name => "AddSubassembly";
    public override string Description => "Adds a subassembly (e.g., lane, shoulder, ditch) to an assembly by name from the subassembly catalog.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var assemblyHandle = GetParam<string>(parameters, "assembly_handle");
        var subassemblyName = GetParam<string>(parameters, "subassembly_name");
        var side = GetParam(parameters, "side", "Right");
        var offset = GetParam(parameters, "offset", 0.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, assemblyHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Assembly not found."));

            var assembly = tr.GetObject(objId, OpenMode.ForWrite) as Assembly;
            if (assembly == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Assembly."));

            // Subassemblies are added using the Civil 3D Tool Palette or programmatically
            // via the Subassembly.CreateFromMacro method with catalog paths
            var civilDoc = CivilApplication.ActiveDocument;

            // Create insertion point on the appropriate side
            var markerPt = new Point3d(
                side.Equals("Left", StringComparison.OrdinalIgnoreCase) ? -offset : offset,
                0, 0);

            var subId = Subassembly.CreateFromMacro(
                civilDoc,
                subassemblyName,
                objId,
                markerPt);

            var sub = tr.GetObject(subId, OpenMode.ForRead) as Subassembly;
            var handle = sub?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Subassembly '{subassemblyName}' added to assembly on {side} side. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to add subassembly: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "assembly_handle")))
            return ValidationResult.Invalid("assembly_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "subassembly_name")))
            return ValidationResult.Invalid("subassembly_name is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["assembly_handle", "subassembly_name"],
        "properties": {
            "assembly_handle": { "type": "string", "description": "Handle of target assembly" },
            "subassembly_name": { "type": "string", "description": "Subassembly macro name (e.g., BasicLane, BasicShoulder, BasicSideSlopeCutDitch)" },
            "side": { "type": "string", "enum": ["Left", "Right"], "default": "Right" },
            "offset": { "type": "number", "default": 0.0, "description": "Offset from assembly baseline" }
        }
    }
    """);

}

public sealed class ExtractCorridorSurfaceTool : CadToolBase
{
    public override string Name => "ExtractCorridorSurface";
    public override string Description => "Extracts a surface from a corridor (top, datum, or feature line-based).";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var corridorHandle = GetParam<string>(parameters, "corridor_handle");
        var surfaceName = GetParam(parameters, "surface_name", "Corridor Surface");
        var addAsBoundary = GetParam(parameters, "add_boundary", true);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, corridorHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Corridor not found."));

            var corridor = tr.GetObject(objId, OpenMode.ForWrite) as Corridor;
            if (corridor == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Corridor."));

            // Check if surface with this name already exists
            CorridorSurface? corrSurface = null;
            foreach (CorridorSurface cs in corridor.CorridorSurfaces)
            {
                if (cs.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase))
                {
                    corrSurface = cs;
                    break;
                }
            }

            if (corrSurface == null)
            {
                corridor.CorridorSurfaces.Add(surfaceName);
                corrSurface = corridor.CorridorSurfaces[corridor.CorridorSurfaces.Count - 1];
            }

            if (addAsBoundary)
            {
                corrSurface.AddCorridorExtentsAsBoundary("Corridor Extents");
            }

            corridor.Rebuild();
            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Corridor surface '{surfaceName}' extracted from corridor '{corridor.Name}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "corridor_handle")))
            return ValidationResult.Invalid("corridor_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["corridor_handle"],
        "properties": {
            "corridor_handle": { "type": "string" },
            "surface_name": { "type": "string", "default": "Corridor Surface" },
            "add_boundary": { "type": "boolean", "default": true, "description": "Add corridor extents as surface boundary" }
        }
    }
    """);

}
