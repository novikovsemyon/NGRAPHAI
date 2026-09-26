using System.IO;
using System.Text;

namespace NGraph.Core.ModelSchemes;

/// <summary>Текстовый редактор сохраняет комментарии и неизвестные ключи; UTF-16 совместим с INIManager старых команд.</summary>
public sealed class SchemeIniFile
{
    private byte[] _original;
    public string FilePath { get; }
    public string Text { get; private set; }
    private SchemeIniFile(string path, byte[] bytes) { FilePath = path; _original = bytes; Text = Decode(bytes); }
    public static SchemeIniFile Open(string path) => new(Path.GetFullPath(path), File.ReadAllBytes(path));
    public string Save(string text)
    {
        if (!File.ReadAllBytes(FilePath).SequenceEqual(_original))
            throw new IOException("INI изменён вне редактора. Откройте файл заново, чтобы сохранить чужие изменения.");
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var backup = FilePath + ".bak";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Replace(temp, FilePath, backup);
            _original = bytes; Text = text;
            return backup;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        try { return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset); }
        catch (DecoderFallbackException)
        {
#if !NETFRAMEWORK
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            return Encoding.GetEncoding(1251).GetString(bytes); // Старые русскоязычные INI без BOM.
        }
    }

    public IReadOnlyDictionary<string, string> ReadSection(string name)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var active = false; var found = false;
        using var reader = new StringReader(Text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                active = string.Equals(line.Substring(1, line.Length - 2).Trim(), name, StringComparison.OrdinalIgnoreCase);
                if (active && found) throw new InvalidOperationException($"Секция [{name}] повторяется в «{Path.GetFileName(FilePath)}».");
                found |= active; continue;
            }
            if (!active) continue;
            var equals = line.IndexOf('='); if (equals <= 0) continue;
            var key = line.Substring(0, equals).Trim(); var value = line.Substring(equals + 1).Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[value.Length - 1] == '"') || (value[0] == '\'' && value[value.Length - 1] == '\'')))
                value = value.Substring(1, value.Length - 2);
            if (result.ContainsKey(key)) throw new InvalidOperationException($"Ключ {key} повторяется в секции [{name}].");
            result.Add(key, value);
        }
        if (!found) throw new InvalidOperationException($"В «{Path.GetFileName(FilePath)}» нет секции [{name}].");
        return result;
    }
}
