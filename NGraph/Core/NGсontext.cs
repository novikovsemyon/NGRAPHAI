using System.Diagnostics.Contracts;

namespace NGraph.Core;

public static class NgContext
{
    public static Parameter RequireParameter(Element element, string name)
    {
        return _FindParameter(element, name)
            ?? throw new InvalidOperationException($"У элемента {element.Id} «{element.Name}» отсутствует параметр «{name}».");
    }

    public static Parameter RequireParameter(Element element, BuiltInParameter parameter)
    {
        return _FindParameter(element, parameter)
            ?? throw new InvalidOperationException($"У элемента {element.Id} «{element.Name}» отсутствует параметр {parameter}.");
    }

    /// <summary>
    /// Find a parameter in the instance or element type by name.
    /// </summary>
    [JetBrains.Annotations.Pure]
    public static Parameter? _FindParameter(Element element, string parameter)
    {
        var instanceParameter = element.LookupParameter(parameter);
        if (instanceParameter is not null)
        {
            return instanceParameter;
        }

        var elementTypeId = element.GetTypeId();
        if (elementTypeId == ElementId.InvalidElementId)
        {
            return null;
        }

        var elementType = element.Document.GetElement(elementTypeId);
        return elementType?.LookupParameter(parameter);
    }

    /// <summary>
    /// Find a parameter in the instance or element type by built-in identifier.
    /// </summary>
    [JetBrains.Annotations.Pure]
    public static Parameter? _FindParameter(Element element, BuiltInParameter parameter)
    {
        var instanceParameter = element.get_Parameter(parameter);
        if (instanceParameter is not null)
        {
            return instanceParameter;
        }

        var elementTypeId = element.GetTypeId();
        if (elementTypeId == ElementId.InvalidElementId)
        {
            return null;
        }

        var elementType = element.Document.GetElement(elementTypeId);
        return elementType?.get_Parameter(parameter);
    }
}
