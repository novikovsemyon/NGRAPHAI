
namespace NGraph.Core.FunctionalScheme;

/// <summary>
/// Представление прибора по пересекающимся точкам FSAheader
/// </summary>
public class ElementHeader: IComparable<ElementHeader>
{
    public int  index { get; set; }
    public string Param_NS_GOST { get; }
    public string Param_NS_Equpment { get; }
    /// <summary>
    /// Список пересекающихся точек
    /// </summary>
    public Parameter Parameter_position_number { get; }
    /// <summary>
    /// Список пересекающихся точек
    /// </summary>
    public List<FSAheader> fSAheaders { get; set; } = [];
    
    /// <summary>
    /// Список точек оборудования
    /// </summary>
    public List<FSAheader> fSAheadersEQ { get; } = [];


    
    public FamilyInstance FamilyInstance { get; }

    

    public ElementHeader()
    {
        
    }
    public ElementHeader(FamilyInstance familyInstance, View v, List<FSAheader> FSAheaders)
    {
        FamilyInstance = familyInstance;
        foreach (var fh in FSAheaders)
        {
            if (IsCrossingElement_withHimself(familyInstance, fh.FI, v))
            {
                fSAheadersEQ.Add(fh);
                fh.ElementHeader = this;
                Param_NS_GOST = familyInstance.LookupParameter(Const.Param_NS_GOST).AsString();
                Param_NS_Equpment = familyInstance.LookupParameter(Const.Param_NS_Equpment).AsString();

            }
            
        }
        
        
    }

   
    
    
    public static bool IsCrossingElement_withHimself(FamilyInstance h1, FamilyInstance h2, View v)
    {
        return Helpers.Overlaps(h1.get_BoundingBox(v), h2.get_BoundingBox(v));
    }
    
    
    public static bool IsCrossingElement_withHimself(FSAheader h1, FSAheader h2, View v)
    {
        return Helpers.Overlaps(h1.FI.get_BoundingBox(v), h2.FI.get_BoundingBox(v));
    }
    
    /*
    public static bool IsHaveCrossingElement(FSAheader h1, List<FSAheader> headers, View v)
    {
        return headers.Any(i =>

            h1 != i
            &&
            GeometryExtensions.Overlaps(h1.FI.get_BoundingBox(v), i.FI.get_BoundingBox(v))

        );

    }
    */
    public int CompareTo(ElementHeader? obj)
    {
        return int.Parse(Parameter_position_number.AsString()) - int.Parse(obj.Parameter_position_number.AsString());

    }


}