using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CopyLinesToView : ExternalCommand
{
    public override void Execute()
    {
        var views = UiDocument.Selection.GetElementIds()
            .Select(Document.GetElement)
            .OfType<ViewDrafting>()
            .Where(view => !view.IsTemplate)
            .ToList();
        if (views.Count != 2)
        {
            TaskDialog.Show("NGraph", "Выберите два чертёжных вида для копирования кабельных линий.");
            return;
        }

        var source = views[0];
        var target = views[1];
        var lines = new FilteredElementCollector(Document, source.Id)
            .OfClass(typeof(CurveElement))
            .OfType<DetailLine>()
            .Where(line => line.LineStyle?.Name.Contains("*NG*") == true)
            .ToList();
        if (lines.Count == 0)
        {
            TaskDialog.Show("NGraph", $"На виде «{source.Name}» нет кабельных линий со стилем *NG*.");
            return;
        }

        using var transaction = new Transaction(Document, "NGraph: копирование кабельных линий");
        transaction.Start();
        try
        {
            foreach (var line in lines)
            {
                var copy = Document.Create.NewDetailCurve(target, line.GeometryCurve);
                copy.LineStyle = line.LineStyle;
                copy.Pinned = line.Pinned;
            }
            transaction.Commit();
        }
        catch (Exception exception)
        {
            transaction.RollBack();
            TaskDialog.Show("NGraph", $"Не удалось скопировать кабельные линии:\n{exception.Message}");
        }
    }
}
