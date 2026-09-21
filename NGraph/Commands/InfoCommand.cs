using Autodesk.Revit.Attributes;
using System.Windows.Interop;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;

namespace NGraph.Commands;

/// <summary>Показывает встроенную справку и фактические версии программы и Revit.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class InfoCommand : ExternalCommand
{
    public override void Execute()
    {
        var view = new NGraphView(new NGraphViewModel { RevitVersion = Application.Application.VersionNumber });
        new WindowInteropHelper(view).Owner = Application.MainWindowHandle;
        view.ShowDialog();
    }
}
