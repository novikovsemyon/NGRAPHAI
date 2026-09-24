using System.Globalization;
using System.IO;
using HOVS.Model;

namespace HOVS.Plugin;

public sealed class RevisionSnapshot
{
    public string Name { get; }
    public HovsModel Model { get; }
    public RevisionSnapshot(string name, HovsModel model) { Name = name; Model = model; }
}

public sealed class RevisionCompareRow
{
    public string Designation { get; set; } = "";
    public string Status { get; set; } = "";
    public string Details { get; set; } = "";
    public Dictionary<string, string> Cells { get; set; } = new();
}

/// <summary>Сравнивает сохранённые снимки, не перечитывая XLSX и не применяя новое обучение к истории.</summary>
public static class RevisionComparison
{
    public static IReadOnlyList<RevisionCompareRow> Compare(IReadOnlyList<RevisionSnapshot> snapshots)
    {
        if (snapshots.Count < 2) throw new ArgumentException("Выберите хотя бы две ревизии.");
        var maps = snapshots.Select(s =>
        {
            var groups = s.Model.Equipment.GroupBy(Key, StringComparer.OrdinalIgnoreCase).ToList();
            var duplicate = groups.FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) throw new InvalidDataException("В ревизии «" + s.Name + "» повторяется обозначение «" + duplicate.Key + "». Сравнение неоднозначно.");
            return groups.ToDictionary(g => g.Key, g => Fields(g.First(), s.Model), StringComparer.OrdinalIgnoreCase);
        }).ToList();
        var rows = new List<RevisionCompareRow>();
        foreach (var key in maps.SelectMany(m => m.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var versions = maps.Select(m => m.TryGetValue(key, out var fields) ? fields : null).ToList();
            var first = versions[0]; var last = versions[versions.Count - 1];
            var changes = new List<string>();
            for (int i = 1; i < versions.Count; i++)
            {
                var before = versions[i - 1]; var after = versions[i];
                string prefix = versions.Count > 2 ? snapshots[i - 1].Name + " → " + snapshots[i].Name + ": " : "";
                if (before == null && after != null) changes.Add(prefix + "Установка добавлена");
                else if (before != null && after == null) changes.Add(prefix + "Установка удалена");
                else if (before != null && after != null)
                    foreach (var field in before.Keys.Union(after.Keys))
                    {
                        before.TryGetValue(field, out var oldValue); after.TryGetValue(field, out var newValue);
                        if (Normalize(oldValue, field) != Normalize(newValue, field))
                            changes.Add(prefix + field + ": " + Display(oldValue) + " → " + Display(newValue));
                    }
            }
            rows.Add(new RevisionCompareRow
            {
                Designation = key,
                Status = first == null && last != null ? "Добавлена" : first != null && last == null ? "Удалена" : changes.Count > 0 ? "Изменена" : "Без изменений",
                Details = changes.Count == 0 ? "Без содержательных изменений." : string.Join(";\n", changes),
                Cells = versions.Select((fields, i) => new { Key = "R" + i, Value = fields == null ? "—" : string.Join(";\n", fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).Select(f => f.Key + ": " + f.Value)) })
                    .ToDictionary(x => x.Key, x => x.Value)
            });
        }
        return rows.OrderBy(r => Array.IndexOf(new[] { "Добавлена", "Удалена", "Изменена", "Без изменений" }, r.Status))
            .ThenBy(r => r.Designation, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Key(Equipment e) => string.IsNullOrWhiteSpace(e.Name) ? e.Id.Trim() : e.Name.Trim();
    private static string Attribute(Equipment e, string key) => e.Attributes.TryGetValue(key, out var value) ? value : "";
    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value!.Trim();
    private static string Normalize(string? value, string field)
    {
        var text = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (field == "Расход L, м³/ч" && decimal.TryParse(text.Replace(" ", "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return number.ToString("G29", CultureInfo.InvariantCulture);
        return text.ToUpperInvariant();
    }
    private static Dictionary<string, string> Fields(Equipment e, HovsModel model)
    {
        var fields = new Dictionary<string, string>
        {
            ["Тип"] = e.Attributes.TryGetValue(ProjectDataOverrides.Prefix + "InstallationType", out var type) ? type : e.Type,
            ["Расход L, м³/ч"] = Attribute(e, "__Installation.Airflow"),
            ["Помещение"] = e.Room,
            ["Состав"] = Components(e, model),
            ["Связи"] = Relations(e, model),
            ["Примечание"] = Attribute(e, NotesEquipmentParser.RawNotesKey),
            ["Резерв"] = Attribute(e, NotesEquipmentParser.ReserveSummaryKey),
            ["Доп. оборудование"] = Attribute(e, NotesEquipmentParser.AdditionalEquipmentKey),
            ["В обработке"] = Attribute(e, ProjectDataOverrides.Prefix + "Selected") == "0" ? "Нет" : "Да"
        };
        // Сохранённые ручные правки — часть ревизии. Не заменяем их текущими прогнозами анализатора.
        var labels = new Dictionary<string, string> {
            ["Recuperation"] = "Рекуперация", ["RecuperatorType"] = "Рекуператор", ["FilterCount"] = "Фильтров", ["FilterType"] = "Фильтр",
            ["Recirculation"] = "Рециркуляция", ["Heating"] = "Нагрев", ["HeatingType"] = "Тип нагрева", ["HeatingCount"] = "Нагревателей",
            ["Cooling"] = "Охлаждение", ["CoolingType"] = "Тип охлаждения", ["CoolingCount"] = "Охладителей", ["Humidification"] = "Увлажнение",
            ["HumidifierType"] = "Увлажнитель", ["HeatExchangers"] = "Теплообменники", ["RecoveryPartner"] = "Парная установка"
        };
        foreach (var label in labels) fields[label.Value] = Attribute(e, ProjectDataOverrides.Prefix + label.Key);
        return fields;
    }
    private static string Components(Equipment e, HovsModel model) => string.Join(", ", model.Components
        .Where(c => string.Equals(c.EquipmentId, e.Id, StringComparison.OrdinalIgnoreCase))
        .GroupBy(c => (c.ComponentType.Trim() + (string.IsNullOrWhiteSpace(c.Value) ? "" : " (" + c.Value.Trim() + ")")), StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.Key + " ×" + g.Sum(c => c.Quantity ?? 1)));
    private static string Relations(Equipment e, HovsModel model) => string.Join(", ", model.Relations
        .Where(r => string.Equals(r.SourceId, e.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(r.TargetId, e.Id, StringComparison.OrdinalIgnoreCase))
        .Select(r => {
            var other = string.Equals(r.SourceId, e.Id, StringComparison.OrdinalIgnoreCase) ? r.TargetId : r.SourceId;
            var target = model.Equipment.FirstOrDefault(x => string.Equals(x.Id, other, StringComparison.OrdinalIgnoreCase));
            return (target == null ? other : Key(target)) + " (" + RelationName(r.Kind) + ")";
        }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    public static string RelationName(RelationKind kind) => kind switch
    {
        RelationKind.GlycolicHeatRecovery => "гликолевый рекуператор", RelationKind.PlateHeatRecovery => "пластинчатый рекуператор",
        RelationKind.RotaryHeatRecovery => "роторный рекуператор", RelationKind.Recirculation => "рециркуляция", RelationKind.SameRoom => "одно помещение", _ => "связь"
    };
}
