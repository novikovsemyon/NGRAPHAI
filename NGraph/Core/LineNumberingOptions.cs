using System.Globalization;

namespace NGraph.Core;

/// <summary>Формат номера не зависит от Revit; диапазон проверяется для всей последовательности.</summary>
public sealed class LineNumberingOptions
{
    public int Start { get; }
    public int Increment { get; }
    public string Prefix { get; }
    public string Postfix { get; }

    private LineNumberingOptions(int start, int increment, string prefix, string postfix)
    {
        Start = start;
        Increment = increment;
        Prefix = prefix;
        Postfix = postfix;
    }

    public string Format(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        var number = checked((int)(Start + (long)index * Increment));
        return Prefix + number.ToString(CultureInfo.InvariantCulture) + Postfix;
    }

    public static bool TryCreate(string start, string increment, string prefix, string postfix, int count,
        out LineNumberingOptions? options, out string error)
    {
        options = null;
        if (count <= 0) error = "Нет элементов для нумерации.";
        else if (!int.TryParse(start, NumberStyles.Integer, CultureInfo.InvariantCulture, out var first))
            error = "Начальное значение должно быть целым числом от −2147483648 до 2147483647.";
        else if (!int.TryParse(increment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var step) || step == 0)
            error = "Инкремент должен быть целым числом, отличным от нуля.";
        else
        {
            var last = first + (long)(count - 1) * step;
            if (last < int.MinValue || last > int.MaxValue)
                error = "Последовательность выходит за диапазон целых чисел. Уменьшите начальное значение или шаг.";
            else
            {
                options = new LineNumberingOptions(first, step, prefix, postfix);
                error = string.Empty;
                return true;
            }
        }
        return false;
    }
}

/// <summary>Описание параметра для WPF без ссылки на объекты Revit API.</summary>
public sealed class LineNumberingParameter
{
    public string Key { get; }
    public string Name { get; }
    public string DisplayName { get; }
    public bool IsDefault { get; }

    public LineNumberingParameter(string key, string name, string displayName, bool isDefault)
    {
        Key = key;
        Name = name;
        DisplayName = displayName;
        IsDefault = isDefault;
    }
}
