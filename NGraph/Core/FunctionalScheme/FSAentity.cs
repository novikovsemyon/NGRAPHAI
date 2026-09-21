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
    public string Group { get; set; } = string.Empty;


    public FSAentity(FamilyInstance fi)
    {
        if (fi is null)
            throw new ArgumentNullException(nameof(fi));
        if (fi.Location is not LocationPoint location)
            throw new InvalidOperationException($"Элемент {fi.Id} не имеет точечного расположения.");

        FI = fi;
        ID = fi.Id;
        XYZ = location.Point;
        Pinned = fi.Pinned;
        Name = fi.Name;
    }

}
