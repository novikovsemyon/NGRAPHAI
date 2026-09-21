namespace NGraph.Core.FunctionalScheme;
/// <summary>
/// Сущость элемента на основе экземпляра Revit 
/// </summary>
public class FSAentity
{
    public string Name { get;}
    public ElementId ID { get; }
    public XYZ XYZ { get; set; }
    public bool Pinned { get; set; }

    public FamilyInstance FI { get;}

    /// <summary>
    /// _Установка
    /// </summary>
    public string Group { get; set; }


    public FSAentity(FamilyInstance fi)
    {
        FI = fi;
        ID = fi.Id;
        XYZ = (fi.Location as LocationPoint).Point;
        Pinned = fi.Pinned;
        Name = fi.Name;
    }

}
