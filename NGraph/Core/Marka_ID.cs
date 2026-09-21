namespace NGraph.Core;

/// <summary>
/// Марка приходящего кабеля на стуктурной схеме
/// </summary>
public class Marka_ID : Gabarit
{
    public FamilyInstance FamilyInstance { get; set; }
    public FamilySymbol FamilySymbol { get;}

    //public int indexBlockDiagramId { get; set; } = 0;
    public int indexBlockDiagramBaseEqupment { get; set; } = 0;
    public int indexBlockDiagramCircuitId { get; set; } = 0;
    public int indexGroupSymbol { get; set; } = 0;

    /// <summary>
    /// Определяет видимость марки
    /// </summary>
    public bool IsSet {  get; set; } = false;

    public Orientation Orientation { get;} 



    public Marka_ID(Document doc, View view, Orientation Orientation)
    {
        FamilySymbol = GetSymbol_OST_DetailComponents_ViewBased(doc, Orientation);
        this.Orientation = Orientation;
        //deltaXYZ = GetGabarit(FamilySymbol, view)*0.1;
        deltaXYZ = GetGabarit(FamilySymbol, view);

    }


    FamilySymbol GetSymbol_OST_DetailComponents_ViewBased(Document document, Orientation Name)
    {
        FamilySymbol symbol = null;
        FilteredElementCollector fsCollector = new FilteredElementCollector(document);
        fsCollector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_DetailComponents);
        ICollection<Element> collection = fsCollector.ToElements();
        foreach (Element element in collection)
        {
            FamilySymbol current = element as FamilySymbol;

            if (current.Family.FamilyPlacementType == FamilyPlacementType.ViewBased & current.Name == Name.ToString())
            {
                symbol = current;
                break;
            }
        }

        return symbol;
    }





}