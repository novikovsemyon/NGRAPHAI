using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using NGraph.Core;

namespace NGraph.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class SelectInModelByElementId : ExternalCommand
{
    public override void Execute()
    {
        List<Element> elements;
        try
        {
            elements = Helpers.SelectElementsId(BuiltInCategory.OST_DetailComponents, Application.ActiveUIDocument);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return;
        }

        var elementIds = new HashSet<ElementId>();
        foreach (var element in elements)
        {
            var parameter = element.LookupParameter("NS_ElementId");
            if (parameter is null || parameter.StorageType != StorageType.String ||
                !long.TryParse(parameter.AsString(), out var value) || value <= 0)
            {
                continue;
            }
#if REVIT2024_OR_GREATER
            var id = new ElementId(value);
#else
            if (value > int.MaxValue) continue;
            var id = new ElementId((int)value);
#endif
            if (Application.ActiveUIDocument.Document.GetElement(id) is not null)
            {
                elementIds.Add(id);
            }
        }

        if (elementIds.Count == 0)
        {
            TaskDialog.Show("NGraph", "В выбранных элементах нет действующих ссылок NS_ElementId на элементы модели.");
            return;
        }

        Application.ActiveUIDocument.Selection.SetElementIds(elementIds);
        Application.ActiveUIDocument.ShowElements(elementIds);
    }
}
