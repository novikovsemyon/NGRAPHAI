using System.IO;
using System.Xml.Linq;

namespace NGraph.Core.CableRouting;

/// <summary>Выбранный параметр хранится отдельно для каждого чертёжного вида, вне файла RVT.</summary>
public static class CableRoutingPreferences
{
    public static string FilePath => Path.Combine(Path.GetDirectoryName(UserSettings.FilePath)!, "duct-cable-views.xml");
    public static string Read(string filePath, string viewKey)
    {
        if (!File.Exists(filePath)) return "";
        var root = XDocument.Load(filePath).Root;
        if (root?.Name != "DuctCableViews") throw new InvalidDataException("Неверный формат настроек маршрутов кабеля.");
        return (string?)root.Elements("View").FirstOrDefault(e => (string?)e.Attribute("key") == viewKey)?.Attribute("parameter") ?? "";
    }

    public static void Save(string filePath, string viewKey, string parameter)
    {
        var document = File.Exists(filePath) ? XDocument.Load(filePath) : new XDocument(new XElement("DuctCableViews"));
        var root = document.Root;
        if (root?.Name != "DuctCableViews") throw new InvalidDataException("Неверный формат настроек маршрутов кабеля.");
        root.Elements("View").Where(e => (string?)e.Attribute("key") == viewKey).Remove();
        root.Add(new XElement("View", new XAttribute("key", viewKey), new XAttribute("parameter", parameter)));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            document.Save(temp);
            if (File.Exists(filePath)) File.Replace(temp, filePath, null); else File.Move(temp, filePath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
