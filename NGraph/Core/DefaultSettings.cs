using System.IO;
using System.Reflection;

namespace NGraph.Core;

/// <summary>Установщик поставляет шаблоны; каждый пользователь получает собственные изменяемые INI.</summary>
public static class DefaultSettings
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NGraph", "Settings");
    public static string LegacyDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "REVIT", "BETA-BIM", "Настройки");
    public static bool IsLegacyPath(string path) => string.Equals(path.TrimEnd('\\', '/'), LegacyDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
        || string.Equals(path.TrimEnd('\\', '/'), @"C:\Users\user\Documents\REVIT\BETA-BIM\Настройки", StringComparison.OrdinalIgnoreCase);

    public static void EnsureInstalled()
    {
        var templates = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Defaults", "Settings");
        Seed(templates, DirectoryPath, LegacyDirectory);
    }

    internal static void Seed(string templates, string destination, string? legacy = null)
    {
        if (!Directory.Exists(templates)) throw new DirectoryNotFoundException("Не найдены штатные INI. Переустановите NGraph: " + templates);
        Directory.CreateDirectory(destination);
        var marker = Path.Combine(destination, ".legacy-imported");
        if (legacy != null && Directory.Exists(legacy) && !File.Exists(marker))
        {
            foreach (var file in Directory.GetFiles(legacy, "*.ini", SearchOption.AllDirectories))
            {
                var relative = file.Substring(legacy.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1);
                var target = Path.Combine(destination, relative);
                var template = Path.Combine(templates, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // При первой миграции MSI уже мог разместить шаблон. Заменяем только неизменённый шаблон.
                if (!File.Exists(target) || (File.Exists(template) && File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(template))))
                    File.Copy(file, target, true);
            }
            File.WriteAllText(marker, "Legacy INI imported");
        }
        foreach (var file in Directory.GetFiles(templates, "*.ini"))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            try { File.Copy(file, target, false); }
            catch (IOException) when (File.Exists(target)) { /* Пользовательский файл сохраняется, включая одновременный запуск двух Revit. */ }
        }
    }
}
