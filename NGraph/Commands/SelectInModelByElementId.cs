using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using NGraph.Core;

namespace NGraph.Commands;

/// <summary>
///     External command entry point.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class SelectInModelByElementId : ExternalCommand
{
    public override void Execute()
    {
        List<Element> elements = Helpers.SelectElementsId(BuiltInCategory.OST_DetailComponents, UiDocument);
        var filteredelements = elements.Where(i =>
            int.TryParse(

                i.LookupParameter("NS_ElementId").AsString(),out _) 
 
        ).ToList();
        var elementsId = filteredelements.ConvertAll
        (
            
                (e=> new ElementId(int.Parse(e.LookupParameter("NS_ElementId").AsString())))
        );

        UiDocument.Selection.SetElementIds(elementsId);
        UiDocument.ShowElements(elementsId);
    }
}