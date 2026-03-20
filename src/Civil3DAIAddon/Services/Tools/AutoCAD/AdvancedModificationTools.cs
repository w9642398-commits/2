using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.AutoCAD;

// ─── Advanced AutoCAD Modification Tools ─────────────────────────────────────

public sealed class MirrorEntityTool : CadToolBase
{
    public override string Name => "MirrorEntity";
    public override string Description => "Mirrors an entity about a specified axis (two points defining the mirror line).";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entityHandle = GetParam<string>(parameters, "entity_handle");
        var mirrorPt1 = GetPointParam(parameters, "mirror_point1");
        var mirrorPt2 = GetPointParam(parameters, "mirror_point2");
        var deleteSource = GetParam(parameters, "delete_source", false);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, entityHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Entity not found."));

            var entity = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
            if (entity == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Entity."));

            var pt1 = new Point3d(mirrorPt1[0], mirrorPt1[1], 0);
            var pt2 = new Point3d(mirrorPt2[0], mirrorPt2[1], 0);
            var mirrorLine = new Line3d(pt1, pt2);

            var mirrored = entity.GetTransformedCopy(Matrix3d.Mirroring(mirrorLine)) as Entity;

            if (mirrored != null)
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                btr.AppendEntity(mirrored);
                tr.AddNewlyCreatedDBObject(mirrored, true);
            }

            if (deleteSource)
                entity.Erase();

            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Entity mirrored{(deleteSource ? " (source deleted)" : "")}. Handle: {mirrored?.Handle.ToString() ?? "unknown"}");
            if (mirrored != null) result.CreatedHandles.Add(mirrored.Handle.ToString());
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "entity_handle")))
            return ValidationResult.Invalid("entity_handle is required");
        if (GetPointParam(parameters, "mirror_point1").Length < 2)
            return ValidationResult.Invalid("mirror_point1 requires [x, y]");
        if (GetPointParam(parameters, "mirror_point2").Length < 2)
            return ValidationResult.Invalid("mirror_point2 requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["entity_handle", "mirror_point1", "mirror_point2"],
        "properties": {
            "entity_handle": { "type": "string" },
            "mirror_point1": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" },
            "mirror_point2": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" },
            "delete_source": { "type": "boolean", "default": false }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ScaleEntityTool : CadToolBase
{
    public override string Name => "ScaleEntity";
    public override string Description => "Scales an entity uniformly around a base point by a specified factor.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entityHandle = GetParam<string>(parameters, "entity_handle");
        var basePoint = GetPointParam(parameters, "base_point");
        var scaleFactor = GetParam<double>(parameters, "scale_factor");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, entityHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Entity not found."));

            var entity = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
            if (entity == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Entity."));

            var pt = new Point3d(basePoint[0], basePoint[1], basePoint.Length > 2 ? basePoint[2] : 0);
            entity.TransformBy(Matrix3d.Scaling(scaleFactor, pt));
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Entity scaled by factor {scaleFactor} around ({pt.X:F2}, {pt.Y:F2}).");
            result.ModifiedHandles.Add(entityHandle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "entity_handle")))
            return ValidationResult.Invalid("entity_handle is required");
        if (GetPointParam(parameters, "base_point").Length < 2)
            return ValidationResult.Invalid("base_point requires [x, y]");
        if (GetParam<double>(parameters, "scale_factor") <= 0)
            return ValidationResult.Invalid("scale_factor must be positive");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["entity_handle", "base_point", "scale_factor"],
        "properties": {
            "entity_handle": { "type": "string" },
            "base_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y]" },
            "scale_factor": { "type": "number", "description": "Scale factor (>0)" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class ArrayEntityTool : CadToolBase
{
    public override string Name => "ArrayEntity";
    public override string Description => "Creates rectangular or polar array copies of an entity.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entityHandle = GetParam<string>(parameters, "entity_handle");
        var arrayType = GetParam(parameters, "array_type", "rectangular");
        var rows = GetParam(parameters, "rows", 1);
        var columns = GetParam(parameters, "columns", 1);
        var rowSpacing = GetParam(parameters, "row_spacing", 10.0);
        var columnSpacing = GetParam(parameters, "column_spacing", 10.0);
        var centerPoint = GetPointParam(parameters, "center");
        var count = GetParam(parameters, "count", 6);
        var angle = GetParam(parameters, "fill_angle", 360.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, entityHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Entity not found."));

            var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
            if (entity == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Entity."));

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var createdHandles = new List<string>();

            if (arrayType.Equals("rectangular", StringComparison.OrdinalIgnoreCase))
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        if (r == 0 && c == 0) continue; // skip original position

                        var clone = entity.Clone() as Entity;
                        if (clone != null)
                        {
                            var displacement = new Vector3d(c * columnSpacing, r * rowSpacing, 0);
                            clone.TransformBy(Matrix3d.Displacement(displacement));
                            btr.AppendEntity(clone);
                            tr.AddNewlyCreatedDBObject(clone, true);
                            createdHandles.Add(clone.Handle.ToString());
                        }
                    }
                }
            }
            else if (arrayType.Equals("polar", StringComparison.OrdinalIgnoreCase))
            {
                if (centerPoint.Length < 2)
                    return Task.FromResult(ToolResult.Fail(Name, "center is required for polar array."));

                var center = new Point3d(centerPoint[0], centerPoint[1], 0);
                var angleDelta = angle / count * Math.PI / 180.0;

                for (int i = 1; i < count; i++)
                {
                    var clone = entity.Clone() as Entity;
                    if (clone != null)
                    {
                        clone.TransformBy(Matrix3d.Rotation(angleDelta * i, Vector3d.ZAxis, center));
                        btr.AppendEntity(clone);
                        tr.AddNewlyCreatedDBObject(clone, true);
                        createdHandles.Add(clone.Handle.ToString());
                    }
                }
            }

            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Array created: {createdHandles.Count} copies ({arrayType}).");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "entity_handle")))
            return ValidationResult.Invalid("entity_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["entity_handle"],
        "properties": {
            "entity_handle": { "type": "string" },
            "array_type": { "type": "string", "enum": ["rectangular", "polar"], "default": "rectangular" },
            "rows": { "type": "integer", "default": 1 },
            "columns": { "type": "integer", "default": 1 },
            "row_spacing": { "type": "number", "default": 10.0 },
            "column_spacing": { "type": "number", "default": 10.0 },
            "center": { "type": "array", "items": { "type": "number" }, "description": "[x, y] for polar array" },
            "count": { "type": "integer", "default": 6, "description": "Number of items for polar array" },
            "fill_angle": { "type": "number", "default": 360.0, "description": "Total angle for polar array (degrees)" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class OffsetEntityTool : CadToolBase
{
    public override string Name => "OffsetEntity";
    public override string Description => "Creates an offset copy of a curve entity (line, polyline, arc, circle) at a specified distance.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entityHandle = GetParam<string>(parameters, "entity_handle");
        var distance = GetParam<double>(parameters, "distance");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, entityHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Entity not found."));

            var entity = tr.GetObject(objId, OpenMode.ForRead) as Curve;
            if (entity == null) return Task.FromResult(ToolResult.Fail(Name, "Entity is not a curve."));

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var offsets = entity.GetOffsetCurves(distance);
            var createdHandles = new List<string>();

            foreach (Entity offsetEnt in offsets)
            {
                btr.AppendEntity(offsetEnt);
                tr.AddNewlyCreatedDBObject(offsetEnt, true);
                createdHandles.Add(offsetEnt.Handle.ToString());
            }

            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Offset at distance {distance}: {createdHandles.Count} entity(ies) created.");
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
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "entity_handle")))
            return ValidationResult.Invalid("entity_handle is required");
        if (GetParam<double>(parameters, "distance") == 0)
            return ValidationResult.Invalid("distance must be non-zero");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["entity_handle", "distance"],
        "properties": {
            "entity_handle": { "type": "string" },
            "distance": { "type": "number", "description": "Offset distance (positive=right, negative=left relative to direction)" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class FilletEntitiesTool : CadToolBase
{
    public override string Name => "FilletEntities";
    public override string Description => "Creates a fillet (rounded corner) between two entities with a specified radius.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entity1Handle = GetParam<string>(parameters, "entity1_handle");
        var entity2Handle = GetParam<string>(parameters, "entity2_handle");
        var radius = GetParam<double>(parameters, "radius");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();

        try
        {
            // Fillet is best done through the FILLET command with FILLETRAD
            doc.SendStringToExecute($"FILLETRAD {radius:F6}\n", true, false, false);
            doc.SendStringToExecute("FILLET\n", true, false, false);

            return Task.FromResult(ToolResult.Ok(Name,
                $"Fillet initiated with radius {radius}. Select entities to complete."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (GetParam<double>(parameters, "radius") < 0)
            return ValidationResult.Invalid("radius must be non-negative");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["radius"],
        "properties": {
            "entity1_handle": { "type": "string" },
            "entity2_handle": { "type": "string" },
            "radius": { "type": "number", "description": "Fillet radius" }
        }
    }
    """);
}

public sealed class ChamferEntitiesTool : CadToolBase
{
    public override string Name => "ChamferEntities";
    public override string Description => "Creates a chamfer (angled cut) between two entities with specified distances.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var distance1 = GetParam(parameters, "distance1", 5.0);
        var distance2 = GetParam(parameters, "distance2", 5.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();

        try
        {
            doc.SendStringToExecute($"CHAMFERA {distance1:F6}\n", true, false, false);
            doc.SendStringToExecute($"CHAMFERB {distance2:F6}\n", true, false, false);
            doc.SendStringToExecute("CHAMFER\n", true, false, false);

            return Task.FromResult(ToolResult.Ok(Name,
                $"Chamfer initiated with distances {distance1}/{distance2}. Select entities to complete."));
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
        "properties": {
            "distance1": { "type": "number", "default": 5.0, "description": "First chamfer distance" },
            "distance2": { "type": "number", "default": 5.0, "description": "Second chamfer distance" }
        }
    }
    """);
}

public sealed class TrimEntityTool : CadToolBase
{
    public override string Name => "TrimEntity";
    public override string Description => "Trims an entity at a cutting edge (boundary entity).";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();

        try
        {
            doc.SendStringToExecute("TRIM\n", true, false, false);
            return Task.FromResult(ToolResult.Ok(Name, "TRIM command initiated. Select cutting edges and objects to trim."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}

public sealed class ExtendEntityTool : CadToolBase
{
    public override string Name => "ExtendEntity";
    public override string Description => "Extends an entity to meet a boundary edge.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();

        try
        {
            doc.SendStringToExecute("EXTEND\n", true, false, false);
            return Task.FromResult(ToolResult.Ok(Name, "EXTEND command initiated. Select boundary edges and objects to extend."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => new JObject { ["type"] = "object", ["properties"] = new JObject() };
}

public sealed class ExplodeEntityTool : CadToolBase
{
    public override string Name => "ExplodeEntity";
    public override string Description => "Explodes a complex entity (block, polyline, dimension) into its component entities.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entityHandle = GetParam<string>(parameters, "entity_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, entityHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Entity not found."));

            var entity = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
            if (entity == null) return Task.FromResult(ToolResult.Fail(Name, "Handle is not an Entity."));

            var explodedEntities = new DBObjectCollection();
            entity.Explode(explodedEntities);

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var createdHandles = new List<string>();
            foreach (DBObject obj in explodedEntities)
            {
                if (obj is Entity explodedEnt)
                {
                    btr.AppendEntity(explodedEnt);
                    tr.AddNewlyCreatedDBObject(explodedEnt, true);
                    createdHandles.Add(explodedEnt.Handle.ToString());
                }
            }

            entity.Erase();
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"Entity exploded into {createdHandles.Count} components.");
            result.CreatedHandles.AddRange(createdHandles);
            result.DeletedHandles.Add(entityHandle);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "entity_handle")))
            return ValidationResult.Invalid("entity_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["entity_handle"],
        "properties": { "entity_handle": { "type": "string" } }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}

public sealed class MeasureDistanceTool : CadToolBase
{
    public override string Name => "MeasureDistance";
    public override string Description => "Measures the distance between two points or the length of an entity.";
    public override string Category => "AutoCAD";

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var point1 = GetPointParam(parameters, "point1");
        var point2 = GetPointParam(parameters, "point2");
        var entityHandle = GetParam<string>(parameters, "entity_handle");

        var doc = Application.DocumentManager.MdiActiveDocument;

        if (!string.IsNullOrEmpty(entityHandle))
        {
            using var tr = doc.TransactionManager.StartTransaction();
            var objId = GetObjectIdFromHandle(doc.Database, entityHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Entity not found."));

            var entity = tr.GetObject(objId, OpenMode.ForRead);
            double length = 0;
            string entityType = entity.GetType().Name;

            if (entity is Curve curve)
            {
                length = curve.GetDistanceAtParameter(curve.EndParam);
            }

            tr.Commit();
            return Task.FromResult(ToolResult.Ok(Name,
                $"{entityType} length: {length:F6}",
                new Dictionary<string, object> { ["length"] = length, ["entity_type"] = entityType }));
        }

        if (point1.Length >= 2 && point2.Length >= 2)
        {
            var pt1 = new Point3d(point1[0], point1[1], point1.Length > 2 ? point1[2] : 0);
            var pt2 = new Point3d(point2[0], point2[1], point2.Length > 2 ? point2[2] : 0);
            var distance = pt1.DistanceTo(pt2);
            var dx = pt2.X - pt1.X;
            var dy = pt2.Y - pt1.Y;
            var dz = pt2.Z - pt1.Z;
            var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            return Task.FromResult(ToolResult.Ok(Name,
                $"Distance: {distance:F6}, Angle: {angle:F2}°, ΔX={dx:F3}, ΔY={dy:F3}, ΔZ={dz:F3}",
                new Dictionary<string, object>
                {
                    ["distance"] = distance,
                    ["angle_degrees"] = angle,
                    ["delta_x"] = dx,
                    ["delta_y"] = dy,
                    ["delta_z"] = dz
                }));
        }

        return Task.FromResult(ToolResult.Fail(Name, "Provide either two points or an entity_handle."));
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters) =>
        ValidationResult.Ok();

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "properties": {
            "point1": { "type": "array", "items": { "type": "number" }, "description": "[x, y, z]" },
            "point2": { "type": "array", "items": { "type": "number" }, "description": "[x, y, z]" },
            "entity_handle": { "type": "string", "description": "Handle of entity to measure" }
        }
    }
    """);

    private static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}
