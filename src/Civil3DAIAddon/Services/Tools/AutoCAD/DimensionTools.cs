using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools.AutoCAD;

// ─── Dimension and Annotation Tools ──────────────────────────────────────────

public sealed class CreateAlignedDimensionTool : CadToolBase
{
    public override string Name => "CreateAlignedDimension";
    public override string Description => "Creates an aligned dimension between two points.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pt1 = GetPointParam(parameters, "point1");
        var pt2 = GetPointParam(parameters, "point2");
        var dimLinePoint = GetPointParam(parameters, "dimension_line_point");
        var layer = GetParam(parameters, "layer", "0");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var p1 = new Point3d(pt1[0], pt1[1], 0);
            var p2 = new Point3d(pt2[0], pt2[1], 0);

            var dimPt = dimLinePoint.Length >= 2
                ? new Point3d(dimLinePoint[0], dimLinePoint[1], 0)
                : new Point3d((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2 + 5, 0);

            var dim = new AlignedDimension(p1, p2, dimPt, "", doc.Database.Dimstyle);
            dim.Layer = layer;

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            btr.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Aligned dimension created. Handle: {dim.Handle}");
            result.CreatedHandles.Add(dim.Handle.ToString());
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (GetPointParam(parameters, "point1").Length < 2) return ValidationResult.Invalid("point1 requires [x, y]");
        if (GetPointParam(parameters, "point2").Length < 2) return ValidationResult.Invalid("point2 requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["point1", "point2"],
        "properties": {
            "point1": { "type": "array", "items": { "type": "number" } },
            "point2": { "type": "array", "items": { "type": "number" } },
            "dimension_line_point": { "type": "array", "items": { "type": "number" } },
            "layer": { "type": "string", "default": "0" }
        }
    }
    """);
}

public sealed class CreateLinearDimensionTool : CadToolBase
{
    public override string Name => "CreateLinearDimension";
    public override string Description => "Creates a horizontal or vertical linear dimension between two points.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pt1 = GetPointParam(parameters, "point1");
        var pt2 = GetPointParam(parameters, "point2");
        var dimLinePoint = GetPointParam(parameters, "dimension_line_point");
        var rotation = GetParam(parameters, "rotation", 0.0);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var p1 = new Point3d(pt1[0], pt1[1], 0);
            var p2 = new Point3d(pt2[0], pt2[1], 0);
            var dimPt = dimLinePoint.Length >= 2
                ? new Point3d(dimLinePoint[0], dimLinePoint[1], 0)
                : new Point3d((p1.X + p2.X) / 2, p1.Y + 5, 0);

            var dim = new RotatedDimension(
                rotation * Math.PI / 180.0, p1, p2, dimPt, "", doc.Database.Dimstyle);

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            btr.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            tr.Commit();

            var result = ToolResult.Ok(Name, $"Linear dimension created. Handle: {dim.Handle}");
            result.CreatedHandles.Add(dim.Handle.ToString());
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (GetPointParam(parameters, "point1").Length < 2) return ValidationResult.Invalid("point1 requires [x, y]");
        if (GetPointParam(parameters, "point2").Length < 2) return ValidationResult.Invalid("point2 requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["point1", "point2"],
        "properties": {
            "point1": { "type": "array", "items": { "type": "number" } },
            "point2": { "type": "array", "items": { "type": "number" } },
            "dimension_line_point": { "type": "array", "items": { "type": "number" } },
            "rotation": { "type": "number", "default": 0.0, "description": "Rotation angle in degrees (0=horizontal, 90=vertical)" }
        }
    }
    """);
}

public sealed class CreateRadialDimensionTool : CadToolBase
{
    public override string Name => "CreateRadialDimension";
    public override string Description => "Creates a radial (radius or diameter) dimension for an arc or circle.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var entityHandle = GetParam<string>(parameters, "entity_handle");
        var isDiameter = GetParam(parameters, "is_diameter", false);

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, entityHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Entity not found."));

            var entity = tr.GetObject(objId, OpenMode.ForRead);
            Point3d center;
            double radius;

            if (entity is Circle circle)
            {
                center = circle.Center;
                radius = circle.Radius;
            }
            else if (entity is Arc arc)
            {
                center = arc.Center;
                radius = arc.Radius;
            }
            else
            {
                return Task.FromResult(ToolResult.Fail(Name, "Entity must be a Circle or Arc."));
            }

            var leaderPt = new Point3d(center.X + radius, center.Y, 0);
            Dimension dim;

            if (isDiameter)
            {
                dim = new DiametricDimension(
                    new Point3d(center.X - radius, center.Y, 0),
                    leaderPt,
                    0,
                    "",
                    doc.Database.Dimstyle);
            }
            else
            {
                dim = new RadialDimension(center, leaderPt, 0, "", doc.Database.Dimstyle);
            }

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            btr.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);
            tr.Commit();

            var result = ToolResult.Ok(Name,
                $"{(isDiameter ? "Diameter" : "Radius")} dimension created. Handle: {dim.Handle}");
            result.CreatedHandles.Add(dim.Handle.ToString());
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
            "entity_handle": { "type": "string", "description": "Handle of circle or arc" },
            "is_diameter": { "type": "boolean", "default": false }
        }
    }
    """);

}

public sealed class CreateHatchTool : CadToolBase
{
    public override string Name => "CreateHatch";
    public override string Description => "Creates a hatch pattern inside a closed boundary (polyline/circle).";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var boundaryHandle = GetParam<string>(parameters, "boundary_handle");
        var patternName = GetParam(parameters, "pattern", "ANSI31");
        var scale = GetParam(parameters, "scale", 1.0);
        var angle = GetParam(parameters, "angle", 0.0);
        var layer = GetParam(parameters, "layer", "0");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var objId = GetObjectIdFromHandle(doc.Database, boundaryHandle);
            if (objId.IsNull) return Task.FromResult(ToolResult.Fail(Name, "Boundary entity not found."));

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var hatch = new Hatch();
            btr.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);

            hatch.SetHatchPattern(HatchPatternType.PreDefined, patternName);
            hatch.PatternScale = scale;
            hatch.PatternAngle = angle * Math.PI / 180.0;
            hatch.Layer = layer;
            hatch.Associative = true;

            var ids = new ObjectIdCollection { objId };
            hatch.AppendLoop(HatchLoopTypes.Outermost, ids);
            hatch.EvaluateHatch(true);

            tr.Commit();

            var result = ToolResult.Ok(Name, $"Hatch '{patternName}' created. Handle: {hatch.Handle}");
            result.CreatedHandles.Add(hatch.Handle.ToString());
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(GetParam<string>(parameters, "boundary_handle")))
            return ValidationResult.Invalid("boundary_handle is required");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["boundary_handle"],
        "properties": {
            "boundary_handle": { "type": "string", "description": "Handle of closed boundary entity" },
            "pattern": { "type": "string", "default": "ANSI31", "description": "Hatch pattern name (e.g., ANSI31, EARTH, GRAVEL, SOLID)" },
            "scale": { "type": "number", "default": 1.0 },
            "angle": { "type": "number", "default": 0.0, "description": "Pattern angle in degrees" },
            "layer": { "type": "string", "default": "0" }
        }
    }
    """);

}

public sealed class CreateLeaderTool : CadToolBase
{
    public override string Name => "CreateLeader";
    public override string Description => "Creates a leader (annotation arrow) with text from a point to a label location.";
    public override string Category => "AutoCAD";
    public override SafetyLevel SafetyLevel => SafetyLevel.Moderate;

    public override Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var startPoint = GetPointParam(parameters, "start_point");
        var endPoint = GetPointParam(parameters, "end_point");
        var text = GetParam(parameters, "text", "");
        var layer = GetParam(parameters, "layer", "0");

        var doc = Application.DocumentManager.MdiActiveDocument;
        using var lk = doc.LockDocument();
        using var tr = doc.TransactionManager.StartTransaction();

        try
        {
            var p1 = new Point3d(startPoint[0], startPoint[1], 0);
            var p2 = new Point3d(endPoint[0], endPoint[1], 0);

            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            // Create MText annotation
            if (!string.IsNullOrEmpty(text))
            {
                var mtext = new MText();
                mtext.Location = p2;
                mtext.Contents = text;
                mtext.TextHeight = 2.5;
                mtext.Layer = layer;
                btr.AppendEntity(mtext);
                tr.AddNewlyCreatedDBObject(mtext, true);

                // Create leader pointing to text
                var leader = new Leader();
                leader.Layer = layer;
                leader.AppendVertex(p1);
                leader.AppendVertex(p2);
                leader.Annotation = mtext.ObjectId;
                btr.AppendEntity(leader);
                tr.AddNewlyCreatedDBObject(leader, true);

                tr.Commit();

                var result = ToolResult.Ok(Name, $"Leader with text created. Handle: {leader.Handle}");
                result.CreatedHandles.Add(leader.Handle.ToString());
                result.CreatedHandles.Add(mtext.Handle.ToString());
                return Task.FromResult(result);
            }
            else
            {
                var leader = new Leader();
                leader.Layer = layer;
                leader.AppendVertex(p1);
                leader.AppendVertex(p2);
                btr.AppendEntity(leader);
                tr.AddNewlyCreatedDBObject(leader, true);

                tr.Commit();

                var result = ToolResult.Ok(Name, $"Leader created. Handle: {leader.Handle}");
                result.CreatedHandles.Add(leader.Handle.ToString());
                return Task.FromResult(result);
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail(Name, $"Failed: {ex.Message}", ex.StackTrace));
        }
    }

    public override ValidationResult ValidateParameters(Dictionary<string, object> parameters)
    {
        if (GetPointParam(parameters, "start_point").Length < 2)
            return ValidationResult.Invalid("start_point requires [x, y]");
        if (GetPointParam(parameters, "end_point").Length < 2)
            return ValidationResult.Invalid("end_point requires [x, y]");
        return ValidationResult.Ok();
    }

    protected override JObject BuildParameterSchema() => JObject.Parse("""
    {
        "type": "object",
        "required": ["start_point", "end_point"],
        "properties": {
            "start_point": { "type": "array", "items": { "type": "number" } },
            "end_point": { "type": "array", "items": { "type": "number" } },
            "text": { "type": "string", "default": "" },
            "layer": { "type": "string", "default": "0" }
        }
    }
    """);
}
