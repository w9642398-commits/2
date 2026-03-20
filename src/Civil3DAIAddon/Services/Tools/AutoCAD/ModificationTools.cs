using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.AutoCAD;

public sealed class MoveEntityTool : CadToolBase
{
    public override string Name => "MoveEntity";
    public override string Description => "Moves an entity by a displacement vector or to a target point.";
    public override string Category => "Modify";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var handle = GetParam<string>(parameters, "handle");
        var displacement = GetPointParam(parameters, "displacement");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, handle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, $"Entity with handle '{handle}' not found."));

        var ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
        if (ent == null)
            return Task.FromResult(ToolResult.Fail(Name, $"Handle '{handle}' is not an entity."));

        var vec = new Vector3d(displacement[0], displacement[1], displacement.Length > 2 ? displacement[2] : 0);
        ent.TransformBy(Matrix3d.Displacement(vec));
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Entity {handle} moved by ({displacement[0]}, {displacement[1]}).");
        result.ModifiedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "handle")))
            return ValidationResult.Invalid("handle is required");
        var disp = GetPointParam(parameters, "displacement");
        if (disp.Length < 2) return ValidationResult.Invalid("displacement requires [dx, dy]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["handle", "displacement"],
        "properties": {
            "handle": { "type": "string", "description": "Entity handle" },
            "displacement": { "type": "array", "items": { "type": "number" }, "description": "[dx, dy] or [dx, dy, dz]" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
        catch { return ObjectId.Null; }
    }
}

public sealed class CopyEntityTool : CadToolBase
{
    public override string Name => "CopyEntity";
    public override string Description => "Creates a copy of an entity, optionally displaced.";
    public override string Category => "Modify";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var handle = GetParam<string>(parameters, "handle");
        var displacement = GetPointParam(parameters, "displacement");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, handle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, $"Entity '{handle}' not found."));

        var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
        if (ent == null)
            return Task.FromResult(ToolResult.Fail(Name, $"Handle '{handle}' is not an entity."));

        var clone = (Entity)ent.Clone();

        if (displacement.Length >= 2)
        {
            var vec = new Vector3d(displacement[0], displacement[1], displacement.Length > 2 ? displacement[2] : 0);
            clone.TransformBy(Matrix3d.Displacement(vec));
        }

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        btr.AppendEntity(clone);
        tr.AddNewlyCreatedDBObject(clone, true);
        var newHandle = clone.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Entity copied. New handle: {newHandle}");
        result.CreatedHandles.Add(newHandle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "handle")))
            return ValidationResult.Invalid("handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["handle"],
        "properties": {
            "handle": { "type": "string" },
            "displacement": { "type": "array", "items": { "type": "number" }, "description": "Optional [dx, dy] or [dx, dy, dz]" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
        catch { return ObjectId.Null; }
    }
}

public sealed class RotateEntityTool : CadToolBase
{
    public override string Name => "RotateEntity";
    public override string Description => "Rotates an entity around a base point by a given angle in degrees.";
    public override string Category => "Modify";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var handle = GetParam<string>(parameters, "handle");
        var basePoint = GetPointParam(parameters, "base_point");
        var angle = GetParam<double>(parameters, "angle") * Math.PI / 180.0;

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, handle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, $"Entity '{handle}' not found."));

        var ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
        if (ent == null)
            return Task.FromResult(ToolResult.Fail(Name, $"Handle '{handle}' is not an entity."));

        var basePt = new Point3d(basePoint[0], basePoint[1], basePoint.Length > 2 ? basePoint[2] : 0);
        ent.TransformBy(Matrix3d.Rotation(angle, Vector3d.ZAxis, basePt));
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Entity {handle} rotated.");
        result.ModifiedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "handle")))
            return ValidationResult.Invalid("handle is required");
        var bp = GetPointParam(parameters, "base_point");
        if (bp.Length < 2) return ValidationResult.Invalid("base_point requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["handle", "base_point", "angle"],
        "properties": {
            "handle": { "type": "string" },
            "base_point": { "type": "array", "items": { "type": "number" } },
            "angle": { "type": "number", "description": "Rotation angle in degrees" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
        catch { return ObjectId.Null; }
    }
}

public sealed class EraseEntityTool : CadToolBase
{
    public override string Name => "EraseEntity";
    public override string Description => "Erases (deletes) an entity by handle. This is a destructive operation.";
    public override string Category => "Modify";
    public override SafetyLevel SafetyLevel => SafetyLevel.Destructive;
    public override bool RequiresConfirmation => true;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var handle = GetParam<string>(parameters, "handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, handle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, $"Entity '{handle}' not found."));

        var ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
        if (ent == null)
            return Task.FromResult(ToolResult.Fail(Name, $"Handle '{handle}' is not an entity."));

        ent.Erase();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Entity {handle} erased.");
        result.DeletedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "handle")))
            return ValidationResult.Invalid("handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["handle"],
        "properties": {
            "handle": { "type": "string", "description": "Handle of the entity to erase" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
        catch { return ObjectId.Null; }
    }
}

public sealed class ChangeLayerTool : CadToolBase
{
    public override string Name => "ChangeLayer";
    public override string Description => "Changes the layer of one or more entities.";
    public override string Category => "Modify";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var handles = GetParam<string>(parameters, "handles")?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      ?? Array.Empty<string>();
        var targetLayer = GetParam<string>(parameters, "target_layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        // Ensure layer exists
        var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
        if (!lt.Has(targetLayer))
        {
            lt.UpgradeOpen();
            var newLayer = new LayerTableRecord { Name = targetLayer };
            lt.Add(newLayer);
            tr.AddNewlyCreatedDBObject(newLayer, true);
        }

        var modified = new List<string>();
        foreach (var handleStr in handles)
        {
            var h = handleStr.Trim();
            var objId = GetObjectIdFromHandle(doc.Database, h);
            if (objId.IsNull) continue;

            var ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
            if (ent != null)
            {
                ent.Layer = targetLayer;
                modified.Add(h);
            }
        }

        tr.Commit();

        var result = ToolResult.Ok(Name, $"Changed layer to '{targetLayer}' for {modified.Count} entities.");
        result.ModifiedHandles.AddRange(modified);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "handles")))
            return ValidationResult.Invalid("handles is required (comma-separated)");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "target_layer")))
            return ValidationResult.Invalid("target_layer is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["handles", "target_layer"],
        "properties": {
            "handles": { "type": "string", "description": "Comma-separated entity handles" },
            "target_layer": { "type": "string", "description": "Target layer name" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
        catch { return ObjectId.Null; }
    }
}

public sealed class SetPropertiesTool : CadToolBase
{
    public override string Name => "SetProperties";
    public override string Description => "Sets properties (color, linetype, lineweight, etc.) on an entity.";
    public override string Category => "Modify";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var handle = GetParam<string>(parameters, "handle");
        var color = GetParam<int>(parameters, "color", -1);
        var linetype = GetParam<string>(parameters, "linetype");
        var lineweight = GetParam<string>(parameters, "lineweight");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var objId = GetObjectIdFromHandle(doc.Database, handle);
        if (objId.IsNull)
            return Task.FromResult(ToolResult.Fail(Name, $"Entity '{handle}' not found."));

        var ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
        if (ent == null)
            return Task.FromResult(ToolResult.Fail(Name, $"Handle '{handle}' is not an entity."));

        if (color >= 0)
            ent.ColorIndex = color;

        if (!string.IsNullOrEmpty(linetype))
        {
            var ltTable = (LinetypeTable)tr.GetObject(doc.Database.LinetypeTableId, OpenMode.ForRead);
            if (ltTable.Has(linetype))
                ent.LinetypeId = ltTable[linetype];
        }

        if (!string.IsNullOrEmpty(lineweight) && Enum.TryParse<LineWeight>(lineweight, true, out var lw))
            ent.LineWeight = lw;

        tr.Commit();

        var result = ToolResult.Ok(Name, $"Properties set on entity {handle}.");
        result.ModifiedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "handle")))
            return ValidationResult.Invalid("handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["handle"],
        "properties": {
            "handle": { "type": "string" },
            "color": { "type": "integer", "description": "ACI color index (0-256)" },
            "linetype": { "type": "string" },
            "lineweight": { "type": "string" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
        catch { return ObjectId.Null; }
    }
}

public sealed class ZoomToObjectsTool : CadToolBase
{
    public override string Name => "ZoomToObjects";
    public override string Description => "Zooms the current viewport to fit the specified entities.";
    public override string Category => "View";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var handles = GetParam<string>(parameters, "handles")?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      ?? Array.Empty<string>();

        var doc = Application.DocumentManager.MdiActiveDocument;
        if (handles.Length == 0)
        {
            doc.SendStringToExecute("ZOOM E\n", true, false, true);
            return Task.FromResult(ToolResult.Ok(Name, "Zoomed to extents."));
        }

        using var tr = doc.TransactionManager.StartTransaction();
        var minPt = new Point3d(double.MaxValue, double.MaxValue, 0);
        var maxPt = new Point3d(double.MinValue, double.MinValue, 0);

        foreach (var h in handles)
        {
            var objId = GetObjectIdFromHandle(doc.Database, h.Trim());
            if (objId.IsNull) continue;
            var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
            if (ent == null) continue;

            var ext = ent.GeometricExtents;
            minPt = new Point3d(
                Math.Min(minPt.X, ext.MinPoint.X),
                Math.Min(minPt.Y, ext.MinPoint.Y), 0);
            maxPt = new Point3d(
                Math.Max(maxPt.X, ext.MaxPoint.X),
                Math.Max(maxPt.Y, ext.MaxPoint.Y), 0);
        }
        tr.Commit();

        var view = doc.Editor.GetCurrentView();
        view.CenterPoint = new Point2d((minPt.X + maxPt.X) / 2, (minPt.Y + maxPt.Y) / 2);
        view.Width = (maxPt.X - minPt.X) * 1.1;
        view.Height = (maxPt.Y - minPt.Y) * 1.1;
        doc.Editor.SetCurrentView(view);

        return Task.FromResult(ToolResult.Ok(Name, $"Zoomed to {handles.Length} objects."));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "properties": {
            "handles": { "type": "string", "description": "Comma-separated handles. Empty = zoom extents." }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try
        {
            var handle = new Handle(Convert.ToInt64(handleStr, 16));
            return db.GetObjectId(false, handle, 0);
        }
        catch { return ObjectId.Null; }
    }
}

public sealed class StartUndoScopeTool : CadToolBase
{
    public override string Name => "StartUndoScope";
    public override string Description => "Starts an undo scope (group) so multiple operations can be undone as one.";
    public override string Category => "Transaction";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var scopeName = GetParam(parameters, "scope_name", "AI Operation");
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Database.UndoController.StartUndoMark();

        return Task.FromResult(ToolResult.Ok(Name, $"Undo scope '{scopeName}' started."));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "properties": {
            "scope_name": { "type": "string", "default": "AI Operation" }
        }
    }
    """);
}

public sealed class CommitTransactionTool : CadToolBase
{
    public override string Name => "CommitTransaction";
    public override string Description => "Ends the current undo scope, finalizing all grouped operations.";
    public override string Category => "Transaction";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.Database.UndoController.EndUndoMark();

        return Task.FromResult(ToolResult.Ok(Name, "Undo scope committed."));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}

public sealed class RollbackTransactionTool : CadToolBase
{
    public override string Name => "RollbackTransaction";
    public override string Description => "Undoes all operations within the current undo scope.";
    public override string Category => "Transaction";
    public override SafetyLevel SafetyLevel => SafetyLevel.Destructive;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        doc?.SendStringToExecute("UNDO\n", true, false, true);

        return Task.FromResult(ToolResult.Ok(Name, "Undo invoked."));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}
