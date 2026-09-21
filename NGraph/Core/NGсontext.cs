using System.Diagnostics.Contracts;

namespace NGraph.Core;

public static class NgContext
{
    #if REVIT2019_OR_GREATER
    /// <summary>
    ///     Find a parameter in the instance or symbol by identifier
    /// </summary>
    /// <param name="element">The element</param>
    /// <param name="parameter">The name of the parameter to be found</param>
    [JetBrains.Annotations.Pure]
    public static Parameter? _FindParameter(Element element, string parameter)
    {
        var instanceParameter = element.LookupParameter(parameter);
        if (instanceParameter is not null) return instanceParameter;

        var elementTypeId = element.GetTypeId();
        if (elementTypeId == ElementId.InvalidElementId) return null;

        var elementType = element.Document.GetElement(elementTypeId);
        return elementType.LookupParameter(parameter);
    }
    public static Parameter? _FindParameter(Element element, BuiltInParameter parameter)
    {
        var instanceParameter = element.get_Parameter(parameter);
        if (instanceParameter is not null) return instanceParameter;

        var elementTypeId = element.GetTypeId();
        if (elementTypeId == ElementId.InvalidElementId) return null;

        var elementType = element.Document.GetElement(elementTypeId);
        return elementType.get_Parameter(parameter);
    }
    #endif
            
    
}