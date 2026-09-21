using System.IO;
using System.Xml.Linq;

namespace NGraph.Core;

/// <summary>Настройки пользователя хранятся вне RVT и применяются при следующем запуске команды.</summary>
public sealed class UserSettings
{
    public string ElementsView { get; set; } = "!_000_NGraph_БАЗА_ЭЛЕМЕНТОВ";
    public string FsaView { get; set; } = "!_000_NGraph_БАЗА ДАННЫХ_ФСА";
    public string EquipmentView { get; set; } = "!_000_NGraph_БАЗА_ОБОРУДОВАНИЯ";
    public string IniDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "REVIT", "BETA-BIM", "Настройки");

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NGraph", "settings.xml");

    public static UserSettings Load() => Load(out _);

    /// <summary>Отсутствие файла означает первый запуск; повреждение возвращается в интерфейс как предупреждение.</summary>
    public static UserSettings Load(out string warning)
    {
        warning = string.Empty;
        var defaults = new UserSettings();
        if (!File.Exists(FilePath)) return defaults;
        try
        {
            var root = XDocument.Load(FilePath).Root;
            if (root?.Name != "NGraphSettings") throw new InvalidDataException("Неверный формат настроек.");
            defaults.ElementsView = Read(root, nameof(ElementsView), defaults.ElementsView);
            defaults.FsaView = Read(root, nameof(FsaView), defaults.FsaView);
            defaults.EquipmentView = Read(root, nameof(EquipmentView), defaults.EquipmentView);
            defaults.IniDirectory = Read(root, nameof(IniDirectory), defaults.IniDirectory);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
        {
            warning = "Не удалось прочитать настройки. Загружены стандартные значения. " + ex.Message;
        }
        return defaults;
    }

    private static string Read(XElement root, string name, string fallback)
    {
        var value = (string?)root.Element(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>Сначала записываем временный файл: неудачная запись не повреждает предыдущие настройки.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            new XDocument(new XElement("NGraphSettings",
                new XElement(nameof(ElementsView), ElementsView), new XElement(nameof(FsaView), FsaView),
                new XElement(nameof(EquipmentView), EquipmentView), new XElement(nameof(IniDirectory), IniDirectory))).Save(temporary);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
