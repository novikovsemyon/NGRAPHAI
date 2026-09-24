using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Views;
using NGraph.ViewModels;
using NGraph.Core.FunctionalScheme;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Extensions.Runtime;

//using Autodesk.Revit.Creation;
namespace NGraph.Commands;

/// <summary>
///     Создание чертежного вида на основании модели. Группирование элементов по параметрам (Рабочий набор/ADSK_Группирование/Уровень/Помещение/ADSK_Позиция/)
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CreateСonnectionsLineOnViewDrafting : CreateСonnectionsLineOnViewDraftingBase
{
    protected override void ApplyAdditionalParameters(FamilyInstance fi, Element begin_element, Element end_element)
    {
        //Получаем расстояние между элементами
        int round = 5000;
        ElementId elementId_begin = RevitElementAccess.CreateId(long.Parse(begin_element.LookupParameter(Const.Param_NS_ElementId).AsString()));
        XYZ xyx_begin = (Application.ActiveUIDocument.Document.GetElement(elementId_begin)
                         ?? throw new InvalidOperationException($"Не найден элемент модели {elementId_begin}.")).GetPlacementPoint();

        ElementId elementId_end = RevitElementAccess.CreateId(long.Parse(end_element.LookupParameter(Const.Param_NS_ElementId).AsString()));
        XYZ xyx_end = (Application.ActiveUIDocument.Document.GetElement(elementId_end)
                       ?? throw new InvalidOperationException($"Не найден элемент модели {elementId_end}.")).GetPlacementPoint();
        int lenght_behind_two_point = Helpers.LenghtInt((int)(((Math.Abs(xyx_begin.X - xyx_end.X) + Math.Abs(xyx_begin.Y - xyx_end.Y) + Math.Abs(xyx_begin.Z - xyx_end.Z)) * 304.8)), round) / 1000;

        fi.LookupParameter(Const.Param_CJ_Lenght).Set(lenght_behind_two_point);
    }

}