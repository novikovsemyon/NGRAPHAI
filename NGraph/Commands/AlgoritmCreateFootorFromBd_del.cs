using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

/// <summary>Вход в общий каталог. Состав и вставка обрабатываются в DatabaseCatalog.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AlgoritmCreateFootorFromBd_del : ExternalCommand
{
    public override void Execute() => DatabaseCatalog.Run(Application, true);
}
