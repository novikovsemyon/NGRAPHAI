using HOVS.Model;
namespace HOVS.Plugin;
/// <summary>Исправления проекта отделены от обучающих примеров: сохранение не запускает обучение.</summary>
public static class ProjectDataOverrides
{
    public const string Prefix = "__Project.User.";
    public static bool ApplyToFeatures(Equipment equipment, InstallationFeatures result)
    {
        bool applied = false;
        foreach (var property in typeof(InstallationFeatures).GetProperties())
            if (equipment.Attributes.TryGetValue(Prefix + property.Name, out var value))
            { property.SetValue(result, value); applied = true; }
        return applied;
    }
    public static void Save(Equipment equipment, InstallationFeatures features, bool selected)
    {
        foreach (var property in typeof(InstallationFeatures).GetProperties())
            equipment.Attributes[Prefix + property.Name] = (string?)property.GetValue(features) ?? "";
        equipment.Attributes[Prefix + "Selected"] = selected ? "1" : "0";
    }
}
