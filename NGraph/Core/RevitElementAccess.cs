namespace NGraph.Core;

/// <summary>Проверки обязательных данных и совместимость идентификаторов Revit.</summary>
internal static class RevitElementAccess
{
    public static XYZ GetPlacementPoint(this Element element)
    {
        if (element.Location is LocationPoint location)
            return location.Point;

        throw new InvalidOperationException($"У элемента {element.Id} «{element.Name}» нет точки размещения.");
    }

    public static ElementId CreateId(long value)
    {
#if REVIT2024_OR_GREATER
        return new ElementId(value);
#else
        return new ElementId(checked((int)value));
#endif
    }
}
