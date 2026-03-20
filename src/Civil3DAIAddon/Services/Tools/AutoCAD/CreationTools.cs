using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.AutoCAD;

public sealed class CreateLineTool : CadToolBase
{
    public override string Name => "CreateLine";
    public override string Description => "Creates a line entity between two points.";
    public override string Category => "Create";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var startPt = GetPointParam(parameters, "start_point");
        var endPt = GetPointParam(parameters, "end_point");
        var layer = GetParam<string>(parameters, "layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var line = new Line(
            new Point3d(startPt[0], startPt[1], startPt.Length > 2 ? startPt[2] : 0),
            new Point3d(endPt[0], endPt[1], endPt.Length > 2 ? endPt[2] : 0));

        if (!string.IsNullOrEmpty(layer))
            line.Layer = layer;

        btr.AppendEntity(line);
        tr.AddNewlyCreatedDBObject(line, true);
        var handle = line.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Line created. Handle: {handle}");
        result.CreatedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var startPt = GetPointParam(parameters, "start_point");
        var endPt = GetPointParam(parameters, "end_point");
        if (startPt.Length < 2) return ValidationResult.Invalid("start_point requires at least [x, y]");
        if (endPt.Length < 2) return ValidationResult.Invalid("end_point requires at least [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["start_point", "end_point"],
        "properties": {
            "start_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y] or [x, y, z]" },
            "end_point": { "type": "array", "items": { "type": "number" }, "description": "[x, y] or [x, y, z]" },
            "layer": { "type": "string", "description": "Target layer name" }
        }
    }
    """);
}

public sealed class CreatePolylineTool : CadToolBase
{
    public override string Name => "CreatePolyline";
    public override string Description => "Creates a polyline through a list of points.";
    public override string Category => "Create";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var points = GetPointsParam(parameters, "points");
        var closed = GetParam(parameters, "closed", false);
        var layer = GetParam<string>(parameters, "layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var pl = new Polyline();
        for (int i = 0; i < points.Count; i++)
        {
            var pt = points[i];
            pl.AddVertexAt(i, new Point2d(pt[0], pt[1]), 0, 0, 0);
        }
        pl.Closed = closed;

        if (!string.IsNullOrEmpty(layer))
            pl.Layer = layer;

        btr.AppendEntity(pl);
        tr.AddNewlyCreatedDBObject(pl, true);
        var handle = pl.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Polyline created with {points.Count} vertices. Handle: {handle}");
        result.CreatedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var points = GetPointsParam(parameters, "points");
        if (points.Count < 2) return ValidationResult.Invalid("At least 2 points are required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["points"],
        "properties": {
            "points": { "type": "array", "items": { "type": "array", "items": { "type": "number" } }, "description": "Array of [x, y] points" },
            "closed": { "type": "boolean", "default": false },
            "layer": { "type": "string" }
        }
    }
    """);
}

public sealed class CreateArcTool : CadToolBase
{
    public override string Name => "CreateArc";
    public override string Description => "Creates an arc entity given center, radius, start angle, and end angle.";
    public override string Category => "Create";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var center = GetPointParam(parameters, "center");
        var radius = GetParam<double>(parameters, "radius");
        var startAngle = GetParam<double>(parameters, "start_angle") * Math.PI / 180.0;
        var endAngle = GetParam<double>(parameters, "end_angle") * Math.PI / 180.0;
        var layer = GetParam<string>(parameters, "layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var arc = new Arc(
            new Point3d(center[0], center[1], center.Length > 2 ? center[2] : 0),
            radius, startAngle, endAngle);

        if (!string.IsNullOrEmpty(layer))
            arc.Layer = layer;

        btr.AppendEntity(arc);
        tr.AddNewlyCreatedDBObject(arc, true);
        var handle = arc.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Arc created. Handle: {handle}");
        result.CreatedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var center = GetPointParam(parameters, "center");
        if (center.Length < 2) return ValidationResult.Invalid("center requires [x, y]");
        if (GetParam<double>(parameters, "radius") <= 0) return ValidationResult.Invalid("radius must be positive");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["center", "radius", "start_angle", "end_angle"],
        "properties": {
            "center": { "type": "array", "items": { "type": "number" } },
            "radius": { "type": "number" },
            "start_angle": { "type": "number", "description": "Start angle in degrees" },
            "end_angle": { "type": "number", "description": "End angle in degrees" },
            "layer": { "type": "string" }
        }
    }
    """);
}

public sealed class CreateCircleTool : CadToolBase
{
    public override string Name => "CreateCircle";
    public override string Description => "Creates a circle entity given center and radius.";
    public override string Category => "Create";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var center = GetPointParam(parameters, "center");
        var radius = GetParam<double>(parameters, "radius");
        var layer = GetParam<string>(parameters, "layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var circle = new Circle(
            new Point3d(center[0], center[1], center.Length > 2 ? center[2] : 0),
            Vector3d.ZAxis, radius);

        if (!string.IsNullOrEmpty(layer))
            circle.Layer = layer;

        btr.AppendEntity(circle);
        tr.AddNewlyCreatedDBObject(circle, true);
        var handle = circle.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Circle created. Handle: {handle}");
        result.CreatedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var center = GetPointParam(parameters, "center");
        if (center.Length < 2) return ValidationResult.Invalid("center requires [x, y]");
        if (GetParam<double>(parameters, "radius") <= 0) return ValidationResult.Invalid("radius must be positive");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["center", "radius"],
        "properties": {
            "center": { "type": "array", "items": { "type": "number" } },
            "radius": { "type": "number" },
            "layer": { "type": "string" }
        }
    }
    """);
}

public sealed class CreateTextTool : CadToolBase
{
    public override string Name => "CreateText";
    public override string Description => "Creates a single-line text (DBText) entity.";
    public override string Category => "Create";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var position = GetPointParam(parameters, "position");
        var textString = GetParam<string>(parameters, "text");
        var height = GetParam(parameters, "height", 2.5);
        var rotation = GetParam(parameters, "rotation", 0.0) * Math.PI / 180.0;
        var layer = GetParam<string>(parameters, "layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var text = new DBText
        {
            Position = new Point3d(position[0], position[1], position.Length > 2 ? position[2] : 0),
            TextString = textString,
            Height = height,
            Rotation = rotation
        };

        if (!string.IsNullOrEmpty(layer))
            text.Layer = layer;

        btr.AppendEntity(text);
        tr.AddNewlyCreatedDBObject(text, true);
        var handle = text.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Text created. Handle: {handle}");
        result.CreatedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var position = GetPointParam(parameters, "position");
        if (position.Length < 2) return ValidationResult.Invalid("position requires [x, y]");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "text")))
            return ValidationResult.Invalid("text is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["position", "text"],
        "properties": {
            "position": { "type": "array", "items": { "type": "number" } },
            "text": { "type": "string" },
            "height": { "type": "number", "default": 2.5 },
            "rotation": { "type": "number", "default": 0, "description": "Rotation in degrees" },
            "layer": { "type": "string" }
        }
    }
    """);
}

public sealed class CreateMTextTool : CadToolBase
{
    public override string Name => "CreateMText";
    public override string Description => "Creates a multi-line text (MText) entity.";
    public override string Category => "Create";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var location = GetPointParam(parameters, "location");
        var contents = GetParam<string>(parameters, "contents");
        var textHeight = GetParam(parameters, "text_height", 2.5);
        var width = GetParam(parameters, "width", 0.0);
        var layer = GetParam<string>(parameters, "layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var mtext = new MText
        {
            Location = new Point3d(location[0], location[1], location.Length > 2 ? location[2] : 0),
            Contents = contents,
            TextHeight = textHeight,
        };

        if (width > 0)
            mtext.Width = width;

        if (!string.IsNullOrEmpty(layer))
            mtext.Layer = layer;

        btr.AppendEntity(mtext);
        tr.AddNewlyCreatedDBObject(mtext, true);
        var handle = mtext.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"MText created. Handle: {handle}");
        result.CreatedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        var location = GetPointParam(parameters, "location");
        if (location.Length < 2) return ValidationResult.Invalid("location requires [x, y]");
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "contents")))
            return ValidationResult.Invalid("contents is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["location", "contents"],
        "properties": {
            "location": { "type": "array", "items": { "type": "number" } },
            "contents": { "type": "string" },
            "text_height": { "type": "number", "default": 2.5 },
            "width": { "type": "number", "default": 0, "description": "MText width, 0 for auto" },
            "layer": { "type": "string" }
        }
    }
    """);
}

public sealed class CreateBlockReferenceTool : CadToolBase
{
    public override string Name => "CreateBlockReference";
    public override string Description => "Inserts a block reference at a given position.";
    public override string Category => "Create";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var blockName = GetParam<string>(parameters, "block_name");
        var position = GetPointParam(parameters, "position");
        var scale = GetParam(parameters, "scale", 1.0);
        var rotation = GetParam(parameters, "rotation", 0.0) * Math.PI / 180.0;
        var layer = GetParam<string>(parameters, "layer");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);

        if (!bt.Has(blockName))
            return Task.FromResult(ToolResult.Fail(Name, $"Block '{blockName}' not found in drawing."));

        var blockId = bt[blockName];
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var blkRef = new BlockReference(
            new Point3d(position[0], position[1], position.Length > 2 ? position[2] : 0),
            blockId)
        {
            ScaleFactors = new Scale3d(scale),
            Rotation = rotation
        };

        if (!string.IsNullOrEmpty(layer))
            blkRef.Layer = layer;

        btr.AppendEntity(blkRef);
        tr.AddNewlyCreatedDBObject(blkRef, true);
        var handle = blkRef.Handle.ToString();
        tr.Commit();

        var result = ToolResult.Ok(Name, $"Block reference '{blockName}' inserted. Handle: {handle}");
        result.CreatedHandles.Add(handle);
        return Task.FromResult(result);
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "block_name")))
            return ValidationResult.Invalid("block_name is required");
        var position = GetPointParam(parameters, "position");
        if (position.Length < 2) return ValidationResult.Invalid("position requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["block_name", "position"],
        "properties": {
            "block_name": { "type": "string" },
            "position": { "type": "array", "items": { "type": "number" } },
            "scale": { "type": "number", "default": 1.0 },
            "rotation": { "type": "number", "default": 0, "description": "Rotation in degrees" },
            "layer": { "type": "string" }
        }
    }
    """);
}
