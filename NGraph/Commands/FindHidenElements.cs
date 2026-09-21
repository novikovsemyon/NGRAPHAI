using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using NGraph.Core;
using Nice3point.Revit.Toolkit.External;



namespace NGraph.Commands;

/// <summary>
///     Info for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class FindHidenElements : ExternalCommand
{


    public override void Execute()
    {
        
        
        
        
        
        var hiddenElements= new FilteredElementCollector(Document)
            .OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
            .Where(x =>x.IsHidden(ActiveView)).
            Select(x => x).ToList();
			
       
        
        var elements = Document.GetElements(ActiveView?.Id);

        //Элементы из бызы отфильтрованные по нужным категориям
        var filteredElements = elements
            .Where(x => x.GetType() == typeof(FamilyInstance)
                     //   || (x.GetType() == typeof(Group) &&  x.Location != null) //Принадлежит группе и эта группа не входит в другую
                     //   || x.GetType() == typeof(DetailLine)
                       // || x.GetType() == typeof(TextNote)
                        & x.IsHidden(ActiveView) //Все скрытые элементы
            );
        filteredElements.Count();

    }

}


