using System.IO;

namespace NGraph.Core.ModelSchemes;

/// <summary>Читает соответствия один раз на комбинацию «группирование / позиция» до изменения RVT.</summary>
public sealed class SchemeIniCatalog
{
    private readonly Dictionary<string, List<string>> _files;
    private readonly Dictionary<string, SchemeIniFile> _documents = new(StringComparer.OrdinalIgnoreCase);
    public SchemeIniCatalog(string directory)
    {
        if (!Directory.Exists(directory)) throw new InvalidOperationException("Не найдена папка INI: " + directory);
        _files = Directory.GetFiles(directory, "*.ini", SearchOption.AllDirectories)
            .GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }
    public string FindFile(string group)
    {
        if (!_files.TryGetValue(group, out var files)) throw new InvalidOperationException($"Не найден INI «{group}.ini».");
        if (files.Count != 1) throw new InvalidOperationException($"В папке настроек найдено несколько файлов «{group}.ini». Оставьте однозначное соответствие.");
        return files[0];
    }
    public IReadOnlyDictionary<string, string> ReadSection(string group, string position)
    {
        if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(position))
            throw new InvalidOperationException("Для выбора УГО заполните ADSK_Группирование и ADSK_Позиция оборудования.");
        var file = FindFile(group);
        if (!_documents.TryGetValue(file, out var document)) _documents.Add(file, document = SchemeIniFile.Open(file));
        return document.ReadSection(position);
    }
    public string Resolve(string group, string position)
    {
        var section = ReadSection(group, position);
        var symbol = section.TryGetValue("Symbol", out var value) ? value.Trim() : string.Empty;
        if (symbol.Length == 0) throw new InvalidOperationException($"В «{group}.ini», секция [{position}], не задан Symbol.");
        return symbol;
    }
}
