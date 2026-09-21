namespace NGraph.Core;

/// <summary>
/// Символ на структурной схеме в цепи.
/// </summary>
public class Symbol_ID : Gabarit
{
    public EQ EQ { get; }
    public FamilyInstance FamilyInstance { get; set; } = null!;
    public FamilySymbol FamilySymbol { get; }

    public Symbol_ID(Document doc, View view, EQ eq)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(eq);

        EQ = eq;
        FamilySymbol = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault(symbol => symbol.Name == eq.hisNameSymbolForStruct)
            ?? throw new InvalidOperationException(
                $"Не найдено семейство структурной схемы '{eq.hisNameSymbolForStruct}'.");

        deltaXYZ = GetGabarit(FamilySymbol, view);
    }
}
