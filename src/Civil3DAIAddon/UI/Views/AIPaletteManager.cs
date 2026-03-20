using Autodesk.AutoCAD.Windows;
using Civil3DAIAddon.UI.ViewModels;

namespace Civil3DAIAddon.UI.Views;

public static class AIPaletteManager
{
    private static PaletteSet? _paletteSet;
    private static MainPanelControl? _mainPanel;

    private const string PaletteSetName = "AI Civil 3D";
    private static readonly Guid PaletteSetGuid = new("F3A1B2C3-D4E5-6789-ABCD-EF0123456789");

    public static PaletteSet GetOrCreate()
    {
        if (_paletteSet != null)
            return _paletteSet;

        _paletteSet = new PaletteSet(PaletteSetName, PaletteSetGuid)
        {
            Style = PaletteSetStyles.ShowPropertiesMenu
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowCloseButton,
            MinimumSize = new System.Drawing.Size(400, 500),
            DockEnabled = DockSides.Left | DockSides.Right
        };

        _mainPanel = new MainPanelControl();

        _paletteSet.AddVisual("AI Assistant", _mainPanel);

        return _paletteSet;
    }

    public static MainPanelViewModel? GetViewModel()
    {
        return _mainPanel?.ViewModel;
    }
}
