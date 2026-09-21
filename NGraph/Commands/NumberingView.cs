using Autodesk.Revit.Attributes;
using System.Text.RegularExpressions;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.ViewModels;
using NGraph.Views;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

/// <summary>
/// Нумерация выбранных листов.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class NumberingView : ExternalCommand
{
    public override void Execute()
    {
        var viewModel = new NGraphNumberingSheetsViewModel
        {
            Doc = Document
        };

        var viewWindow = new NGraphNumberingSheetsView(viewModel);
        viewWindow.ShowDialog();

        if (viewWindow.Cancel is true)
        {
            return;
        }

        if (viewWindow.CB_Param.SelectedItem is not Parameter selectedParameter)
        {
            TaskDialog.Show("NGraph", "Не выбран параметр для нумерации.");
            return;
        }

        var selectedElementIds = UiDocument.Selection.GetElementIds();
        if (selectedElementIds.Count == 0)
        {
            TaskDialog.Show("NGraph", "Не выбраны листы для нумерации.");
            return;
        }

        var page = int.TryParse(viewWindow.TB_Value.Text, out var parsedPage) && parsedPage > 0
            ? parsedPage
            : 1;
        var pageNumberSheet = 1;

        var sortedSelectedElementIds = selectedElementIds
            .OrderBy(
                id => NgContext._FindParameter(Document.GetElement(id), BuiltInParameter.SHEET_NUMBER)?.AsString() ?? string.Empty,
                new NaturalStringComparer())
            .ToList();

        using var transaction = new Transaction(Document, "NGraph: нумерация листов");
        transaction.Start();

        try
        {
            foreach (var elementId in sortedSelectedElementIds)
            {
                var element = Document.GetElement(elementId);
                if (element is null)
                {
                    continue;
                }

                var pageParameter = NgContext._FindParameter(element, selectedParameter.Definition.Name);
                var sheetNumberParameter = NgContext._FindParameter(element, "Номер листа для выпуска");

                if (pageParameter is null || pageParameter.IsReadOnly)
                {
                    continue;
                }

                pageParameter.Set(page.ToString());

                if (sheetNumberParameter is { IsReadOnly: false })
                {
                    sheetNumberParameter.Set(pageNumberSheet.ToString());
                }

                page++;
                pageNumberSheet++;
            }

            transaction.Commit();
        }
        catch (Exception exception)
        {
            transaction.RollBack();
            TaskDialog.Show("NGraph", $"Ошибка нумерации листов:\n{exception.Message}");
        }
    }

    public sealed class NaturalStringComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y)
        {
            x ??= string.Empty;
            y ??= string.Empty;

            var partsX = Regex.Split(x.Replace(" ", string.Empty), "([0-9]+)");
            var partsY = Regex.Split(y.Replace(" ", string.Empty), "([0-9]+)");

            for (var i = 0; i < Math.Min(partsX.Length, partsY.Length); i++)
            {
                if (i % 2 == 1 &&
                    int.TryParse(partsX[i], out var numX) &&
                    int.TryParse(partsY[i], out var numY))
                {
                    if (numX != numY)
                    {
                        return numX.CompareTo(numY);
                    }
                }
                else
                {
                    var stringCompare = string.Compare(partsX[i], partsY[i], StringComparison.CurrentCulture);
                    if (stringCompare != 0)
                    {
                        return stringCompare;
                    }
                }
            }

            return partsX.Length.CompareTo(partsY.Length);
        }
    }
}
