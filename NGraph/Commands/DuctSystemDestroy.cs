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
        Element element = Helpers.SelectElementId(BuiltInCategory.OST_DuctCurves, UiDocument, Document);
        Helpers helpers = new Helpers();

        using (Transaction tr = new Transaction(Document, "NS_СonnectDuctMEPSystem"))
        {
            tr.Start();
            try
            {
                helpers.DisConnectDuctCurveWith((element as Duct).MEPSystem, Document, BuiltInCategory.OST_ElectricalEquipment);

            }
            catch { }
            tr.Commit();
        }
        

    }
    
}