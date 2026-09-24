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
public class CreateСonnectionsLineOnViewDrafting_NoLength : CreateСonnectionsLineOnViewDraftingBase
{
    
}