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
    public static XYZ GetGabarit(FamilySymbol FamilySymbol, View ViewDrafting)
    {
        double Xmax = FamilySymbol.get_BoundingBox(ViewDrafting).Max.X;
        double Xmin = FamilySymbol.get_BoundingBox(ViewDrafting).Min.X;
        double Ymax = FamilySymbol.get_BoundingBox(ViewDrafting).Max.Y;
        double Ymin = FamilySymbol.get_BoundingBox(ViewDrafting).Min.Y;
        double dX = Xmax - Xmin;
        double dY = Ymax - Ymin;
        //return new XYZ(dX*10, dY*10, 0); //Учитываем на чертежном виде масштаб 1 к 10
        return new XYZ(dX, dY, 0); //Учитываем на чертежном виде масштаб 1 к 10
    }



}


