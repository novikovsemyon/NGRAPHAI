using Autodesk.Revit.Attributes;
using NGraph.Core.ModelSchemes;

namespace NGraph.Commands;

/// <summary>Отдельная команда с начальным режимом группировки по параметрам оборудования.</summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public sealed class CreateSxemaByModelWithoutSpaceAndLevel : CreateSxemaByModel
{
    protected override SchemeGroupingMode InitialMode => SchemeGroupingMode.Parameters;
}
