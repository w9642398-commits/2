using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Civil3DAIAddon.Interfaces;
using Civil3DAIAddon.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Civil3DAIAddon.Bootstrap;

public sealed class AddonCommands
{
    [CommandMethod("AICIVIL_OPEN", CommandFlags.Modal)]
    public void OpenAssistant()
    {
        var palette = AIPaletteManager.GetOrCreate();
        palette.Visible = true;
    }

    [CommandMethod("AICIVIL_ANALYZE", CommandFlags.Modal)]
    public void AnalyzeDrawing()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var extractor = AddonInitializer.Services.GetRequiredService<IDrawingContextExtractor>();
        var snapshot = extractor.ExtractSnapshot(ContextScope.AllDrawing);

        doc.Editor.WriteMessage($"\n--- Drawing Analysis ---");
        doc.Editor.WriteMessage($"\nFile: {snapshot.FileName}");
        doc.Editor.WriteMessage($"\nUnits: {snapshot.Units}");
        doc.Editor.WriteMessage($"\nLayers: {snapshot.Layers.Count}");
        doc.Editor.WriteMessage($"\nTotal entities: {snapshot.TotalEntityCount}");
        doc.Editor.WriteMessage($"\nCivil objects: {snapshot.CivilObjects.Count}");
        doc.Editor.WriteMessage($"\n------------------------");
    }

    [CommandMethod("AICIVIL_DRYRUN", CommandFlags.Modal)]
    public void DryRun()
    {
        var palette = AIPaletteManager.GetOrCreate();
        palette.Visible = true;

        var vm = palette.GetViewModel();
        if (vm != null)
            vm.CurrentMode = Models.AI.ExecutionMode.DryRun;
    }

    [CommandMethod("AICIVIL_EXECUTE", CommandFlags.Modal)]
    public void Execute()
    {
        var palette = AIPaletteManager.GetOrCreate();
        palette.Visible = true;

        var vm = palette.GetViewModel();
        if (vm != null)
            vm.CurrentMode = Models.AI.ExecutionMode.Execute;
    }

    [CommandMethod("AICIVIL_SETTINGS", CommandFlags.Modal)]
    public void OpenSettings()
    {
        var settingsWindow = new SettingsWindow();
        Application.ShowModalWindow(settingsWindow);
    }

    [CommandMethod("AICIVIL_HISTORY", CommandFlags.Modal)]
    public void ShowHistory()
    {
        var palette = AIPaletteManager.GetOrCreate();
        palette.Visible = true;

        var vm = palette.GetViewModel();
        vm?.ShowHistoryPanel();
    }

    [CommandMethod("AICIVIL_LOGS", CommandFlags.Modal)]
    public void ShowLogs()
    {
        var palette = AIPaletteManager.GetOrCreate();
        palette.Visible = true;

        var vm = palette.GetViewModel();
        vm?.ShowLogsPanel();
    }
}
