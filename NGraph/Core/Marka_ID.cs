namespace NGraph.Core;

/// <summary>
/// Марка приходящего кабеля на структурной схеме.
/// </summary>
public class Marka_ID : Gabarit
{
    public FamilyInstance FamilyInstance { get; set; } = null!;
    public FamilySymbol FamilySymbol { get; }

    public int indexBlockDiagramBaseEqupment { get; set; }
    public int indexBlockDiagramCircuitId { get; set; }
    public int indexGroupSymbol { get; set; }

    /// <summary>
    /// Определяет видимость марки.
    /// </summary>
    public bool IsSet { get; set; }

    public Orientation Orientation { get; }

    public Marka_ID(Document doc, View view, Orientation orientation)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(view);

        Orientation = orientation;
        FamilySymbol = GetSymbol(doc, orientation)
            ?? throw new InvalidOperationException(
                $"Не найдено видовое семейство марки '{orientation}' в категории Detail Components.");

        deltaXYZ = GetGabarit(FamilySymbol, view);
    }

    private static FamilySymbol? GetSymbol(Document document, Orientation orientation)
    {
        return new FilteredElementCollector(document)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_DetailComponents)
            .Cast<FamilySymbol>()
            .FirstOrDefault(symbol =>
                symbol.Family.FamilyPlacementType == FamilyPlacementType.ViewBased &&
                symbol.Name == orientation.ToString());
    }
}
