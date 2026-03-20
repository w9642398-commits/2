using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Microsoft.Extensions.Logging;

namespace Civil3DAIAddon.Services.Drawing;

public sealed class DrawingContextExtractor : IDrawingContextExtractor
{
    private readonly IConfigurationService _configService;
    private readonly IActionLogger _logger;

    public DrawingContextExtractor(IConfigurationService configService, IActionLogger logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public DrawingSnapshot ExtractSnapshot(ContextScope scope)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
            return new DrawingSnapshot { FileName = "(no document)" };

        var db = doc.Database;
        var config = _configService.Load();
        var snapshot = new DrawingSnapshot
        {
            FileName = System.IO.Path.GetFileName(doc.Name),
            FilePath = doc.Name,
            Units = db.Insunits.ToString(),
            Layers = GetLayers(),
            ActiveViewport = GetViewportInfo(doc.Editor),
        };

        switch (scope)
        {
            case ContextScope.Selection:
                snapshot.SelectedEntities = GetSelectedEntities();
                snapshot.VisibleEntities = new List<EntitySummary>();
                break;
            case ContextScope.CurrentView:
                snapshot.VisibleEntities = GetVisibleEntities(config.MaxContextEntities);
                snapshot.SelectedEntities = GetSelectedEntities();
                break;
            case ContextScope.ModelSpace:
            case ContextScope.AllDrawing:
                snapshot.VisibleEntities = GetVisibleEntities(config.MaxContextEntities);
                snapshot.SelectedEntities = GetSelectedEntities();
                snapshot.CivilObjects = GetCivilObjects();
                break;
        }

        snapshot.TotalEntityCount = CountTotalEntities(db);
        return snapshot;
    }

    public List<EntitySummary> GetSelectedEntities()
    {
        var result = new List<EntitySummary>();
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        var selection = doc.Editor.SelectImplied();
        if (selection.Status != PromptStatus.OK || selection.Value == null)
            return result;

        using var tr = doc.TransactionManager.StartTransaction();
        foreach (SelectedObject selObj in selection.Value)
        {
            if (selObj == null) continue;
            var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
            if (ent != null)
                result.Add(EntityToSummary(ent));
        }
        tr.Commit();

        return result;
    }

    public List<EntitySummary> GetVisibleEntities(int maxCount = 200)
    {
        var result = new List<EntitySummary>();
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        using var tr = doc.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        int count = 0;
        foreach (ObjectId id in btr)
        {
            if (count >= maxCount) break;
            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent != null && ent.Visible)
            {
                result.Add(EntityToSummary(ent));
                count++;
            }
        }
        tr.Commit();

        return result;
    }

    public List<EntitySummary> GetEntitiesByType(string typeName)
    {
        var result = new List<EntitySummary>();
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        using var tr = doc.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in btr)
        {
            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent != null && ent.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                result.Add(EntityToSummary(ent));
        }
        tr.Commit();

        return result;
    }

    public List<EntitySummary> GetEntitiesByLayer(string layerName)
    {
        var result = new List<EntitySummary>();
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        using var tr = doc.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in btr)
        {
            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent != null && ent.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                result.Add(EntityToSummary(ent));
        }
        tr.Commit();

        return result;
    }

    public List<CivilObjectSummary> GetCivilObjects()
    {
        var result = new List<CivilObjectSummary>();
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        try
        {
            using var tr = doc.TransactionManager.StartTransaction();
            var civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            // Alignments
            foreach (ObjectId id in civilDoc.GetAlignmentIds())
            {
                var alignment = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                if (alignment != null)
                {
                    result.Add(new CivilObjectSummary
                    {
                        Handle = alignment.Handle.ToString(),
                        Type = "Alignment",
                        Name = alignment.Name,
                        Style = alignment.StyleName,
                        Properties = new Dictionary<string, string>
                        {
                            ["Length"] = alignment.Length.ToString("F3"),
                            ["StartStation"] = alignment.StartingStation.ToString("F3"),
                            ["EndStation"] = alignment.EndingStation.ToString("F3")
                        }
                    });
                }
            }

            // Surfaces
            foreach (ObjectId id in civilDoc.GetSurfaceIds())
            {
                var surface = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                if (surface != null)
                {
                    var props = new Dictionary<string, string>
                    {
                        ["Type"] = surface.GetType().Name
                    };

                    if (surface is TinSurface tinSurf)
                    {
                        props["MinElevation"] = tinSurf.GetGeneralProperties().MinimumElevation.ToString("F3");
                        props["MaxElevation"] = tinSurf.GetGeneralProperties().MaximumElevation.ToString("F3");
                    }

                    result.Add(new CivilObjectSummary
                    {
                        Handle = surface.Handle.ToString(),
                        Type = "Surface",
                        Name = surface.Name,
                        Style = surface.StyleName,
                        Properties = props
                    });
                }
            }

            tr.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError("CONTEXT", "GetCivilObjects", ex);
        }

        return result;
    }

    public List<LayerInfo> GetLayers()
    {
        var result = new List<LayerInfo>();
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return result;

        using var tr = doc.TransactionManager.StartTransaction();
        var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

        foreach (ObjectId id in lt)
        {
            var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
            result.Add(new LayerInfo
            {
                Name = layer.Name,
                IsOn = !layer.IsOff,
                IsFrozen = layer.IsFrozen,
                IsLocked = layer.IsLocked,
                Color = layer.Color.ColorIndex
            });
        }
        tr.Commit();

        return result;
    }

    public byte[]? CaptureViewportScreenshot()
    {
        // Viewport screenshot capture via AutoCAD API
        // This uses the EXPORT command approach or BMPOUT
        // For production, consider using the ViewportControl rendering
        try
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;

            var tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"aicivil_viewport_{Guid.NewGuid():N}.png");

            // Use PNGOUT command to capture current viewport
            doc.SendStringToExecute($"-PNGOUT\n{tempPath}\n", true, false, false);

            // Wait briefly for file
            if (System.IO.File.Exists(tempPath))
            {
                var bytes = System.IO.File.ReadAllBytes(tempPath);
                System.IO.File.Delete(tempPath);
                return bytes;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("CONTEXT", "CaptureViewportScreenshot", ex);
        }

        return null;
    }

    private static EntitySummary EntityToSummary(Entity ent)
    {
        var summary = new EntitySummary
        {
            Handle = ent.Handle.ToString(),
            Type = ent.GetType().Name,
            Layer = ent.Layer,
        };

        var ext = ent.GeometricExtents;
        summary.Bounds = new BoundingBox
        {
            MinX = ext.MinPoint.X,
            MinY = ext.MinPoint.Y,
            MinZ = ext.MinPoint.Z,
            MaxX = ext.MaxPoint.X,
            MaxY = ext.MaxPoint.Y,
            MaxZ = ext.MaxPoint.Z,
        };

        // Extract type-specific properties
        switch (ent)
        {
            case Line line:
                summary.Properties["StartPoint"] = FormatPoint(line.StartPoint);
                summary.Properties["EndPoint"] = FormatPoint(line.EndPoint);
                summary.Properties["Length"] = line.Length.ToString("F3");
                break;
            case Polyline pl:
                summary.Properties["NumberOfVertices"] = pl.NumberOfVertices.ToString();
                summary.Properties["Length"] = pl.Length.ToString("F3");
                summary.Properties["Closed"] = pl.Closed.ToString();
                break;
            case Circle circle:
                summary.Properties["Center"] = FormatPoint(circle.Center);
                summary.Properties["Radius"] = circle.Radius.ToString("F3");
                break;
            case Arc arc:
                summary.Properties["Center"] = FormatPoint(arc.Center);
                summary.Properties["Radius"] = arc.Radius.ToString("F3");
                summary.Properties["StartAngle"] = arc.StartAngle.ToString("F3");
                summary.Properties["EndAngle"] = arc.EndAngle.ToString("F3");
                break;
            case DBText text:
                summary.Properties["TextString"] = text.TextString;
                summary.Properties["Height"] = text.Height.ToString("F3");
                summary.Properties["Position"] = FormatPoint(text.Position);
                break;
            case MText mtext:
                summary.Properties["Contents"] = mtext.Contents;
                summary.Properties["Location"] = FormatPoint(mtext.Location);
                break;
        }

        return summary;
    }

    private static string FormatPoint(Point3d pt) => $"({pt.X:F3},{pt.Y:F3},{pt.Z:F3})";

    private static int CountTotalEntities(Database db)
    {
        int count = 0;
        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId _ in btr)
            count++;
        tr.Commit();
        return count;
    }

    private static ViewportInfo GetViewportInfo(Editor editor)
    {
        var view = editor.GetCurrentView();
        return new ViewportInfo
        {
            CenterX = view.CenterPoint.X,
            CenterY = view.CenterPoint.Y,
            Width = view.Width,
            Height = view.Height,
        };
    }
}
