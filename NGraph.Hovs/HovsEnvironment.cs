using System.IO;
namespace HOVS.Plugin;
/// <summary>Вся база объектов, профили и обучение лежат в одной переносимой папке вне RVT.</summary>
public static class HovsEnvironment
{
    public static string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NGraph", "Hovs");
}
