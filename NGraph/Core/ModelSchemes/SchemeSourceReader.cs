using System.Globalization;

namespace NGraph.Core.ModelSchemes;

/// <summary>Снимок для новой команды по параметрам. Пространства не читаются; уровень нужен только при выборе источника «Уровень модели».</summary>
internal sealed class SchemeSourceReader
{
    public IReadOnlyList<SchemeSourceElement> Elements { get; }
    public IReadOnlyList<SchemeParameterChoice> Parameters { get; }

    public SchemeSourceReader(Document document)
    {
        var metadata = new Dictionary<string, Tuple<string, bool>>();
        var typeValues = new Dictionary<ElementId, Dictionary<string, string>>();
        var elements = new List<SchemeSourceElement>();
        var equipment = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_ElectricalEquipment)
            .WhereElementIsNotElementType().OfType<FamilyInstance>()
            .Where(fi => ReadNamed(fi, "ADSK_Группирование").Contains("АК_")).ToList();
        foreach (var fi in equipment)
        {
            var values = ReadParameters(fi, false);
            if (!typeValues.TryGetValue(fi.Symbol.Id, out var type))
                typeValues.Add(fi.Symbol.Id, type = ReadParameters(fi.Symbol, true));
            foreach (var pair in type) values[pair.Key] = pair.Value;
            elements.Add(new SchemeSourceElement(fi.Id.ToString(), fi.Name, ReadNamed(fi, "ADSK_Группирование"),
                ReadNamed(fi, "ADSK_Позиция"), ReadNamed(fi, "Имя панели"), ReadNamed(fi, "Марка"),
                ReadNamed(fi, "ADSK_Наименование краткое"), values,
                () => ReadLevel(fi, document)));
        }
        Elements = elements;
        var duplicateNames = new HashSet<Tuple<string, bool>>(metadata.Values.GroupBy(v => v)
            .Where(g => g.Count() > 1).Select(g => g.Key));
        Parameters = metadata.OrderBy(p => p.Value.Item1, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Value.Item2).ThenBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new SchemeParameterChoice(p.Key, p.Value.Item1, p.Value.Item2,
                elements.Count(e => e.Value(p.Key).Length > 0), elements.Count,
                duplicateNames.Contains(p.Value) ? p.Key : null)).ToList();

        Dictionary<string, string> ReadParameters(Element element, bool isType)
        {
            var result = new Dictionary<string, string>();
            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter.StorageType == StorageType.None) continue;
                var key = (isType ? "type:" : "instance:") + parameter.Id;
                metadata[key] = Tuple.Create(parameter.Definition.Name, isType);
                result[key] = ReadValue(parameter, document);
            }
            return result;
        }
    }

    private static string ReadNamed(Element element, string name) => ReadValue(NgContext._FindParameter(element, name), element.Document);
    private static string ReadValue(Parameter? parameter, Document document)
    {
        if (parameter == null || !parameter.HasValue) return string.Empty;
        switch (parameter.StorageType)
        {
            case StorageType.String: return (parameter.AsString() ?? string.Empty).Trim();
            case StorageType.Integer: return (parameter.AsValueString() ?? parameter.AsInteger().ToString(CultureInfo.InvariantCulture)).Trim();
            case StorageType.Double: return (parameter.AsValueString() ?? parameter.AsDouble().ToString("G17", CultureInfo.InvariantCulture)).Trim();
            case StorageType.ElementId:
                var id = parameter.AsElementId();
                if (id == ElementId.InvalidElementId) return string.Empty;
                return (document.GetElement(id)?.Name ?? parameter.AsValueString() ?? id.ToString()).Trim();
            default: return string.Empty;
        }
    }

    private static SchemeLevel? ReadLevel(FamilyInstance fi, Document document)
    {
        var level = document.GetElement(fi.LevelId) as Level;
        // У оборудования на грани LevelId может отсутствовать, но опорный уровень задан параметром Revit.
        foreach (var parameterId in new[] { BuiltInParameter.FAMILY_LEVEL_PARAM, BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM, BuiltInParameter.SCHEDULE_LEVEL_PARAM })
        {
            if (level != null) break;
            var parameter = fi.get_Parameter(parameterId);
            if (parameter?.StorageType == StorageType.ElementId) level = document.GetElement(parameter.AsElementId()) as Level;
        }
        return level == null ? null : new SchemeLevel(level.Id.ToString(), level.Name, level.Elevation * 304.8);
    }
}
