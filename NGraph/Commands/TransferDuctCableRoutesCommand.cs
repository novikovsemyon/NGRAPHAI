using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using NGraph.Core.CableRouting;
using NGraph.Views;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

/// <summary>Переносит номера кабелей со схемы в воздуховоды, а длины маршрутов — в зелёные точки.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public sealed class TransferDuctCableRoutesCommand : ExternalCommand
{
    public override void Execute()
    {
        try { Run(); }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
        catch (Exception ex) { TaskDialog.Show("NGraph — Кабель по воздуховодам", ex.Message); }
    }

    private void Run()
    {
        var document = Application.ActiveUIDocument.Document;
        if (document.ActiveView is not ViewDrafting view)
        { TaskDialog.Show("NGraph", "Откройте структурную схему с расставленными зелёными точками кабелей."); return; }
        var source = new RevitCableSource(document, view);
        if (source.Connections.Count == 0)
        { TaskDialog.Show("NGraph", "На виде нет зелёных точек. Сначала выполните «Расставить кабель» или «Расставить кабель без модели и длины». Проверьте тип сигнальной точки в настройках."); return; }
        var transfer = new RevitCableTransfer(document, source); var saved = ""; var warning = "";
        try { saved = CableRoutingPreferences.Read(CableRoutingPreferences.FilePath, transfer.ViewKey); }
        catch (Exception ex) { warning = "Не удалось прочитать предыдущий выбор параметра: " + ex.Message; }
        var vm = transfer.CreateViewModel(saved);
        var window = new DuctCableView(vm, warning);
        new WindowInteropHelper(window).Owner = Application.MainWindowHandle;
        if (window.ShowDialog() != true) return;
        var result = transfer.Apply(vm);
        try { CableRoutingPreferences.Save(CableRoutingPreferences.FilePath, transfer.ViewKey, vm.Parameter!.Key); }
        catch (Exception ex) { result += "\nДанные записаны, но выбор параметра не сохранён: " + ex.Message; }
        TaskDialog.Show("NGraph — Кабель по воздуховодам", result);
    }
}
