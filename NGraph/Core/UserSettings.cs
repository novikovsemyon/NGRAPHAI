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

    public string RegionType { get; set; } = "BD_Готовое решение";

    public string RegionNameParameter { get; set; } = "ADSK_Наименование";

    public string RegionGroupParameter { get; set; } = "ADSK_Группирование";

    public string RegionCodeParameter { get; set; } = "ADSK_Примечание";

    public string EquipmentType { get; set; } = "Окружность 10 мм";

    public string SignalType { get; set; } = "FAS_точка";

    public string SignalWithoutTagType { get; set; } = "FAS_точка_без маркировки";

    public string HovsFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NGraph", "Hovs");

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NGraph", "settings.xml");

    public static UserSettings Load() => Load(out _);

    /// <summary>Отсутствие файла означает первый запуск; повреждение возвращается в интерфейс как предупреждение.</summary>
    public static UserSettings Load(out string warning) => LoadFrom(FilePath, out warning);

    // Отдельный путь позволяет проверять хранение на временных файлах, не затрагивая настройки пользователя.
    internal static UserSettings LoadFrom(string filePath, out string warning)
    {
        warning = string.Empty;
        var defaults = new UserSettings();
        if (!File.Exists(filePath)) return defaults;
        try
        {
            var root = XDocument.Load(filePath).Root;
            if (root?.Name != "NGraphSettings") throw new InvalidDataException("Неверный формат настроек.");
            defaults.RegionType = Read(root, nameof(RegionType), defaults.RegionType);
            defaults.RegionNameParameter = Read(root, nameof(RegionNameParameter), defaults.RegionNameParameter);
            defaults.RegionGroupParameter = Read(root, nameof(RegionGroupParameter), defaults.RegionGroupParameter);
            defaults.RegionCodeParameter = Read(root, nameof(RegionCodeParameter), defaults.RegionCodeParameter);
            defaults.EquipmentType = Read(root, nameof(EquipmentType), defaults.EquipmentType);
            defaults.SignalType = Read(root, nameof(SignalType), defaults.SignalType);
            defaults.SignalWithoutTagType = Read(root, nameof(SignalWithoutTagType), defaults.SignalWithoutTagType);
            defaults.HovsFolder = Read(root, nameof(HovsFolder), defaults.HovsFolder);
            defaults.ElementsView = Read(root, nameof(ElementsView), defaults.ElementsView);
            defaults.FsaView = Read(root, nameof(FsaView), defaults.FsaView);
            defaults.EquipmentView = Read(root, nameof(EquipmentView), defaults.EquipmentView);
            defaults.IniDirectory = Read(root, nameof(IniDirectory), defaults.IniDirectory);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
        {
            warning = "Не удалось прочитать настройки. Загружены стандартные значения. " + ex.Message;
        }
        return defaults;
    }

    private static string Read(XElement root, string name, string fallback)
    {
        var value = (string?)root.Element(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
    }

    /// <summary>Сначала записываем временный файл: неудачная запись не повреждает предыдущие настройки.</summary>
    public void Save() => SaveTo(FilePath);

    internal void SaveTo(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            new XDocument(new XElement("NGraphSettings",
                new XElement(nameof(RegionType), RegionType),
                new XElement(nameof(RegionNameParameter), RegionNameParameter),
                new XElement(nameof(RegionGroupParameter), RegionGroupParameter),
                new XElement(nameof(RegionCodeParameter), RegionCodeParameter),
                new XElement(nameof(EquipmentType), EquipmentType),
                new XElement(nameof(SignalType), SignalType),
                new XElement(nameof(SignalWithoutTagType), SignalWithoutTagType),
                new XElement(nameof(HovsFolder), HovsFolder),
                new XElement(nameof(ElementsView), ElementsView), new XElement(nameof(FsaView), FsaView),
                new XElement(nameof(EquipmentView), EquipmentView), new XElement(nameof(IniDirectory), IniDirectory))).Save(temporary);
            if (File.Exists(filePath)) File.Replace(temporary, filePath, null);
            else File.Move(temporary, filePath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
