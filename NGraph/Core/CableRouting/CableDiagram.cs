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
    public CableConnection(DiagramCable cable, long beginId, long endId, bool star, string error = "")
    { Cable = cable; BeginId = beginId; EndId = endId; IsStar = star; Error = error; }
}

/// <summary>Соединения читаются из существующих зелёных точек, без создания электрических цепей.</summary>
public static class CableDiagram
{
    public static IReadOnlyList<CableConnection> Resolve(IReadOnlyList<DiagramEquipment> equipment,
        IReadOnlyList<DiagramLine> lines, IReadOnlyList<DiagramCable> cables)
    {
        var parent = Enumerable.Range(0, lines.Count).ToArray();
        int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        // Ответвление может заканчиваться внутри закреплённой шины. Пересечение двух
        // линий без конечной точки не является соединением и не склеивает разные сети.
        for (int i=0; i<lines.Count; i++) for (int j=i+1; j<lines.Count; j++)
            if (lines[i].Contains(lines[j].A) || lines[i].Contains(lines[j].B) ||
                lines[j].Contains(lines[i].A) || lines[j].Contains(lines[i].B)) parent[Root(j)] = Root(i);
        var components = Enumerable.Range(0, lines.Count).GroupBy(Root)
            .Select(g => g.Select(i => lines[i]).ToList()).ToList();
        var result = new List<CableConnection>();
        foreach (var cable in cables)
        {
            try
            {
                if (cable.Error.Length > 0) throw new InvalidOperationException(cable.Error);
                if (string.IsNullOrWhiteSpace(cable.Number)) throw new InvalidOperationException("Не заполнен CJ_Номер кабеля.");
                var candidates = components.Where(c => c.Any(l => l.Contains(cable.Point))).ToList();
                if (candidates.Count != 1) throw new InvalidOperationException("Зелёная точка должна находиться на одной сети линий *NG*. Проверьте её положение.");
                var component = candidates[0];
                var local = equipment.Where(e => component.Any(l => e.Contains(l.A) || e.Contains(l.B))).ToList();
                var begin = ResolveEnd(cable.Begin, local, "начала");
                var end = ResolveEnd(cable.End, local, "конца");
                if (!end.Contains(cable.Point)) throw new InvalidOperationException("Точка перемещена от окончания кабеля. Заново расставьте кабель на схеме.");
                if (begin.ModelId <= 0 || end.ModelId <= 0) throw new InvalidOperationException("У оборудования не заполнен корректный NS_ElementId.");
                var star = component.Any(l => l.Pinned);
                if (star && !component.Any(l => l.Pinned && (begin.Contains(l.A) || begin.Contains(l.B))))
                    throw new InvalidOperationException("Начало звезды не примыкает к закреплённой линии. Проверьте CJ_Начало кабеля.");
                result.Add(new CableConnection(cable, begin.ModelId, end.ModelId, star));
            }
            catch (InvalidOperationException ex) { result.Add(new CableConnection(cable, 0, 0, false, ex.Message)); }
        }
        // Одинаковый номер у разных кабелей теряет смысл на общей трассе: не объединяем молча.
        var duplicates = new HashSet<string>(cables.Where(c => !string.IsNullOrWhiteSpace(c.Number))
            .GroupBy(c => c.Number, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.Ordinal);
        return result.Select(r => duplicates.Contains(r.Cable.Number)
            ? new CableConnection(r.Cable, r.BeginId, r.EndId, r.IsStar, "Номер кабеля повторяется на этом виде. Проверьте зелёные точки.") : r).ToList();
    }

    private static DiagramEquipment ResolveEnd(string panel, List<DiagramEquipment> equipment, string side)
    {
        if (string.IsNullOrWhiteSpace(panel)) throw new InvalidOperationException($"Не заполнено имя {side} кабеля.");
        var matches = equipment.Where(e => string.Equals(e.Panel.Trim(), panel.Trim(), StringComparison.Ordinal)).ToList();
        if (matches.Count == 0) throw new InvalidOperationException($"Не найдено оборудование {side} «{panel}» в соединении. Проверьте NS_Имя панели.");
        var ids = matches.Select(e => e.ModelId).Distinct().ToList();
        if (ids.Count != 1) throw new InvalidOperationException($"Неоднозначное имя {side} «{panel}»: несколько NS_ElementId в одной сети.");
        return matches[0];
    }
}
