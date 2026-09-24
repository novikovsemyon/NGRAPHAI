using System.IO;
namespace NGraph.Core;
public static class CableSettings
{
    /// <summary>INI задаёт шаг в метрах; расчёт кабельной команды ведётся в миллиметрах.</summary>
    public static int RoundingMillimeters(string directory)
    {
        var file = Path.Combine(directory, "settings.ini");
        if (!File.Exists(file)) return 5000;
        var value = new INIManager(file).GetPrivateString("CreateConnectionsLine", "ОкруглениеКабеля");
        if (string.IsNullOrWhiteSpace(value)) return 5000;
        if (!int.TryParse(value, out var meters) || meters <= 0 || meters > int.MaxValue / 1000)
            throw new InvalidDataException("В settings.ini параметр [CreateConnectionsLine] ОкруглениеКабеля должен быть положительным целым числом метров.");
        return meters * 1000;
    }
}
