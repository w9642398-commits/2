namespace Civil3DAIAddon.Models.AI;

public sealed class AIRequest
{
    public string UserPrompt { get; set; } = string.Empty;
    public DrawingSnapshot DrawingSnapshot { get; set; } = new();
    public List<ToolDefinition> AvailableTools { get; set; } = new();
    public List<string> SafetyRules { get; set; } = new();
    public List<ConversationMessage> ConversationHistory { get; set; } = new();
    public ExecutionMode Mode { get; set; } = ExecutionMode.DryRun;
}

public sealed class DrawingSnapshot
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public List<LayerInfo> Layers { get; set; } = new();
    public List<EntitySummary> SelectedEntities { get; set; } = new();
    public List<EntitySummary> VisibleEntities { get; set; } = new();
    public List<CivilObjectSummary> CivilObjects { get; set; } = new();
    public ViewportInfo ActiveViewport { get; set; } = new();
    public List<string> AvailableStyles { get; set; } = new();
    public int TotalEntityCount { get; set; }
}

public sealed class LayerInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsOn { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsLocked { get; set; }
    public int Color { get; set; }
    public int EntityCount { get; set; }
}

public sealed class EntitySummary
{
    public string Handle { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Layer { get; set; } = string.Empty;
    public BoundingBox? Bounds { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

public sealed class CivilObjectSummary
{
    public string Handle { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Style { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

public sealed class BoundingBox
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}

public sealed class ViewportInfo
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double ViewDirection { get; set; }
}

public sealed class ConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum ExecutionMode
{
    DryRun,
    Execute
}
