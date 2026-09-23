using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using NGraph.Core;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

/// <summary>
///     DisConnect Duct Curve MEPSystem
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class DuctSystemDestroy : ExternalCommand
{
    
    public override void Execute()
    {
        Element element = Helpers.SelectElementId(BuiltInCategory.OST_DuctCurves, Application.ActiveUIDocument, Application.ActiveUIDocument.Document);
        if (element is not Duct duct || duct.MEPSystem is not MechanicalSystem system)
        {
            Autodesk.Revit.UI.TaskDialog.Show("NGraph", "Выберите воздуховод, принадлежащий механической системе.");
            return;
        }
        Helpers helpers = new Helpers();

        using (Transaction tr = new Transaction(Application.ActiveUIDocument.Document, "NS_СonnectDuctMEPSystem"))
        {
            tr.Start();
            try
            {
                helpers.DisConnectDuctCurveWith(system, Application.ActiveUIDocument.Document, BuiltInCategory.OST_ElectricalEquipment);

            }
            catch { }
            tr.Commit();
        }
        

    }
    
}