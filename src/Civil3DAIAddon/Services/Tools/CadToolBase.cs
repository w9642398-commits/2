using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.Models.AI;
using Civil3DAIAddon.Models.Tools;

namespace Civil3DAIAddon.Services.Tools;

public abstract class CadToolBase : ICadTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public virtual SafetyLevel SafetyLevel => SafetyLevel.Safe;
    public virtual bool RequiresConfirmation => SafetyLevel >= SafetyLevel.Destructive;
    public virtual bool SupportsUndo => true;

    public ToolDefinition GetDefinition()
    {
        return new ToolDefinition
        {
            Name = Name,
            Description = Description,
            Category = Category,
            ParameterSchema = BuildParameterSchema(),
            Returns = GetReturnDescription(),
            SafetyLevel = SafetyLevel,
            RequiresConfirmation = RequiresConfirmation,
            SupportsUndo = SupportsUndo
        };
    }

    public abstract Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters);
    public abstract ValidationResult ValidateParameters(Dictionary<string, object> parameters);
    protected abstract JObject BuildParameterSchema();
    protected virtual string GetReturnDescription() => "Operation result with affected object handles.";

    protected static T GetParam<T>(Dictionary<string, object> parameters, string key, T defaultValue = default!)
    {
        if (!parameters.TryGetValue(key, out var value))
            return defaultValue;

        if (value is T typed)
            return typed;

        if (value is JToken jtoken)
            return jtoken.ToObject<T>() ?? defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    protected static double[] GetPointParam(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
            return Array.Empty<double>();

        if (value is JArray arr)
            return arr.Select(t => t.Value<double>()).ToArray();

        if (value is double[] dArr)
            return dArr;

        return Array.Empty<double>();
    }

    protected static List<double[]> GetPointsParam(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
            return new List<double[]>();

        if (value is JArray arr)
            return arr.Select(item =>
            {
                if (item is JArray ptArr)
                    return ptArr.Select(t => t.Value<double>()).ToArray();
                return Array.Empty<double>();
            }).Where(p => p.Length >= 2).ToList();

        return new List<double[]>();
    }

    protected static ObjectId GetObjectIdFromHandle(Database db, string handleStr)
    {
        try { return db.GetObjectId(false, new Handle(Convert.ToInt64(handleStr, 16)), 0); }
        catch { return ObjectId.Null; }
    }
}
