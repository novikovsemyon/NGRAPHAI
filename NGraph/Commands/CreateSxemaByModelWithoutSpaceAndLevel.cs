using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using NGraph.Core;
using NGraph.Core.ModelSchemes;
using NGraph.Views;
using NGraph.ViewModels;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

/// <summary>Построение по параметрам оборудования; не использует алгоритм CreateSxemaByModel.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public sealed class CreateSxemaByModelWithoutSpaceAndLevel : ExternalCommand
{
    public override void Execute()
    {
        try { ExecuteManager(); }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
        catch (Exception ex) { TaskDialog.Show("NGraph — Менеджер схем", ex.Message); }
    }

    private void ExecuteManager()
    {
        var uiDocument = Application.ActiveUIDocument;
        var document = uiDocument.Document;
        var source = new SchemeSourceReader(document);
        if (source.Elements.Count == 0)
        {
            TaskDialog.Show("NGraph — Менеджер схем", "Не найдено электрооборудование с АК_ в параметре ADSK_Группирование.");
            return;
        }
        var iniDirectory = UserSettings.Load().IniDirectory;
        var viewModel = new ParameterSchemeViewModel(source.Elements, source.Parameters, iniDirectory);
        while (true)
        {
            var window = new ParameterSchemeView(viewModel);
            new WindowInteropHelper(window).Owner = Application.MainWindowHandle;
            if (window.ShowDialog() != true) return;
            ViewDrafting view;
            try { view = new ModelSchemeBuilder(document).Build(viewModel.Plan, viewModel.GetOptions(), iniDirectory); }
            catch (Exception ex)
            {
                // Построитель откатывает вид и все обозначения. Повторное окно сохраняет выбранные источники.
                TaskDialog.Show("NGraph — Схема не построена", ex.Message);
                continue;
            }
            try { uiDocument.ActiveView = view; }
            catch (Exception ex) { TaskDialog.Show("NGraph — Схема создана", $"Вид «{view.Name}» создан. Откройте его в диспетчере проекта.\n{ex.Message}"); }
            return;
        }
    }
}
