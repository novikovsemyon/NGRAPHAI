using Installer;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;
using Assembly = System.Reflection.Assembly;

const string outputName = "NGraph";
const string projectName = "NGraph";

var project = new Project
{
    OutDir = "output",
    Name = projectName,
    Platform = Platform.x64,
    Language = "ru-RU",
    Codepage = "1251",
    UI = WUI.WixUI_FeatureTree,
    MajorUpgrade = MajorUpgrade.Default,
    GUID = new Guid("BCCD7741-2EE9-4D60-A4C4-4F1762E382C9"),
    BannerImage = @"install\Resources\Icons\BannerImage.png",
    BackgroundImage = @"install\Resources\Icons\BackgroundImage.png",
    Version = Assembly.GetExecutingAssembly().GetName().Version.ClearRevision(),
    ControlPanelInfo =
    {
        Manufacturer = Environment.UserName,
        ProductIcon = @"install\Resources\Icons\ShellIcon.ico"
    }
};

var wixEntities = Generator.GenerateWixEntities(args);
project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.CustomizeDlg);

BuildSingleUserMsi();
BuildMultiUserUserMsi();

void BuildSingleUserMsi()
{
    project.Scope = InstallScope.perUser;
    project.OutFileName = $"{outputName}-{project.Version}-SingleUser";
    project.Dirs =
    [
        new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\", wixEntities),
        Generator.SettingsDirectory()
    ];
    BuildAndVerify();
}

void BuildMultiUserUserMsi()
{
    project.Scope = InstallScope.perMachine;
    project.OutFileName = $"{outputName}-{project.Version}-MultiUser";
    project.Dirs =
    [
        new InstallDir(@"%CommonAppDataFolder%\Autodesk\Revit\Addins\", wixEntities)
    ];
    BuildAndVerify();
}
// WixSharp can return without an exception after a native compiler error. Never report a missing MSI as success.
void BuildAndVerify()
{
    var file = project.BuildMsi();
    if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(file))
        throw new InvalidOperationException("MSI was not created. Review the WiX compiler output.");
}
