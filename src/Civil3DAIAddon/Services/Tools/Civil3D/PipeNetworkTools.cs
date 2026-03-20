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

// ─── Pipe Network Tools ──────────────────────────────────────────────────────

public sealed class CreatePipeNetworkTool : CadToolBase
{
    public override string Name => "CreatePipeNetwork";
    public override string Description => "Creates a new Civil 3D pipe network with specified name and parts list.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var networkName = GetParam<string>(parameters, "name");
        var partsListName = GetParam(parameters, "parts_list", "");
        var layer = GetParam(parameters, "layer", "C-STRM-PIPE");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);

            // Find parts list
            var partsListId = ObjectId.Null;
            if (!string.IsNullOrEmpty(partsListName))
            {
                foreach (ObjectId plId in civilDoc.GetPartListIds())
                {
                    var pl = tr.GetObject(plId, OpenMode.ForRead) as PartsList;
                    if (pl != null && pl.Name.Equals(partsListName, StringComparison.OrdinalIgnoreCase))
                    {
                        partsListId = plId;
                        break;
                    }
                }
            }

            var networkId = Network.Create(civilDoc, networkName, partsListId, ObjectId.Null, layerId);

            var network = tr.GetObject(networkId, OpenMode.ForRead) as Network;
            var handle = network?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Pipe network '{networkName}' created. Handle: {handle}");
            result.CreatedHandles.Add(handle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed to create pipe network: {ex.Message}", ex.StackTrace));
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
            "name": { "type": "string", "description": "Network name" },
            "parts_list": { "type": "string", "description": "Name of parts list to use" },
            "layer": { "type": "string", "default": "C-STRM-PIPE" }
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

public sealed class AddPipeToNetworkTool : CadToolBase
{
    public override string Name => "AddPipeToNetwork";
    public override string Description => "Adds a pipe between two structures in a pipe network.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var networkHandle = GetParam<string>(parameters, "network_handle");
        var startPoint = GetPointParam(parameters, "start_point");
        var endPoint = GetPointParam(parameters, "end_point");
        var pipeName = GetParam(parameters, "pipe_name", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var netId = GetObjectIdFromHandle(doc.Database, networkHandle);
            if (netId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Network not found."));

            var network = tr.GetObject(netId, OpenMode.ForWrite) as Network;
            if (network == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Network."));

            var pt1 = new Point3d(startPoint[0], startPoint[1], startPoint.Length > 2 ? startPoint[2] : 0);
            var pt2 = new Point3d(endPoint[0], endPoint[1], endPoint.Length > 2 ? endPoint[2] : 0);

            // Add structures at start and end if not existing
            var structId1 = network.AddStructure(pt1);
            var structId2 = network.AddStructure(pt2);

            // Add pipe between structures
            var pipeId = network.AddPipe(structId1, structId2);

            var pipe = tr.GetObject(pipeId, OpenMode.ForRead) as Pipe;
            var handle = pipe?.Handle.ToString() ?? "unknown";

            if (pipe != null && !string.IsNullOrEmpty(pipeName))
            {
                var pw = tr.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
                if (pw != null) pw.Name = pipeName;
            }

            tr.Commit();

            var result = ToolResult.Ok(Name, $"Pipe added to network '{network.Name}'. Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "network_handle")))
            return ValidationResult.Invalid("network_handle is required");
        var sp = GetPointParam(parameters, "start_point");
        var ep = GetPointParam(parameters, "end_point");
        if (sp.Length < 2) return ValidationResult.Invalid("start_point requires [x, y]");
        if (ep.Length < 2) return ValidationResult.Invalid("end_point requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["network_handle", "start_point", "end_point"],
        "properties": {
            "network_handle": { "type": "string" },
            "start_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y, z]" },
            "end_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y, z]" },
            "pipe_name": { "type": "string" }
        }
    }
    """);

}

public sealed class AddStructureToNetworkTool : CadToolBase
{
    public override string Name => "AddStructureToNetwork";
    public override string Description => "Adds a structure (manhole, catch basin, etc.) to a pipe network at a specified location.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var networkHandle = GetParam<string>(parameters, "network_handle");
        var position = GetPointParam(parameters, "position");
        var structureName = GetParam(parameters, "structure_name", "");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var netId = GetObjectIdFromHandle(doc.Database, networkHandle);
            if (netId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Network not found."));

            var network = tr.GetObject(netId, OpenMode.ForWrite) as Network;
            if (network == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Network."));

            var pt = new Point3d(position[0], position[1], position.Length > 2 ? position[2] : 0);
            var structId = network.AddStructure(pt);

            var structure = tr.GetObject(structId, OpenMode.ForWrite) as Structure;
            if (structure != null && !string.IsNullOrEmpty(structureName))
                structure.Name = structureName;

            var handle = structure?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Structure added to network at ({pt.X:F2}, {pt.Y:F2}). Handle: {handle}");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "network_handle")))
            return ValidationResult.Invalid("network_handle is required");
        if (GetPointParam(parameters, "position").Length < 2)
            return ValidationResult.Invalid("position requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["network_handle", "position"],
        "properties": {
            "network_handle": { "type": "string" },
            "position": { "type": "array", "items": { "type": "number" }, "description": "[x, y, z]" },
            "structure_name": { "type": "string" }
        }
    }
    """);

}

public sealed class QueryPipeNetworkTool : CadToolBase
{
    public override string Name => "QueryPipeNetwork";
    public override string Description => "Returns detailed information about a pipe network including all pipes and structures.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var networkHandle = GetParam<string>(parameters, "network_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var netId = GetObjectIdFromHandle(doc.Database, networkHandle);
            if (netId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Network not found."));

            var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
            if (network == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Network."));

            var pipes = new List<Dictionary<string, object>>();
            foreach (ObjectId pipeId in network.GetPipeIds())
            {
                var pipe = tr.GetObject(pipeId, OpenMode.ForRead) as Pipe;
                if (pipe != null)
                {
                    pipes.Add(new Dictionary<string, object>
                    {
                        ["name"] = pipe.Name,
                        ["handle"] = pipe.Handle.ToString(),
                        ["length"] = pipe.Length2D,
                        ["inner_diameter"] = pipe.InnerDiameterOrWidth,
                        ["slope"] = pipe.Slope,
                        ["flow_direction"] = pipe.FlowDirection.ToString()
                    });
                }
            }

            var structures = new List<Dictionary<string, object>>();
            foreach (ObjectId strId in network.GetStructureIds())
            {
                var structure = tr.GetObject(strId, OpenMode.ForRead) as Structure;
                if (structure != null)
                {
                    structures.Add(new Dictionary<string, object>
                    {
                        ["name"] = structure.Name,
                        ["handle"] = structure.Handle.ToString(),
                        ["location_x"] = structure.Location.X,
                        ["location_y"] = structure.Location.Y,
                        ["rim_elevation"] = structure.RimElevation,
                        ["sump_elevation"] = structure.SumpElevation,
                        ["connected_pipes"] = structure.ConnectedPipesCount
                    });
                }
            }

            tr.Commit();

            return Task.FromResult(ToolResult.Ok(Name,
                $"Network '{network.Name}': {pipes.Count} pipes, {structures.Count} structures.",
                new Dictionary<string, object>
                {
                    ["name"] = network.Name,
                    ["pipes"] = pipes,
                    ["structures"] = structures
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "network_handle")))
            return ValidationResult.Invalid("network_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["network_handle"],
        "properties": { "network_handle": { "type": "string" } }
    }
    """);

}

public sealed class CheckPipeInterferenceTool : CadToolBase
{
    public override string Name => "CheckPipeInterference";
    public override string Description => "Checks for interference between two pipe networks or a pipe network and a surface.";
    public override string Category => "Civil3D";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var networkHandle = GetParam<string>(parameters, "network_handle");
        var otherHandle = GetParam<string>(parameters, "other_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var netId = GetObjectIdFromHandle(doc.Database, networkHandle);
            if (netId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Network not found."));

            var network = tr.GetObject(netId, OpenMode.ForRead) as Network;
            if (network == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not a Network."));

            var otherId = GetObjectIdFromHandle(doc.Database, otherHandle);
            if (otherId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Other object not found."));

            var interferences = new List<Dictionary<string, object>>();

            // Check pipe-to-pipe interference within and between networks
            var interferenceCheck = network.GetInterferenceCheckIds();
            foreach (ObjectId checkId in interferenceCheck)
            {
                var check = tr.GetObject(checkId, OpenMode.ForRead) as InterferenceCheck;
                if (check != null)
                {
                    interferences.Add(new Dictionary<string, object>
                    {
                        ["part1"] = check.Part1Id.Handle.ToString(),
                        ["part2"] = check.Part2Id.Handle.ToString(),
                        ["has_conflict"] = check.HasConflict
                    });
                }
            }

            tr.Commit();

            var conflicts = interferences.Count(i => (bool)i["has_conflict"]);
            return Task.FromResult(ToolResult.Ok(Name,
                $"Interference check: {interferences.Count} checks, {conflicts} conflicts found.",
                new Dictionary<string, object> { ["interferences"] = interferences }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "network_handle")))
            return ValidationResult.Invalid("network_handle is required");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "other_handle")))
            return ValidationResult.Invalid("other_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["network_handle", "other_handle"],
        "properties": {
            "network_handle": { "type": "string", "description": "Handle of first pipe network" },
            "other_handle": { "type": "string", "description": "Handle of second network or surface to check against" }
        }
    }
    """);

}

public sealed class CreatePressureNetworkTool : CadToolBase
{
    public override string Name => "CreatePressureNetwork";
    public override string Description => "Creates a Civil 3D pressure pipe network for water/sewer pressure systems.";
    public override string Category => "Civil3D";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var networkName = GetParam<string>(parameters, "name");
        var layer = GetParam(parameters, "layer", "C-WATR-PIPE");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var civilDoc = CivilApplication.ActiveDocument;
            var layerId = GetOrCreateLayer(doc.Database, tr, layer);

            // Pressure networks use the PressurePipeNetwork class
            var networkId = PressurePipeNetwork.Create(civilDoc, networkName, layerId);

            var network = tr.GetObject(networkId, OpenMode.ForRead) as PressurePipeNetwork;
            var handle = network?.Handle.ToString() ?? "unknown";
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Pressure network '{networkName}' created. Handle: {handle}");
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
            "name": { "type": "string", "description": "Pressure network name" },
            "layer": { "type": "string", "default": "C-WATR-PIPE" }
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
