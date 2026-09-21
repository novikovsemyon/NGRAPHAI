namespace NGraph.Core;

/// <summary>
/// Геометрия в поле
/// </summary>
public class Gabarit
{
    
    /// <summary>
    /// Габариты
    /// </summary>
    public XYZ deltaXYZ { get; set; } = new XYZ();
    
    /// <summary>
    /// Координаты Revit
    /// </summary>
    public XYZ XYZ_begin {  get; set; } = new XYZ();

    /// <summary>
    /// Координаты Revit
    /// </summary>
    public XYZ XYZ_end { get; set; } = new XYZ();


   

    /// <summary>
    /// Габарит символа (
    /// </summary>
    /// <param name="FamilySymbol"></param>
    /// <param name="ViewDrafting"></param>
    /// <returns></returns>
    public static XYZ GetGabarit(FamilySymbol familySymbol, View viewDrafting)
    {
        ArgumentNullException.ThrowIfNull(familySymbol);
        ArgumentNullException.ThrowIfNull(viewDrafting);

        var boundingBox = familySymbol.get_BoundingBox(viewDrafting)
            ?? throw new InvalidOperationException($"Не удалось получить габариты семейства '{familySymbol.Name}'.");

        return new XYZ(
            boundingBox.Max.X - boundingBox.Min.X,
            boundingBox.Max.Y - boundingBox.Min.Y,
            0);
    }



}


