namespace NGraph.Core;

/// <summary>
/// Символ на структурной схеме в цепи.
/// </summary>
public class Symbol_ID : Gabarit
{
    
    public EQ EQ { get; }
    public FamilyInstance FamilyInstance { get; set; }
    public FamilySymbol FamilySymbol { get; }
    
    public Symbol_ID(Document doc, View view, EQ EQ)
    {

        this.EQ = EQ;
        FamilySymbol = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).First(q => q.Name == EQ.hisNameSymbolForStruct) as FamilySymbol;

        deltaXYZ = GetGabarit(FamilySymbol, view);

    }


}