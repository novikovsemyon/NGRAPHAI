using NGraph.Core;

// Проверяем реальный код хранения без Revit и без изменения пользовательского профиля.
var directory = Path.Combine(Path.GetTempPath(), "NGraph-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var file = Path.Combine(directory, "settings.xml");
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
try
{
    var defaults = UserSettings.LoadFrom(file, out var warning);
    Check(warning == "" && defaults.ElementsView == "!_000_NGraph_БАЗА_ЭЛЕМЕНТОВ", "First launch defaults");
    var settings = new UserSettings { ElementsView = "База & элементы", FsaView = "ФСА <1>",
        EquipmentView = "Оборудование", IniDirectory = directory };
    settings.SaveTo(file);
    var loaded = UserSettings.LoadFrom(file, out warning);
    Check(warning == "" && loaded.ElementsView == settings.ElementsView && loaded.FsaView == settings.FsaView
        && loaded.EquipmentView == settings.EquipmentView && loaded.IniDirectory == directory, "Unicode/XML round trip");
    settings.ElementsView = "Новая база";
    settings.RegionNameParameter = "Пользовательское имя";
    settings.SaveTo(file);
    Check(UserSettings.LoadFrom(file, out warning).ElementsView == "Новая база", "Atomic replacement");
    Check(UserSettings.LoadFrom(file, out warning).RegionNameParameter == "Пользовательское имя", "Parameter mapping persistence");
    Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "Temporary file cleanup");
    File.WriteAllText(file, "<NGraphSettings><ElementsView> </ElementsView></NGraphSettings>");
    Check(UserSettings.LoadFrom(file, out warning).ElementsView == defaults.ElementsView && warning == "", "Missing fields fall back");
    foreach (var invalid in new[] { "<broken", "<Other />" })
    {
        File.WriteAllText(file, invalid);
        Check(UserSettings.LoadFrom(file, out warning).ElementsView == defaults.ElementsView && warning.Length > 0,
            "Corruption warning and defaults");
        Check(File.ReadAllText(file) == invalid, "Load must not overwrite invalid input");
    }
    Console.WriteLine("PASS: defaults, XML round trip, replacement, cleanup and corrupt-file recovery.");
}
finally
{
    Directory.Delete(directory, true);
}
