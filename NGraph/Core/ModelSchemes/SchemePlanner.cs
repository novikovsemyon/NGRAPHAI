using System.Globalization;
using System.Text.RegularExpressions;

namespace NGraph.Core.ModelSchemes;

/// <summary>План новой команды по параметрам. Исходная CreateSxemaByModel этот класс не использует.</summary>
public static class SchemePlanner
{
    public static SchemePlan Build(IReadOnlyList<SchemeSourceElement> source, SchemeOptions options)
    {
        var plan = new SchemePlan();
        var filtered = source.Where(e => options.FilterParameterKey.Length == 0 || options.FilterValue == null
            || e.Value(options.FilterParameterKey) == options.FilterValue).ToList();
        plan.FilteredCount = filtered.Count;
        if (string.IsNullOrWhiteSpace(options.ViewName)) plan.Errors.Add("Введите название нового чертёжного вида.");
        if (options.GroupNameParameterKey.Length == 0) plan.Errors.Add("Выберите параметр названия группы вместо пространства.");
        if (options.LevelSource == SchemeLevelSource.Parameter && options.LevelParameterKey.Length == 0)
            plan.Errors.Add("Выберите параметр уровня.");
        if (filtered.Count == 0) plan.Errors.Add("По выбранному фильтру оборудование не найдено.");
        if (plan.Errors.Count > 0) return plan;

        foreach (var element in filtered)
        {
            var missing = new List<string>();
            var sectionKey = element.Value(options.SectionParameterKey);
            var sectionName = options.SectionParameterKey.Length == 0 ? "Оборудование"
                : sectionKey.Length > 0 ? sectionKey : "Без секции";
            if (options.SectionParameterKey.Length > 0 && sectionKey.Length == 0) missing.Add("секция");

            string levelKey, levelName;
            double? elevation = null;
            if (options.LevelSource == SchemeLevelSource.Parameter)
            {
                var value = element.Value(options.LevelParameterKey);
                levelKey = "parameter:" + value;
                levelName = value.Length > 0 ? value : "Без значения уровня";
                if (value.Length == 0) missing.Add("уровень из параметра");
            }
            else
            {
                var level = element.Level;
                levelKey = "model:" + (level?.Id ?? string.Empty);
                levelName = level?.Name ?? "Без уровня";
                elevation = level?.ElevationMillimeters;
                if (level == null) missing.Add("уровень модели");
            }

            // Ни Space, ни Room не нужны даже при отсутствии этих объектов в RVT.
            var groupName = element.Value(options.GroupNameParameterKey);
            var groupNumber = element.Value(options.GroupNumberParameterKey);
            if (groupName.Length == 0) missing.Add("название группы");
            if (options.GroupNumberParameterKey.Length > 0 && groupNumber.Length == 0) missing.Add("номер группы");
            var caption = groupName.Length > 0 ? groupName : "Без названия группы";
            if (options.GroupNumberParameterKey.Length > 0)
                caption += " · " + (groupNumber.Length > 0 ? groupNumber : "без номера");

            if (missing.Count > 0)
            {
                plan.Issues.Add(new SchemeIssue(element.Id, element.Name, "Не заполнено или отсутствует: " + string.Join(", ", missing) + "."));
                if (options.MissingValues == MissingSchemeValuePolicy.Exclude) { plan.SkippedCount++; continue; }
            }
            plan.Elements.Add(new SchemePlannedElement(element, sectionKey, sectionName, levelKey,
                levelName, elevation, groupName, groupNumber, caption));
        }
        if (options.MissingValues == MissingSchemeValuePolicy.Stop && plan.Issues.Count > 0)
            plan.Errors.Add("Заполните поля группировки или измените правило обработки пустых значений.");
        if (plan.Elements.Count == 0) plan.Errors.Add("После исключения элементов с пустыми полями нечего строить.");
        var order = SchemeValueComparer.Instance;
        plan.Groups.AddRange(plan.Elements.GroupBy(e => e.GroupKey)
            .Select(g => new SchemePreviewGroup(g.OrderBy(e => e.Source.Position, order)
                .ThenBy(e => e.Source.Mark, order).ThenBy(e => e.Id, order).ToList()))
            .OrderBy(g => g.SectionName, order)
            .ThenBy(g => g.First.ElevationMillimeters ?? double.MaxValue)
            .ThenBy(g => g.LevelName, order).ThenBy(g => g.First.LevelKey, StringComparer.Ordinal)
            .ThenBy(g => g.First.GroupNumber, order).ThenBy(g => g.First.GroupName, order));
        return plan;
    }
}

/// <summary>Числа и подписи вида «Этаж 2», «Этаж 10» сортируются в естественном порядке.</summary>
public sealed class SchemeValueComparer : IComparer<string>
{
    public static SchemeValueComparer Instance { get; } = new();
    public int Compare(string? x, string? y)
    {
        x ??= string.Empty; y ??= string.Empty;
        var xIsNumber = decimal.TryParse(x.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var a);
        var yIsNumber = decimal.TryParse(y.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var b);
        if (xIsNumber || yIsNumber)
        {
            if (!xIsNumber) return 1;
            if (!yIsNumber) return -1;
            var numeric = a.CompareTo(b);
            return numeric != 0 ? numeric : StringComparer.Ordinal.Compare(x, y);
        }
        var left = Regex.Split(x, "([0-9]+)"); var right = Regex.Split(y, "([0-9]+)");
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            int result;
            if (i % 2 == 1)
            {
                var l = left[i].TrimStart('0'); var r = right[i].TrimStart('0');
                result = l.Length.CompareTo(r.Length);
                if (result == 0) result = StringComparer.Ordinal.Compare(l, r);
            }
            else result = StringComparer.CurrentCultureIgnoreCase.Compare(left[i], right[i]);
            if (result != 0) return result;
        }
        var length = left.Length.CompareTo(right.Length);
        return length != 0 ? length : StringComparer.Ordinal.Compare(x, y);
    }
}
