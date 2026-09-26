using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using NGraph.Views;

namespace NGraph.Commands;

/// <summary>Только выбор команды. Исходный пространственный алгоритм запускается через его штатную точку входа.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public sealed class SchemeManagerCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var window = new SchemeManagerView();
        new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
        if (window.ShowDialog() != true || !window.SelectedMethod.HasValue) return Result.Cancelled;

        IExternalCommand command = window.SelectedMethod.Value == SchemeBuildMethod.Spaces
            ? new CreateSxemaByModel()
            : new CreateSxemaByModelWithoutSpaceAndLevel();
        // Передаём исходный контекст Revit: не вызываем Execute() Toolkit без его инициализации.
        return command.Execute(commandData, ref message, elements);
    }
}
