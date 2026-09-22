using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using NGraph.Views;
namespace NGraph.Commands;
/// <summary>Открывает внешнюю базу ХОВС. Документ Revit не используется для выбора объекта.</summary>
[Transaction(TransactionMode.Manual)]
public class HovsWorkspaceCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            var window = new HovsWorkspaceWindow();
            new System.Windows.Interop.WindowInteropHelper(window).Owner = Application.MainWindowHandle;
            window.ShowDialog();
        }
        catch (Exception ex) { TaskDialog.Show("NGraph — ХОВС", "Не удалось открыть базу: " + ex.Message); }
    }
}
