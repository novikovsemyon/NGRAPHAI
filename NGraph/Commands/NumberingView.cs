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
        var sheets = Application.ActiveUIDocument.Selection.GetElementIds()
            .Select(Application.ActiveUIDocument.Document.GetElement)
            .OfType<ViewSheet>()
            .OrderBy(sheet => sheet.SheetNumber, new NaturalStringComparer())
            .ToList();
        if (sheets.Count == 0)
        {
            TaskDialog.Show("NGraph", "Выберите листы в диспетчере проекта перед запуском нумерации.");
            return;
        }

        var viewModel = new NGraphNumberingSheetsViewModel(Application.ActiveUIDocument.Document)
        {
            Sheets = sheets
        };

        var viewWindow = new NGraphNumberingSheetsView(viewModel);
        new System.Windows.Interop.WindowInteropHelper(viewWindow).Owner = Application.MainWindowHandle;
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

        if (!int.TryParse(viewWindow.TB_Value.Text, out var page) || page <= 0 ||
            (long)page + sheets.Count - 1 > int.MaxValue)
        {
            TaskDialog.Show("NGraph", "Укажите положительный начальный номер в допустимом диапазоне.");
            return;
        }

        var pageNumberSheet = 1;

        using var transaction = new Transaction(Application.ActiveUIDocument.Document, "NGraph: нумерация листов");
        transaction.Start();

        try
        {
            // Revit requires unique sheet numbers even during a renumbering operation.
            if (selectedParameter.Id == new ElementId(BuiltInParameter.SHEET_NUMBER))
            {
                var temporaryPrefix = "NGraph-" + Guid.NewGuid().ToString("N") + "-";
                for (var i = 0; i < sheets.Count; i++)
                {
                    sheets[i].SheetNumber = temporaryPrefix + i;
                }
            }

            foreach (var element in sheets)
            {

                var pageParameter = NgContext._FindParameter(element, selectedParameter.Definition.Name);
                var sheetNumberParameter = NgContext._FindParameter(element, "Номер листа для выпуска");

                if (pageParameter is null || pageParameter.IsReadOnly)
                {
                    throw new InvalidOperationException(
                        $"На листе «{element.Name}» параметр «{selectedParameter.Definition.Name}» отсутствует или недоступен для записи.");
                }

                SetNumber(pageParameter, page);

                if (sheetNumberParameter is { IsReadOnly: false })
                {
                    if (sheetNumberParameter.Id != pageParameter.Id)
                    {
                        SetNumber(sheetNumberParameter, pageNumberSheet);
                    }
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

    private static void SetNumber(Parameter parameter, int value)
    {
        if ((parameter.StorageType == StorageType.String && parameter.AsString() == value.ToString()) ||
            (parameter.StorageType == StorageType.Integer && parameter.AsInteger() == value))
        {
            return;
        }

        var updated = parameter.StorageType switch
        {
            StorageType.String => parameter.Set(value.ToString()),
            StorageType.Integer => parameter.Set(value),
            _ => throw new InvalidOperationException(
                $"Параметр «{parameter.Definition.Name}» должен быть текстовым или целочисленным.")
        };
        if (!updated)
        {
            throw new InvalidOperationException($"Не удалось записать параметр «{parameter.Definition.Name}».");
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
