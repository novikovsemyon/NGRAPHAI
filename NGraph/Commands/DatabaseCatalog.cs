using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Core.FunctionalScheme;
using NGraph.ViewModels;
using NGraph.Views;
namespace NGraph.Commands;
/// <summary>Общий сценарий обеих баз: чтение → выбор → транзакция. Отмена не изменяет документ.</summary>
internal static class DatabaseCatalog
{
    public static void Run(UIApplication application, bool newView)
    {
        var ui = application.ActiveUIDocument;
        var doc = ui.Document;
        var settings = UserSettings.Load();
        var name = newView ? settings.FsaView : settings.ElementsView;
        var source = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>()
            .FirstOrDefault(x => !x.IsTemplate && x.Name == name);
        if (source == null) { TaskDialog.Show("NGraph", "Не найден вид базы: " + name + "\nПроверьте настройки."); return; }
        if (!newView && (doc.ActiveView is not ViewDrafting || doc.ActiveView.Id == source.Id))
        { TaskDialog.Show("NGraph", "Откройте целевой чертёжный вид, отличный от вида базы."); return; }
        try
        {
            var sections = FSAmodelCreateFromDb.CreateFromBd_ReadFilledRegion(doc, source);
            var dialog = new NGraphCreateFootorFromBdView(new NGraphCreateFootorFromBdViewModel {
                Sections = sections, Title = newView ? "База данных — готовые схемы" : "База элементов",
                InsertLabel = newView ? "Создать вид" : "Вставить" });
            new System.Windows.Interop.WindowInteropHelper(dialog).Owner = application.MainWindowHandle;
            if (dialog.ShowDialog() != true || dialog.selectedSections == null) return;
            var section = dialog.selectedSections;
            FSAmodelCreateFromDb.CreateFromBd_AddToSectionOtherElements(doc, source, ref section);
            // Вставка больше не зависит от вспомогательного семейства «Размещение семейств».
            var offset = newView ? XYZ.Zero : ui.Selection.PickPoint("Укажите центр вставки") - section.Центр;
            ViewDrafting target;
            using (var transaction = new Transaction(doc, newView ? "NGraph: схема из базы" : "NGraph: элемент из базы"))
            {
                transaction.Start();
                if (newView)
                {
                    var type = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(x => x.ViewFamily == ViewFamily.Drafting);
                    target = ViewDrafting.Create(doc, type.Id);
                    var proposed = "Схема_" + dialog.ИмяУстановки.Text.Trim();
                    var names = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(View)).Select(x => x.Name));
                    var unique = proposed; int index = 2;
                    while (names.Contains(unique)) unique = proposed + " (" + index++ + ")";
                    target.Name = unique;
                }
                else target = (ViewDrafting)doc.ActiveView;
                FSAmodelCreateFromDb.CreateFromBd_withoutTransaction(doc, ref section, target, dialog.ИмяУстановки.Text.Trim(), offset);
                if (transaction.Commit() != TransactionStatus.Committed) return;
            }
            ui.ActiveView = target;
            if (dialog.CreateFSA) AlgoritmCreateFootor.AlgoritmCreateFootorCommand(doc);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
        catch (Exception ex) { TaskDialog.Show("NGraph", "Ошибка работы с базой: " + ex.Message); }
    }
}
