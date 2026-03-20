using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;

namespace Civil3DAIAddon.Ribbon;

public sealed class RibbonCommandHandler : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is RibbonButton button)
        {
            var command = button.CommandParameter?.ToString();
            if (!string.IsNullOrEmpty(command))
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute($"{command}\n", true, false, true);
            }
        }
    }
}
