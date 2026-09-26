namespace NGraph.Core.CableRouting;

/// <summary>Граф физических портов. Вес — длина оси воздуховода в метрах, без округления.</summary>
public sealed class CableRouteGraph
{
    private readonly Dictionary<string, List<Arc>> _arcs = new(StringComparer.Ordinal);
    private readonly Dictionary<long, HashSet<string>> _terminals = new();
    private readonly Dictionary<string, Search> _searches = new(StringComparer.Ordinal);

    public void AddPort(string port, long ownerId)
    {
        if (!_arcs.ContainsKey(port)) _arcs.Add(port, new List<Arc>());
        if (!_terminals.TryGetValue(ownerId, out var ports)) _terminals.Add(ownerId, ports = new HashSet<string>(StringComparer.Ordinal));
        ports.Add(port);
        _searches.Clear();
    }

    public void Connect(string a, string b, double meters, long ductId = 0, string system = "")
    {
        if (double.IsNaN(meters) || double.IsInfinity(meters) || meters < 0)
            throw new ArgumentOutOfRangeException(nameof(meters));
        if (!_arcs.ContainsKey(a) || !_arcs.ContainsKey(b)) throw new ArgumentException("Неизвестный порт графа.");
        _arcs[a].Add(new Arc(b, meters, ductId, system));
        _arcs[b].Add(new Arc(a, meters, ductId, system));
        _searches.Clear();
    }

    public CablePath Find(long begin, long end)
    {
        if (begin == end) throw new InvalidOperationException("Начало и конец ссылаются на один элемент модели.");
        if (!_terminals.TryGetValue(begin, out var starts)) throw new InvalidOperationException($"Элемент {begin} не подключён к системе воздуховодов.");
        if (!_terminals.TryGetValue(end, out var ends)) throw new InvalidOperationException($"Элемент {end} не подключён к системе воздуховодов.");
        // Один запуск Дейкстры обслуживает все ответвления звезды от одного начала.
        var key = begin.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!_searches.TryGetValue(key, out var search)) _searches.Add(key, search = Dijkstra(starts));
        var target = ends.Where(search.Distance.ContainsKey).OrderBy(p => search.Distance[p]).ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
        if (target == null) throw new InvalidOperationException("Нет физического пути: проверьте разрывы, соединители и принадлежность к системе воздуховодов.");
        var ducts = new List<long>(); var systems = new HashSet<string>(StringComparer.Ordinal);
        var cursor = target;
        while (search.Previous.TryGetValue(cursor, out var step))
        {
            if (step.Edge.DuctId != 0) ducts.Add(step.Edge.DuctId);
            if (step.Edge.System.Length > 0) systems.Add(step.Edge.System);
            cursor = step.From;
        }
        ducts.Reverse();
        if (ducts.Count == 0) throw new InvalidOperationException("Путь не содержит воздуховодов.");
        return new CablePath(search.Distance[target], ducts.Distinct().ToList(), systems.OrderBy(s => s).ToList());
    }

    private Search Dijkstra(IEnumerable<string> starts)
    {
        var result = new Search();
        // SortedSet доступен и в net48; последовательный номер различает равные приоритеты.
        var queue = new SortedSet<(double Distance, long Sequence, string Port)>(); long sequence = 0;
        foreach (var port in starts.OrderBy(p => p, StringComparer.Ordinal))
        { result.Distance[port] = 0; queue.Add((0, sequence++, port)); }
        while (queue.Count > 0)
        {
            var current = queue.Min; queue.Remove(current);
            if (current.Distance > result.Distance[current.Port]) continue;
            foreach (var arc in _arcs[current.Port].OrderBy(a => a.To, StringComparer.Ordinal))
            {
                var next = current.Distance + arc.Meters;
                if (result.Distance.TryGetValue(arc.To, out var old) && next >= old) continue;
                result.Distance[arc.To] = next;
                result.Previous[arc.To] = new Step(current.Port, arc);
                queue.Add((next, sequence++, arc.To));
            }
        }
        return result;
    }

    private sealed class Search
    {
        public Dictionary<string, double> Distance { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Step> Previous { get; } = new(StringComparer.Ordinal);
    }
    private sealed class Arc
    {
        public string To { get; } public double Meters { get; } public long DuctId { get; } public string System { get; }
        public Arc(string to, double meters, long ductId, string system) { To = to; Meters = meters; DuctId = ductId; System = system; }
    }
    private sealed class Step
    {
        public string From { get; } public Arc Edge { get; }
        public Step(string from, Arc edge) { From = from; Edge = edge; }
    }
}

public sealed class CablePath
{
    public double Meters { get; } public IReadOnlyList<long> DuctIds { get; } public IReadOnlyList<string> Systems { get; }
    public CablePath(double meters, IReadOnlyList<long> ducts, IReadOnlyList<string> systems)
    { Meters = meters; DuctIds = ducts; Systems = systems; }
}
