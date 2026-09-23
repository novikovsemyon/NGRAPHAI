using Autodesk.Revit.Attributes;
using System.Windows.Interop;
using NGraph.ViewModels;
using NGraph.Views;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

/// <summary>Открывает настройки модально; окно принадлежит Revit и не скрывается за ним.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class NGraphSettings : ExternalCommand
{
    public override void Execute()
    {
        var view = new NGraphSettingsView(new NGraphSettingsViewModel(Application.ActiveUIDocument.Document));
        new WindowInteropHelper(view).Owner = Application.MainWindowHandle;
        view.ShowDialog();
    }
}
