using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;

namespace Civil3DAIAddon.Ribbon;

public sealed class RibbonBuilder
{
    private const string TabId = "AICIVIL_TAB";
    private const string TabTitle = "AI Civil";

    public void BuildRibbon()
    {
        var ribbonControl = ComponentManager.Ribbon;
        if (ribbonControl == null)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor
                .WriteMessage("\n[Civil3D AI Addon] Ribbon not available, will retry on next idle.");
            return;
        }

        if (ribbonControl.FindTab(TabId) != null)
            return;

        var tab = new RibbonTab
        {
            Title = TabTitle,
            Id = TabId,
            IsActive = false
        };

        tab.Panels.Add(CreateMainPanel());
        tab.Panels.Add(CreateToolsPanel());
        tab.Panels.Add(CreateViewPanel());

        ribbonControl.Tabs.Add(tab);
    }

    private static RibbonPanel CreateMainPanel()
    {
        var panelSource = new RibbonPanelSource { Title = "Assistant" };
        var panel = new RibbonPanel { Source = panelSource };

        panelSource.Items.Add(CreateLargeButton(
            "Open Assistant", "AICIVIL_OPEN",
            "Open the AI Assistant panel"));

        panelSource.Items.Add(CreateLargeButton(
            "Analyze Drawing", "AICIVIL_ANALYZE",
            "Analyze current drawing context"));

        return panel;
    }

    private static RibbonPanel CreateToolsPanel()
    {
        var panelSource = new RibbonPanelSource { Title = "Execution" };
        var panel = new RibbonPanel { Source = panelSource };

        var row = new RibbonRowPanel();

        row.Items.Add(CreateSmallButton(
            "Dry Run", "AICIVIL_DRYRUN",
            "Execute prompt in dry-run mode (preview only)"));

        row.Items.Add(new RibbonRowBreak());

        row.Items.Add(CreateSmallButton(
            "Execute", "AICIVIL_EXECUTE",
            "Execute prompt and apply changes to drawing"));

        panelSource.Items.Add(row);

        return panel;
    }

    private static RibbonPanel CreateViewPanel()
    {
        var panelSource = new RibbonPanelSource { Title = "View" };
        var panel = new RibbonPanel { Source = panelSource };

        var row = new RibbonRowPanel();

        row.Items.Add(CreateSmallButton(
            "Settings", "AICIVIL_SETTINGS",
            "Configure AI addon settings"));

        row.Items.Add(new RibbonRowBreak());

        row.Items.Add(CreateSmallButton(
            "History", "AICIVIL_HISTORY",
            "View conversation history"));

        row.Items.Add(new RibbonRowBreak());

        row.Items.Add(CreateSmallButton(
            "Logs", "AICIVIL_LOGS",
            "View action logs"));

        panelSource.Items.Add(row);

        return panel;
    }

    private static RibbonButton CreateLargeButton(string text, string command, string tooltip)
    {
        return new RibbonButton
        {
            Text = text,
            ShowText = true,
            ShowImage = true,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            CommandParameter = command,
            CommandHandler = new RibbonCommandHandler(),
            ToolTip = tooltip
        };
    }

    private static RibbonButton CreateSmallButton(string text, string command, string tooltip)
    {
        return new RibbonButton
        {
            Text = text,
            ShowText = true,
            Size = RibbonItemSize.Standard,
            CommandParameter = command,
            CommandHandler = new RibbonCommandHandler(),
            ToolTip = tooltip
        };
    }
}
