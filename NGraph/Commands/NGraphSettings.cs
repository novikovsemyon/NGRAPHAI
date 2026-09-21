using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;

using NGraph.Core;
using NGraph.ViewModels;
using NGraph.Views;
using Nice3point.Revit.Toolkit.External;



namespace NGraph.Commands;

/// <summary>
///     Settings for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class NGraphSettings : ExternalCommand
{
    
    public override void Execute()
    {

        
        var viewModel = new NGraphSettingsViewModel();
        viewModel.Doc = Document;
        var view = new NGraphSettingsView(viewModel);

 
        view.ShowDialog();
        //view.Control.IsChecked = true;

            

        Helpers helpers = new Helpers();
            
        var GenericAnnotation = helpers.AllElementsOfCategory(Document, BuiltInCategory.OST_GenericAnnotation).ToList(); //Типовые аннотации
        var ElectricalEqupment = helpers.AllElementsOfCategory(Document,BuiltInCategory.OST_ElectricalEquipment).ToList(); //Электрооборудование

        var DetailComponents = helpers.AllElementsOfCategory(Document, BuiltInCategory.OST_DetailComponents).
            OfType<FamilyInstance>().
            Where(k=>k.Name == "Горизонтально" || k.Name == "Вертикально").ToList(); //Элементы узлов
        var ElectricalCircuit = helpers.AllElementsOfCategory(Document, BuiltInCategory.OST_ElectricalCircuit).ToList(); //Электрические цепи

        var DetailComponents_FAS = helpers.AllElementsOfCategory(Document, BuiltInCategory.OST_DetailComponents).
            OfType<FamilyInstance>().
            Where(k => k.Name == "FAS_точка" || k.Name == "FAS_точка_без маркировки").ToList(); //Элементы узлов
            

    }
}