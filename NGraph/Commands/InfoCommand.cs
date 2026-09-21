using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;

namespace NGraph.Commands;

/// <summary>
///     External command entry point.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class InfoCommand : ExternalCommand
{
    public override void Execute()
    {
        var viewModel = new NGraphViewModel();
        var view = new NGraphView(viewModel);
        view.ShowDialog();
    }
}