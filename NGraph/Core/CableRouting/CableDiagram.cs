namespace NGraph.Core.CableRouting;

public readonly struct DiagramPoint
{
    public double X { get; } public double Y { get; }
    public DiagramPoint(double x, double y) { X = x; Y = y; }
    public double Distance(DiagramPoint p) => Math.Sqrt((X-p.X)*(X-p.X) + (Y-p.Y)*(Y-p.Y));
}

public sealed class DiagramEquipment
{
    public long Id { get; set; } public long ModelId { get; set; }
    public string Panel { get; set; } = "";
    public DiagramPoint Min { get; set; } public DiagramPoint Max { get; set; }
    public bool Contains(DiagramPoint p) => p.X >= Min.X-1 && p.X <= Max.X+1 && p.Y >= Min.Y-1 && p.Y <= Max.Y+1;
}

public sealed class DiagramLine
{
    public DiagramPoint A { get; set; } public DiagramPoint B { get; set; } public bool Pinned { get; set; }
    public bool Contains(DiagramPoint p)
    {
        var dx = B.X-A.X; var dy = B.Y-A.Y; var square = dx*dx+dy*dy;
        var t = square == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X-A.X)*dx+(p.Y-A.Y)*dy)/square));
        return p.Distance(new DiagramPoint(A.X+t*dx, A.Y+t*dy)) <= 1; // мм на чертёжном виде
    }
}

public sealed class DiagramCable
{
    public long Id { get; set; } public string Number { get; set; } = "";
    public string Begin { get; set; } = ""; public string End { get; set; } = "";
    public DiagramPoint Point { get; set; }
    public string Error { get; set; } = "";
}

public sealed class CableConnection
{
    public DiagramCable Cable { get; } public long BeginId { get; } public long EndId { get; }
    public bool IsStar { get; } public string Error { get; }
    public string BeginLabel { get; } public string EndLabel { get; }
    public CableConnection(DiagramCable cable, long beginId, long endId, bool star, string error = "",
        string? beginLabel = null, string? endLabel = null)
    {
        Cable = cable; BeginId = beginId; EndId = endId; IsStar = star; Error = error;
        BeginLabel = beginLabel ?? cable.Begin; EndLabel = endLabel ?? cable.End;
    }
}

/// <summary>Соединения читаются из существующих зелёных точек, без создания электрических цепей.</summary>
public static class CableDiagram
{
    public static IReadOnlyList<CableConnection> Resolve(IReadOnlyList<DiagramEquipment> equipment,
        IReadOnlyList<DiagramLine> lines, IReadOnlyList<DiagramCable> cables)
    {
        var components = Components(lines);
        var starParts = components.Where(c => c.Any(l => l.Pinned)).ToDictionary(c => c,
            c => (Buses: Components(c.Where(l => l.Pinned).ToList()), Branches: Components(c.Where(l => !l.Pinned).ToList())));
        var result = new List<CableConnection>();
        foreach (var cable in cables)
        {
            var star = false;
            try
            {
                if (cable.Error.Length > 0) throw new InvalidOperationException(cable.Error);
                if (string.IsNullOrWhiteSpace(cable.Number)) throw new InvalidOperationException("Не заполнен CJ_Номер кабеля.");
                var candidates = components.Where(c => c.Any(l => l.Contains(cable.Point))).ToList();
                if (candidates.Count != 1) throw new InvalidOperationException("Зелёная точка должна находиться на одной сети линий *NG*. Проверьте её положение.");
                var component = candidates[0];
                var local = equipment.Where(e => component.Any(l => e.Contains(l.A) || e.Contains(l.B))).ToList();
                star = starParts.TryGetValue(component, out var parts);
                DiagramEquipment begin, end;
                if (star) (begin, end) = ResolveStar(cable, parts.Buses, parts.Branches, local);
                else
                {
                    begin = ResolveEnd(cable.Begin, local, "начала");
                    end = ResolveEnd(cable.End, local, "конца", cable.Point);
                    if (!end.Contains(cable.Point)) throw new InvalidOperationException("Точка перемещена от окончания кабеля. Заново расставьте кабель на схеме.");
                }
                if (begin.ModelId <= 0 || end.ModelId <= 0) throw new InvalidOperationException("У оборудования не заполнен корректный NS_ElementId.");
                result.Add(new CableConnection(cable, begin.ModelId, end.ModelId, star,
                    beginLabel: star ? Label(begin) : null, endLabel: star ? Label(end) : null));
            }
            catch (InvalidOperationException ex) { result.Add(new CableConnection(cable, 0, 0, star, ex.Message)); }
        }
        // Одинаковый номер у разных кабелей теряет смысл на общей трассе: не объединяем молча.
        var duplicates = new HashSet<string>(cables.Where(c => !string.IsNullOrWhiteSpace(c.Number))
            .GroupBy(c => c.Number, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.Ordinal);
        return result.Select(r => duplicates.Contains(r.Cable.Number)
            ? new CableConnection(r.Cable, r.BeginId, r.EndId, r.IsStar, "Номер кабеля повторяется на этом виде. Проверьте зелёные точки.", r.BeginLabel, r.EndLabel) : r).ToList();
    }

    private static (DiagramEquipment Begin, DiagramEquipment End) ResolveStar(DiagramCable cable,
        IReadOnlyList<List<DiagramLine>> pinnedParts, IReadOnlyList<List<DiagramLine>> unpinnedParts, List<DiagramEquipment> equipment)
    {
        // Убираем закреплённые линии из обхода ответвлений: иначе все окончания звезды
        // попадут в один поиск по имени. Конкретное ответвление задаётся положением зелёной точки.
        var branches = unpinnedParts.Where(b => b.Any(l => l.Contains(cable.Point))).ToList();
        if (branches.Count != 1)
            throw new InvalidOperationException("Зелёная точка звезды должна находиться на незакреплённом ответвлении к оборудованию.");
        var branch = branches[0];
        var buses = pinnedParts.Where(b => b.Any(pinned => branch.Any(line => Touches(pinned, line)))).ToList();
        if (buses.Count != 1)
            throw new InvalidOperationException("Ответвление должно присоединяться к одной закреплённой линии или связной цепочке закреплённых линий.");
        var bus = buses[0];
        var begin = UniqueEquipment(equipment.Where(e => bus.Any(l => e.Contains(l.A) || e.Contains(l.B))).ToList(),
            "У закреплённой линии не найден элемент начала звезды.",
            "Закреплённая линия присоединена к нескольким элементам модели. У звезды должно быть одно начало.");
        if (begin.ModelId <= 0) throw new InvalidOperationException("У начала звезды не заполнен корректный NS_ElementId.");
        // Названия панелей у разных окончаний могут совпадать. Берём NS_ElementId
        // обозначения, к которому пришло незакреплённое ответвление с этой зелёной точкой.
        var end = UniqueEquipment(equipment.Where(e => e.ModelId != begin.ModelId && e.Contains(cable.Point) &&
            branch.Any(l => (l.A.Distance(cable.Point) <= 1 && e.Contains(l.A)) ||
                            (l.B.Distance(cable.Point) <= 1 && e.Contains(l.B)))).ToList(),
            "У зелёной точки не найден элемент окончания незакреплённого ответвления звезды.",
            "В окончании ответвления совмещены несколько элементов модели. Разведите обозначения на схеме.");
        return (begin, end);
    }

    private static DiagramEquipment UniqueEquipment(List<DiagramEquipment> candidates, string missing, string ambiguous)
    {
        if (candidates.Count == 0) throw new InvalidOperationException(missing);
        if (candidates.Select(e => e.ModelId).Distinct().Count() != 1) throw new InvalidOperationException(ambiguous);
        return candidates[0];
    }

    private static string Label(DiagramEquipment equipment) => string.IsNullOrWhiteSpace(equipment.Panel)
        ? $"Элемент {equipment.ModelId}" : equipment.Panel;

    private static bool Touches(DiagramLine a, DiagramLine b) => a.Contains(b.A) || a.Contains(b.B) || b.Contains(a.A) || b.Contains(a.B);

    private static List<List<DiagramLine>> Components(IReadOnlyList<DiagramLine> lines)
    {
        var parent = Enumerable.Range(0, lines.Count).ToArray();
        int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        // Конец ответвления на середине шины образует соединение. Пересечение линий
        // без конечной точки не склеивает независимые сети.
        for (int i=0; i<lines.Count; i++) for (int j=i+1; j<lines.Count; j++)
            if (Touches(lines[i], lines[j])) parent[Root(j)] = Root(i);
        return Enumerable.Range(0, lines.Count).GroupBy(Root).Select(g => g.Select(i => lines[i]).ToList()).ToList();
    }

    private static DiagramEquipment ResolveEnd(string panel, List<DiagramEquipment> equipment, string side, DiagramPoint? point = null)
    {
        if (string.IsNullOrWhiteSpace(panel)) throw new InvalidOperationException($"Не заполнено имя {side} кабеля.");
        var matches = equipment.Where(e => string.Equals(e.Panel.Trim(), panel.Trim(), StringComparison.Ordinal)).ToList();
        if (matches.Count == 0) throw new InvalidOperationException($"Не найдено оборудование {side} «{panel}» в соединении. Проверьте NS_Имя панели.");
        var ids = matches.Select(e => e.ModelId).Distinct().ToList();
        if (ids.Count != 1) throw new InvalidOperationException($"Неоднозначное имя {side} «{panel}»: несколько NS_ElementId в одной сети.");
        return point.HasValue ? matches.FirstOrDefault(e => e.Contains(point.Value)) ?? matches[0] : matches[0];
    }
}
