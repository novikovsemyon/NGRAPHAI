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
        IniDirectory = directory };
    settings.SaveTo(file);
    var loaded = UserSettings.LoadFrom(file, out warning);
    Check(warning == "" && loaded.ElementsView == settings.ElementsView && loaded.FsaView == settings.FsaView
        && loaded.IniDirectory == directory, "Unicode/XML round trip");
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
    var templates = Path.Combine(directory, "templates"); var user = Path.Combine(directory, "user"); var legacy = Path.Combine(directory, "legacy");
    Directory.CreateDirectory(templates); Directory.CreateDirectory(legacy);
    File.WriteAllText(Path.Combine(templates, "АК_Газ.ini"), "default", System.Text.Encoding.Unicode);
    File.WriteAllText(Path.Combine(templates, "settings.ini"), "rounding", System.Text.Encoding.Unicode);
    File.WriteAllText(Path.Combine(legacy, "АК_Газ.ini"), "custom legacy", System.Text.Encoding.Unicode);
    DefaultSettings.Seed(templates, user); // Имитация уже установленного MSI шаблона.
    DefaultSettings.Seed(templates, user, legacy);
    Check(File.ReadAllText(Path.Combine(user, "АК_Газ.ini")) == "custom legacy", "Legacy customizations migrate over untouched MSI template");
    File.WriteAllText(Path.Combine(user, "settings.ini"), "custom current");
    File.WriteAllText(Path.Combine(legacy, "АК_Газ.ini"), "changed old folder");
    DefaultSettings.Seed(templates, user, legacy);
    Check(File.ReadAllText(Path.Combine(user, "settings.ini")) == "custom current", "Upgrade must preserve user INI");
    Check(File.ReadAllText(Path.Combine(user, "АК_Газ.ini")) == "custom legacy", "Migration runs only once");
    File.WriteAllText(file, "<NGraphSettings><IniDirectory>" + System.Security.SecurityElement.Escape(DefaultSettings.LegacyDirectory) + "</IniDirectory></NGraphSettings>");
    Check(UserSettings.LoadFrom(file, out warning).IniDirectory == DefaultSettings.DirectoryPath, "Legacy default path is replaced");
    Check(ProgramVersion.Format("2.0.38-beta.1+abcdef", null) == "2.0.38-beta.1", "Display keeps prerelease and removes commit hash");
    Check(ProgramVersion.Format(null, new Version(2, 0, 38, 0)) == "2.0.38", "Assembly version fallback");
    if (OperatingSystem.IsWindows())
    {
        var cableFile = Path.Combine(user, "settings.ini");
        File.WriteAllText(cableFile, "[CreateConnectionsLine]\nОкруглениеКабеля = 7\n", System.Text.Encoding.Unicode);
        Check(CableSettings.RoundingMillimeters(user) == 7000, "Unicode cable setting uses meters");
        File.WriteAllText(cableFile, "[CreateConnectionsLine]\nОкруглениеКабеля = 0\n", System.Text.Encoding.Unicode);
        bool rejected = false;
        try { CableSettings.RoundingMillimeters(user); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid cable rounding rejected before model changes");
    }
    Console.WriteLine("PASS: defaults, XML round trip, replacement, cleanup and corrupt-file recovery, INI migration/preservation, version formatting.");
}
finally
{
    Directory.Delete(directory, true);
}
