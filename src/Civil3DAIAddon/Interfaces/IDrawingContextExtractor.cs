using Civil3DAIAddon.Models.AI;

namespace Civil3DAIAddon.Interfaces;

public interface IDrawingContextExtractor
{
    DrawingSnapshot ExtractSnapshot(ContextScope scope);
    List<EntitySummary> GetSelectedEntities();
    List<EntitySummary> GetVisibleEntities(int maxCount = 200);
    List<EntitySummary> GetEntitiesByType(string typeName);
    List<EntitySummary> GetEntitiesByLayer(string layerName);
    List<CivilObjectSummary> GetCivilObjects();
    List<LayerInfo> GetLayers();
    byte[]? CaptureViewportScreenshot();
}

public enum ContextScope
{
    Selection,
    ModelSpace,
    CurrentView,
    AllDrawing
}
