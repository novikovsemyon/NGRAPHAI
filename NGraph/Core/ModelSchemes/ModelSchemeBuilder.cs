using System.Globalization;
using NGraph.Core.FunctionalScheme;

namespace NGraph.Core.ModelSchemes;

/// <summary>Построитель только для новой команды по параметрам. Не участвует в работе CreateSxemaByModel.</summary>
internal sealed class ModelSchemeBuilder
{
    private readonly Document _document;
    private static double Mm(double value) => value / 304.8;
    public ModelSchemeBuilder(Document document) { _document = document; }

    public ViewDrafting Build(SchemePlan plan, SchemeOptions options, string iniDirectory)
    {
        if (!plan.CanBuild) throw new InvalidOperationException(string.Join(Environment.NewLine, plan.Errors));
        var symbols = ResolveSymbols(plan, iniDirectory);
        var textTypes = new FilteredElementCollector(_document).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().ToList();
        TextNoteType TextType(string name) => textTypes.FirstOrDefault(t => t.Name == name)
            ?? throw new InvalidOperationException($"Загрузите в проект тип текста «{name}».");
        var sectionText = TextType("ГОСТ тип А h=5 c=3.5");
        var levelText = TextType("ГОСТ тип А h=3.5 c=2.5");
        var roomText = TextType("ГОСТ тип А h=2 c=1.4");
        var viewType = new FilteredElementCollector(_document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(t => t.ViewFamily == ViewFamily.Drafting)
            ?? throw new InvalidOperationException("В проекте нет типа чертёжного вида.");
        var name = GetViewName(_document, options.ViewName);

        using var transaction = new Transaction(_document, "Структурная схема по модели");
        transaction.Start();
        var view = ViewDrafting.Create(_document, viewType.Id);
        view.Name = name; view.Scale = 1;
        foreach (var symbol in symbols.Values.Distinct()) if (!symbol.IsActive) symbol.Activate();
        _document.Regenerate();

        var placed = new Dictionary<string, PlacedEquipment>();
        foreach (var item in plan.Elements)
        {
            var instance = _document.Create.NewFamilyInstance(XYZ.Zero, symbols[item.Id], view);
            SetEquipmentParameters(instance, item.Source, view.Name);
            placed.Add(item.Id, new PlacedEquipment(instance));
        }
        // Габариты получаем у созданных экземпляров с заполненными надписями, а не у неактивного FamilySymbol.
        _document.Regenerate();
        foreach (var item in placed.Values)
        {
            var box = item.Instance.get_BoundingBox(view)
                ?? throw new InvalidOperationException($"У УГО «{item.Instance.Name}» нет габаритов на чертёжном виде.");
            item.Min = new XYZ(box.Min.X, box.Min.Y, 0);
            item.Width = Math.Max(box.Max.X - box.Min.X, Mm(2));
            item.Height = Math.Max(box.Max.Y - box.Min.Y, Mm(2));
        }

        var sections = new List<DrawingSection>();
        double sectionX = 0;
        foreach (var sectionGroup in plan.Groups.GroupBy(g => g.First.SectionKey))
        {
            var section = new DrawingSection { Caption = sectionGroup.First().SectionName, Origin = new XYZ(sectionX, 0, 0) };
            foreach (var levelGroup in sectionGroup.GroupBy(g => g.First.LevelKey))
            {
                var first = levelGroup.First().First;
                var level = new DrawingLevel { Origin = new XYZ(0, section.Height, 0), Caption = first.LevelName };
                if (first.ElevationMillimeters.HasValue)
                    level.Caption += " (отм. " + (Math.Round(first.ElevationMillimeters.Value / 10, MidpointRounding.AwayFromZero) * 10)
                        .ToString("0", CultureInfo.InvariantCulture) + ")";
                foreach (var group in levelGroup)
                {
                    var room = new DrawingRoom { Caption = group.GroupCaption, Origin = new XYZ(level.Width, 0, 0) };
                    double rowY = 0;
                    foreach (var row in group.Elements.GroupBy(e => e.Source.Position))
                    {
                        double rowX = 0, rowHeight = 0;
                        foreach (var element in row)
                        {
                            var item = placed[element.Id]; item.Local = new XYZ(rowX, rowY, 0);
                            room.Equipment.Add(item);
                            room.Width = Math.Max(room.Width, rowX + item.Width);
                            rowHeight = Math.Max(rowHeight, item.Height);
                            rowX += item.Width + Math.Max(item.Width, Mm(5));
                        }
                        room.Height = rowY + rowHeight;
                        rowY += rowHeight + Math.Max(rowHeight, Mm(5));
                    }
                    room.Width = Math.Max(room.Width, Mm(40));
                    room.Height = Math.Max(room.Height, Mm(10)) + Mm(12); // Место для подписи группы.
                    level.Rooms.Add(room);
                    level.Width += room.Width + Mm(25);
                    level.Height = Math.Max(level.Height, room.Height);
                }
                section.Levels.Add(level);
                section.Width = Math.Max(section.Width, level.Width);
                section.Height += level.Height + Mm(25);
            }
            sections.Add(section);
            sectionX += section.Width + Mm(70);
        }

        var lineStyle = GetLineStyle();
        foreach (var section in sections)
        {
            DrawBox(view, section.Origin - Offset(15), section.Origin + new XYZ(section.Width, section.Height, 0) + Offset(15), lineStyle, true, false, true, false);
            TextNote.Create(_document, view.Id, section.Origin + new XYZ(-Mm(15), section.Height + Mm(12), 0),
                Math.Max(section.Width, Mm(40)), section.Caption, new TextNoteOptions(sectionText.Id));
            foreach (var level in section.Levels)
            {
                var levelOrigin = section.Origin + level.Origin;
                DrawBox(view, levelOrigin - Offset(10), levelOrigin + new XYZ(level.Width, level.Height, 0) + Offset(10), lineStyle, false, false, false, true);
                TextNote.Create(_document, view.Id, levelOrigin + new XYZ(-Mm(15), -Mm(5), 0), Math.Max(level.Height, Mm(30)),
                    level.Caption, new TextNoteOptions(levelText.Id) { Rotation = Math.PI / 2 });
                foreach (var room in level.Rooms)
                {
                    var origin = levelOrigin + room.Origin;
                    DrawBox(view, origin - Offset(5), origin + new XYZ(room.Width, room.Height, 0) + Offset(5), lineStyle, true, true, true, true);
                    TextNote.Create(_document, view.Id, origin + new XYZ(0, room.Height, 0), room.Width,
                        room.Caption, new TextNoteOptions(roomText.Id));
                    foreach (var equipment in room.Equipment)
                        ElementTransformUtils.MoveElement(_document, equipment.Instance.Id, origin + equipment.Local - equipment.Min);
                }
            }
        }
        if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit отменил создание схемы.");
        return view;
    }

    private Dictionary<string, FamilySymbol> ResolveSymbols(SchemePlan plan, string iniDirectory)
    {
        var ini = new SchemeIniCatalog(iniDirectory);
        var resolver = new SchemeSymbolResolver(_document);
        var result = new Dictionary<string, FamilySymbol>(); var errors = new List<string>();
        foreach (var item in plan.Elements)
        {
            try
            {
                var name = ini.Resolve(item.Source.IniGroup, item.Source.Position);
                result.Add(item.Id, resolver.Resolve(name));
            }
            catch (InvalidOperationException ex) { errors.Add($"Элемент {item.Id}: {ex.Message}"); }
        }
        if (errors.Count > 0) throw new InvalidOperationException("Проверьте соответствия INI и семейства:\n" + string.Join("\n", errors.Take(15))
            + (errors.Count > 15 ? $"\nЕщё ошибок: {errors.Count - 15}." : ""));
        return result;
    }

    internal static string GetViewName(Document document, string requested)
    {
        var names = new HashSet<string>(new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>()
            .Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
        var name = requested.Trim(); var suffix = 2;
        while (names.Contains(name)) name = requested.Trim() + " (" + suffix++ + ")";
        return name;
    }

    internal static void SetEquipmentParameters(FamilyInstance instance, SchemeSourceElement source, string viewName)
    {
        var values = new Dictionary<string, string> {
            [Const.Param_NS_ElementId] = source.Id, [Const.Param_ADSK_Position] = source.Position,
            [Const.Param_ADSK_Group] = source.IniGroup, ["NS_Имя панели"] = source.PanelName,
            ["NS_Текст УГО"] = source.Position, ["ADSK_Наименование краткое"] = source.ShortName,
            [Const.Param_CJ_Work] = viewName, [Const.Param_CJ_Begin] = " ", [Const.Param_CJ_End] = " ",
            [Const.Param_CJ_Begin_eq] = " ", [Const.Param_CJ_End_eq] = " ", [Const.Param_CJ_Number] = " ",
            [Const.Param_CJ_Mark] = " ", [Const.Param_CJ_Nets] = " " };
        foreach (var value in values)
        {
            var parameter = InstanceParameter(instance, value.Key, StorageType.String);
            if (parameter.AsString() != value.Value && !parameter.Set(value.Value))
                throw new InvalidOperationException($"Не удалось записать «{value.Key}» в УГО «{instance.Name}».");
        }
        var length = InstanceParameter(instance, Const.Param_CJ_Lenght, StorageType.Integer);
        if (length.AsInteger() != 1 && !length.Set(1)) throw new InvalidOperationException("Не удалось заполнить длину кабеля в УГО.");
    }
    private static Parameter InstanceParameter(FamilyInstance instance, string name, StorageType storage)
    {
        var matches = instance.GetParameters(name);
        if (matches.Count != 1 || matches[0].IsReadOnly || matches[0].StorageType != storage)
            throw new InvalidOperationException($"У УГО «{instance.Symbol.FamilyName}: {instance.Name}» отсутствует однозначный записываемый параметр экземпляра «{name}» типа {storage}.");
        return matches[0];
    }
    private GraphicsStyle GetLineStyle()
    {
        const string name = "grey_1_штрих";
        var categories = _document.Settings.Categories;
        var lines = categories.get_Item(BuiltInCategory.OST_Lines);
        if (lines.SubCategories.Contains(name)) return lines.SubCategories.get_Item(name).GetGraphicsStyle(GraphicsStyleType.Projection);
        var category = categories.NewSubcategory(lines, name);
        category.SetLineWeight(1, GraphicsStyleType.Projection); category.LineColor = new Autodesk.Revit.DB.Color(128, 128, 128);
        var pattern = LinePatternElement.GetLinePatternElementByName(_document, "Штрих")
            ?? LinePatternElement.GetLinePatternElementByName(_document, "NGraph_Штрих");
        if (pattern == null)
        {
            var definition = new LinePattern("NGraph_Штрих");
            definition.SetSegments(new List<LinePatternSegment> {
                new(LinePatternSegmentType.Dash, Mm(2)), new(LinePatternSegmentType.Space, Mm(1)) });
            pattern = LinePatternElement.Create(_document, definition);
        }
        category.SetLinePatternId(pattern.Id, GraphicsStyleType.Projection);
        return category.GetGraphicsStyle(GraphicsStyleType.Projection);
    }
    private void DrawBox(View view, XYZ min, XYZ max, GraphicsStyle style, bool left, bool bottom, bool right, bool top)
    {
        var points = new[] { min, new XYZ(min.X, max.Y, 0), max, new XYZ(max.X, min.Y, 0) };
        var enabled = new[] { left, top, right, bottom };
        for (var i = 0; i < 4; i++)
        {
            if (!enabled[i]) continue;
            var curve = _document.Create.NewDetailCurve(view, Line.CreateBound(points[i], points[(i + 1) % 4]));
            curve.LineStyle = style;
        }
    }
    private static XYZ Offset(double mm) => new(Mm(mm), Mm(mm), 0);
    private sealed class PlacedEquipment
    {
        public FamilyInstance Instance { get; }
        public XYZ Min { get; set; } = XYZ.Zero;
        public XYZ Local { get; set; } = XYZ.Zero;
        public double Width { get; set; }
        public double Height { get; set; }
        public PlacedEquipment(FamilyInstance instance) { Instance = instance; }
    }
    private class DrawingBox
    {
        public string Caption { get; set; } = string.Empty;
        public XYZ Origin { get; set; } = XYZ.Zero;
        public double Width { get; set; }
        public double Height { get; set; }
    }
    private sealed class DrawingRoom : DrawingBox { public List<PlacedEquipment> Equipment { get; } = new(); }
    private sealed class DrawingLevel : DrawingBox { public List<DrawingRoom> Rooms { get; } = new(); }
    private sealed class DrawingSection : DrawingBox { public List<DrawingLevel> Levels { get; } = new(); }
}
