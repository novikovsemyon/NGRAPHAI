using System.IO;

namespace NGraph.Core.ModelSchemes;

/// <summary>Читает соответствия один раз на комбинацию «группирование / позиция» до изменения RVT.</summary>
public sealed class SchemeIniCatalog
{
    private readonly Dictionary<string, List<string>> _files;
    private readonly Dictionary<Tuple<string, string>, string> _symbols = new();
    public SchemeIniCatalog(string directory)
    {
        if (!Directory.Exists(directory)) throw new InvalidOperationException("Не найдена папка INI: " + directory);
        _files = Directory.GetFiles(directory, "*.ini", SearchOption.AllDirectories)
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }
    public string Resolve(string group, string position)
    {
        if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(position))
            throw new InvalidOperationException("Для выбора УГО заполните ADSK_Группирование и ADSK_Позиция оборудования.");
        var key = Tuple.Create(group, position);
        if (_symbols.TryGetValue(key, out var symbol)) return symbol;
        if (!_files.TryGetValue(group, out var files)) throw new InvalidOperationException($"Не найден INI «{group}.ini».");
        if (files.Count != 1) throw new InvalidOperationException($"В папке настроек найдено несколько файлов «{group}.ini». Оставьте однозначное соответствие.");
        symbol = new INIManager(files[0]).GetPrivateString(position, "Symbol").Trim();
        if (symbol.Length == 0) throw new InvalidOperationException($"В «{group}.ini», секция [{position}], не задан Symbol.");
        _symbols.Add(key, symbol);
        return symbol;
    }
}
