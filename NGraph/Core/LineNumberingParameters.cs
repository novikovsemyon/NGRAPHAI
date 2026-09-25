namespace NGraph.Core;

/// <summary>Выбирает общие записываемые параметры экземпляров, не затрагивая типоразмеры.</summary>
internal static class LineNumberingParameters
{
    public static List<LineNumberingParameter> GetCommon(IReadOnlyList<Element> elements)
    {
        if (elements.Count == 0) return new List<LineNumberingParameter>();
        var parameters = WritableTextParameters(elements[0]).ToList();
        var commonIds = new HashSet<ElementId>(parameters.Select(p => p.Id));
        foreach (var element in elements.Skip(1))
            commonIds.IntersectWith(WritableTextParameters(element).Select(p => p.Id));

        var common = parameters.Where(p => commonIds.Contains(p.Id)).GroupBy(p => p.Id).Select(g => g.First()).ToList();
        var duplicateNames = new HashSet<string>(common.GroupBy(p => p.Definition.Name)
            .Where(g => g.Count() > 1).Select(g => g.Key));
        var panelNameId = new ElementId(BuiltInParameter.RBS_ELEC_PANEL_NAME);
        return common.OrderBy(p => p.Definition.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new LineNumberingParameter(p.Id.ToString(), p.Definition.Name,
                duplicateNames.Contains(p.Definition.Name) ? $"{p.Definition.Name} (ID {p.Id})" : p.Definition.Name,
                p.Id == panelNameId)).ToList();
    }

    public static Parameter RequireWritable(Element element, LineNumberingParameter choice)
    {
        // Идентификатор, а не LookupParameter(name), различает одноимённые определения.
        var parameter = WritableTextParameters(element).FirstOrDefault(p => p.Id.ToString() == choice.Key);
        return parameter ?? throw new InvalidOperationException(
            $"Параметр «{choice.Name}» элемента {element.Id} «{element.Name}» недоступен для записи.");
    }

    private static IEnumerable<Parameter> WritableTextParameters(Element element) =>
        element.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly && p.StorageType == StorageType.String);
}
