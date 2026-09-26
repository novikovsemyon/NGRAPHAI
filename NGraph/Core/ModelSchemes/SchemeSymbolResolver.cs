namespace NGraph.Core.ModelSchemes;

/// <summary>Одно правило выбора загруженного УГО для таблицы, изображения и окончательной схемы.</summary>
internal sealed class SchemeSymbolResolver
{
    private readonly Dictionary<string, List<FamilySymbol>> _available;
    public SchemeSymbolResolver(Document document)
    {
        _available = new FilteredElementCollector(document).OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_DetailComponents).Cast<FamilySymbol>()
            .GroupBy(s => s.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
    }
    public FamilySymbol Resolve(string name)
    {
        if (!_available.TryGetValue(name, out var matches)) throw new InvalidOperationException($"Не загружен типоразмер элемента узла «{name}».");
        if (matches.Count != 1) throw new InvalidOperationException($"Имя УГО «{name}» встречается в нескольких семействах. Задайте уникальное имя типоразмера.");
        return matches[0];
    }
}
