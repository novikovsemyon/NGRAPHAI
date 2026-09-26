using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NGraph.Core.CableRouting;

public abstract class CableNotify : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CableRouteRow : CableNotify
{
    private bool _included = true;
    public bool Included { get => _included; set { _included = value; Changed(); } }
    public CableConnection Connection { get; }
    public CablePath? Path { get; }
    public string Number => Connection.Cable.Number;
    public string Begin => Connection.Cable.Begin;
    public string End => Connection.Cable.End;
    public long Id => Connection.Cable.Id;
    public string Kind => Connection.IsStar ? "Звезда" : "Соединение";
    public string ModelIds => $"{Connection.BeginId} → {Connection.EndId}";
    public string RouteIds => Path == null ? "" : string.Join(" → ", Path.DuctIds);
    public string Systems => Path == null ? "" : string.Join("; ", Path.Systems);
    public string Length => Path == null ? "—" : Path.Meters.ToString("0.###", CultureInfo.CurrentCulture);
    public string PreviousLength { get; }
    private string _newLength = "—";
    public string NewLength { get => _newLength; set { _newLength = value; Changed(); } }
    public string Error { get; }
    public string Status => Error.Length == 0 ? "Готово" : Error;
    public bool Ready => Error.Length == 0 && Path != null;
    public CableRouteRow(CableConnection connection, CablePath? path, string previousLength, string error)
    { Connection = connection; Path = path; PreviousLength = previousLength; Error = error; }
}

public sealed class CableChoice
{
    public string Key { get; } public string Name { get; } public string DisplayName { get; }
    public CableChoice(string key, string name, string? displayName = null) { Key = key; Name = name; DisplayName = displayName ?? name; }
}

public sealed class CableDuctSnapshot
{
    public long Id { get; set; } public string SystemId { get; set; } = ""; public string System { get; set; } = "";
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
}

public sealed class CableDuctWrite
{
    public long Id { get; } public string System { get; } public string OldValue { get; } public string NewValue { get; }
    public string Error { get; } public bool HasRoute { get; }
    public string Status => Error.Length > 0 ? Error : OldValue == NewValue ? "Без изменений" : NewValue.Length == 0 ? "Очистить" : "Записать";
    public CableDuctWrite(CableDuctSnapshot duct, string oldValue, string newValue, bool hasRoute, string error)
    { Id = duct.Id; System = duct.System; OldValue = oldValue; NewValue = newValue; HasRoute = hasRoute; Error = error; }
}

public static class CableWritePlanner
{
    public static int RoundedMeters(double meters, int step)
    {
        if (step < 1 || meters < 0 || double.IsNaN(meters) || double.IsInfinity(meters)) throw new ArgumentOutOfRangeException(nameof(meters));
        var result = Math.Ceiling(meters / step - 1e-9) * step;
        if (result > int.MaxValue) throw new InvalidOperationException("Длина выходит за диапазон целых метров.");
        return checked((int)Math.Max(0, result));
    }

    public static IReadOnlyList<CableDuctWrite> Plan(IReadOnlyList<CableRouteRow> routes,
        IReadOnlyList<CableDuctSnapshot> ducts, string parameter, bool append, bool clearUnused)
    {
        var numbers = new Dictionary<long, SortedSet<string>>();
        foreach (var row in routes.Where(r => r.Included && r.Ready)) foreach (var id in row.Path!.DuctIds)
        {
            if (!numbers.TryGetValue(id, out var values)) numbers[id] = values = new SortedSet<string>(StringComparer.Ordinal);
            values.Add(row.Number);
        }
        var systems = new HashSet<string>(ducts.Where(d => numbers.ContainsKey(d.Id)).Select(d => d.SystemId), StringComparer.Ordinal);
        return ducts.Where(d => numbers.ContainsKey(d.Id) || clearUnused && systems.Contains(d.SystemId)).OrderBy(d => d.System).ThenBy(d => d.Id)
            .Select(d =>
            {
                var available = d.Values.TryGetValue(parameter, out var old);
                var values = numbers.TryGetValue(d.Id, out var n) ? n.ToList() : new List<string>();
                if (append && numbers.ContainsKey(d.Id)) values.InsertRange(0, Split(old ?? ""));
                var text = string.Join(Environment.NewLine, values.Distinct(StringComparer.Ordinal));
                return new CableDuctWrite(d, old ?? "", text, numbers.ContainsKey(d.Id),
                    available ? "" : "Параметр отсутствует или недоступен для записи");
            }).ToList();
    }

    private static IEnumerable<string> Split(string text) => text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim()).Where(s => s.Length > 0);
}
