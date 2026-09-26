using Autodesk.Revit.DB.Mechanical;
using NGraph.Core.FunctionalScheme;

namespace NGraph.Core.CableRouting;

/// <summary>Снимок чертёжного вида и физических соединений. Чтение не открывает транзакций.</summary>
internal sealed class RevitCableSource
{
    private readonly Document _document;
    public ViewDrafting View { get; }
    public IReadOnlyList<CableConnection> Connections { get; }
    public CableRouteGraph Graph { get; } = new();
    public Dictionary<long, Element> Ducts { get; } = new();
    public Dictionary<long, string> DuctSystems { get; } = new();
    public Dictionary<long, string> DuctSystemIds { get; } = new();

    public RevitCableSource(Document document, ViewDrafting view)
    {
        _document = document; View = view;
        Connections = ReadDiagram();
        ReadNetworks();
    }

    public static long Id(ElementId id) => long.Parse(id.ToString(), System.Globalization.CultureInfo.InvariantCulture);
    public static string Text(Element e, string name)
    {
        var ps = e.GetParameters(name);
        if (ps.Count > 1) throw new InvalidOperationException($"У элемента {e.Id} несколько параметров «{name}».");
        var p = ps.SingleOrDefault();
        return p == null ? "" : (p.StorageType == StorageType.String ? p.AsString() ?? "" : p.AsValueString() ?? "").Trim();
    }

    private IReadOnlyList<CableConnection> ReadDiagram()
    {
        var settings = UserSettings.Load();
        var equipment = new List<DiagramEquipment>(); var cables = new List<DiagramCable>();
        var elements = new FilteredElementCollector(_document, View.Id).WhereElementIsNotElementType().ToElements();
        foreach (var instance in elements.OfType<FamilyInstance>().Where(e => e.Category?.Id == new ElementId(BuiltInCategory.OST_DetailComponents)))
        {
            if (instance.Name == settings.SignalType || instance.Name == settings.SignalWithoutTagType)
            {
                var cable = new DiagramCable { Id = Id(instance.Id) };
                try
                {
                    cable.Number = Text(instance, Const.Param_CJ_Number);
                    cable.Begin = Text(instance, Const.Param_CJ_Begin); cable.End = Text(instance, Const.Param_CJ_End);
                    cable.Point = Point(instance.GetPlacementPoint());
                }
                catch (Exception ex) { cable.Error = ex.Message; }
                cables.Add(cable);
            }
            else
            {
                var box = instance.get_BoundingBox(View); if (box == null) continue;
                var panel = Text(instance, Const.Param_NS_PanelName);
                var link = Text(instance, Const.Param_NS_ElementId);
                if (panel.Length == 0 && link.Length == 0) continue;
                long.TryParse(link, out var modelId);
                var corners = new List<DiagramPoint>();
                foreach (var x in new[] { box.Min.X, box.Max.X }) foreach (var y in new[] { box.Min.Y, box.Max.Y })
                    foreach (var z in new[] { box.Min.Z, box.Max.Z }) corners.Add(Point(box.Transform.OfPoint(new XYZ(x,y,z))));
                equipment.Add(new DiagramEquipment { Id = Id(instance.Id), ModelId = modelId, Panel = panel,
                    Min = new DiagramPoint(corners.Min(p => p.X), corners.Min(p => p.Y)),
                    Max = new DiagramPoint(corners.Max(p => p.X), corners.Max(p => p.Y)) });
            }
        }
        var lines = new List<DiagramLine>();
        foreach (var line in elements.OfType<CurveElement>().Where(e => e.LineStyle?.Name.Contains("*NG*") == true))
        {
            var points = line.GeometryCurve.Tessellate();
            for (var i=1; i<points.Count; i++) lines.Add(new DiagramLine { A = Point(points[i-1]), B = Point(points[i]), Pinned = line.Pinned });
        }
        return CableDiagram.Resolve(equipment, lines, cables);
    }

    private DiagramPoint Point(XYZ p) => new(p.DotProduct(View.RightDirection)*304.8, p.DotProduct(View.UpDirection)*304.8);

    private void ReadNetworks()
    {
        var endpoints = Connections.Where(c => c.Error.Length == 0).SelectMany(c => new[] { c.BeginId, c.EndId })
            .Distinct().Select(ModelElement).OfType<Element>().ToList();
        // Система выбирается по физическим HVAC-портам оборудования; электрические цепи не читаются.
        var systems = endpoints.SelectMany(Ports).Select(c => c.MEPSystem).OfType<MechanicalSystem>()
            .GroupBy(s => s.Id).Select(g => g.First()).OrderBy(s => Id(s.Id)).ToList();
        foreach (var system in systems)
        {
            var elements = system.DuctNetwork.Cast<Element>().Concat(system.Elements.Cast<Element>()).Concat(endpoints)
                .Concat(system.BaseEquipment == null ? Enumerable.Empty<Element>() : new[] { system.BaseEquipment })
                .GroupBy(e => e.Id).Select(g => g.First()).ToList();
            var ports = new Dictionary<string, Connector>(StringComparer.Ordinal);
            foreach (var element in elements)
            {
                var connectors = Ports(element).Where(c => c.MEPSystem?.Id == system.Id).OrderBy(c => c.Id).ToList();
                foreach (var connector in connectors)
                {
                    var key = PortKey(system, connector); ports[key] = connector; Graph.AddPort(key, Id(element.Id));
                }
                if (connectors.Count < 2) continue;
                if (element is MEPCurve duct && (element is Duct || element is FlexDuct))
                {
                    Ducts[Id(element.Id)] = element; DuctSystems[Id(element.Id)] = system.Name; DuctSystemIds[Id(element.Id)] = system.Id.ToString();
                    var positions = DuctStations(duct, connectors);
                    for (int i=1; i<positions.Count; i++) Graph.Connect(PortKey(system, positions[i-1].Port), PortKey(system, positions[i].Port),
                        positions[i].Meters-positions[i-1].Meters, Id(element.Id), system.Name);
                }
                else if (element is FamilyInstance)
                {
                    // Фитинги и оборудование дают связность внутри ОДНОЙ механической системы.
                    // В длину входит только ось воздуховодов, как TotalCurveLength у GraphId.
                    for (int i=1; i<connectors.Count; i++) Graph.Connect(PortKey(system, connectors[0]), PortKey(system, connectors[i]), 0, system: system.Name);
                }
            }
            foreach (var pair in ports)
            {
                var connector = pair.Value;
                foreach (Connector other in connector.AllRefs)
                {
                    if (other.Owner.Id == connector.Owner.Id || !Physical(other) || !connector.IsConnectedTo(other)) continue;
                    var key = PortKey(system, other);
                    if (ports.ContainsKey(key) && string.CompareOrdinal(pair.Key, key) < 0) Graph.Connect(pair.Key, key, 0, system: system.Name);
                }
            }
        }
    }

    private static List<(Connector Port, double Meters)> DuctStations(MEPCurve duct, List<Connector> ports)
    {
        if (duct.Location is LocationCurve location)
        {
            var curve = location.Curve; var start = curve.GetEndParameter(0); var finish = curve.GetEndParameter(1);
            return ports.Select(port =>
            {
                var projection = curve.Project(port.Origin) ?? throw new InvalidOperationException($"Не удалось прочитать ось воздуховода {duct.Id}.");
                var parameter = Math.Max(start, Math.Min(finish, projection.Parameter));
                double feet;
                if (parameter <= start + 1e-9) feet = 0;
                else if (parameter >= finish - 1e-9) feet = curve.Length;
                else if (curve is Line || curve is Arc) feet = curve.Length * (parameter-start)/(finish-start);
                else { using var part = curve.Clone(); part.MakeBound(start, parameter); feet = part.Length; }
                return (Port: port, Meters: feet*0.3048);
            }).OrderBy(p => p.Meters).ThenBy(p => p.Port.Id).ToList();
        }
        var length = duct.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
        if (ports.Count != 2 || length?.StorageType != StorageType.Double || length.AsDouble() <= 0)
            throw new InvalidOperationException($"Нет корректной длины оси воздуховода {duct.Id}.");
        return new List<(Connector, double)> { (ports[0], 0), (ports[1], length.AsDouble()*0.3048) };
    }

    private static string PortKey(MechanicalSystem system, Connector port) => $"{system.Id}:{port.Owner.Id}:{port.Id}";
    private Element? ModelElement(long id)
    {
        try { return _document.GetElement(RevitElementAccess.CreateId(id)); }
        catch (OverflowException) { return null; } // неверный NS_ElementId отобразится в строке, остальные кабели доступны
    }
    private static bool Physical(Connector c) => c.Domain == Domain.DomainHvac &&
        (c.ConnectorType == ConnectorType.End || c.ConnectorType == ConnectorType.Curve || c.ConnectorType == ConnectorType.Physical);
    private static IEnumerable<Connector> Ports(Element element)
    {
        var manager = element is MEPCurve curve ? curve.ConnectorManager : (element as FamilyInstance)?.MEPModel?.ConnectorManager;
        return manager == null ? Enumerable.Empty<Connector>() : manager.Connectors.Cast<Connector>().Where(Physical);
    }
}
