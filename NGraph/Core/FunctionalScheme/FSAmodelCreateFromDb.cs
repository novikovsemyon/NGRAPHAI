using Autodesk.Revit.UI;

namespace NGraph.Core.FunctionalScheme;

public class FSAmodelCreateFromDb
{
    /// <summary>Читает карточки каталога без предположений о составе семейств внутри областей.</summary>
    public static List<СекцияБазыДанных> CreateFromBd_ReadFilledRegion(Document document, View? view)
    {
        if (view == null)
        {
            TaskDialog.Show("NGraph", "Отсутствует чертёжный вид базы элементов.");
            return new List<СекцияБазыДанных>();
        }
        var regionType = UserSettings.Load().RegionType;
        return new FilteredElementCollector(document).OwnedByView(view.Id)
            .OfClass(typeof(FilledRegion)).Cast<FilledRegion>()
            .Where(region => document.GetElement(region.GetTypeId())?.Name == regionType)
            .Select(region => new СекцияБазыДанных(region, view)).ToList();
    }

    /// <summary>
    /// Сохраняет исходные элементы для штатного копирования Revit. Видимость семейства
    /// не определяет его тип: FAS_Точка_связи и ГОСТ21.208 могут не иметь параметров сигнала или марки.
    /// </summary>
    public static void CreateFromBd_AddToSectionOtherElements(Document document, View? view,
        ref СекцияБазыДанных section)
    {
        if (view == null) throw new ArgumentException("Не выбран вид базы.");
        var bounds = section.FilledRegion.get_BoundingBox(view)
            ?? throw new InvalidOperationException("Не удалось определить границы выбранного варианта.");
        // OwnedByView включает скрытые элементы, но исключает семейства с соседних видов.
        var elements = new FilteredElementCollector(document).OwnedByView(view.Id)
            .WhereElementIsNotElementType().ToElements();
        // Марки внутри группы уже входят в её копию; отдельно добавляем только внешние марки.
        var tags = elements.OfType<IndependentTag>().Where(tag => tag.GroupId == ElementId.InvalidElementId).ToList();
        section.PlacementItems.Clear();
        foreach (var element in elements)
        {
            // Участники группы и вложенные семейства копируются вместе с родителем ровно один раз.
            if (element.GroupId != ElementId.InvalidElementId) continue;
            if (element is FamilyInstance nested && nested.SuperComponent != null) continue;
            if (!(element is FamilyInstance || element is Group || element is DetailCurve || element is TextNote)) continue;
            bool inside;
            if (element.Location is LocationPoint point)
                inside = ContainsPoint(bounds, point.Point);
            else
            {
                var box = element.get_BoundingBox(view);
                inside = box != null && ContainsPoint(bounds, box.Transform.OfPoint(box.Min))
                    && ContainsPoint(bounds, box.Transform.OfPoint(box.Max));
            }
            if (!inside) continue;

            var hosts = new HashSet<ElementId>();
            AddMembers(document, element, hosts);
            var attachedTags = tags.Where(tag => TaggedIds(tag).Any(hosts.Contains)).ToList();
            section.PlacementItems.Add(new CatalogPlacementItem(element, attachedTags, element.IsHidden(view)));
        }
        if (section.PlacementItems.Count == 0)
            throw new InvalidOperationException("В выбранной области нет элементов для вставки.");
    }

    // У чертёжных семейств габарит по Z может выходить за плоскую цветовую область.
    // Принадлежность варианту определяется только координатами в плоскости чертежа.
    private static bool ContainsPoint(BoundingBoxXYZ bounds, XYZ point)
    {
        var p = bounds.Transform.Inverse.OfPoint(point);
        const double tolerance = 1e-7;
        return p.X >= bounds.Min.X - tolerance && p.X <= bounds.Max.X + tolerance
            && p.Y >= bounds.Min.Y - tolerance && p.Y <= bounds.Max.Y + tolerance;
    }

    private static void AddMembers(Document document, Element element, HashSet<ElementId> ids)
    {
        if (!ids.Add(element.Id)) return;
        IEnumerable<ElementId> children = element is Group group ? group.GetMemberIds()
            : element is FamilyInstance family ? family.GetSubComponentIds() : Array.Empty<ElementId>();
        foreach (var id in children)
            if (document.GetElement(id) is Element child) AddMembers(document, child, ids);
    }

    private static IEnumerable<ElementId> TaggedIds(IndependentTag tag)
    {
#if REVIT2022_OR_GREATER
        return tag.GetTaggedLocalElementIds();
#else
        var id = tag.TaggedLocalElementId;
        return id != ElementId.InvalidElementId ? new[] { id } : Array.Empty<ElementId>();
#endif
    }

    /// <summary>
    /// Вызывается внутри транзакции команды. Копирование сохраняет все параметры,
    /// ориентацию, типоразмеры и марки, включая семейства, неизвестные NGraph.
    /// Пустое обозначение означает «сохранить значения из базы».
    /// </summary>
    public static void CreateFromBd_withoutTransaction(Document document, ref СекцияБазыДанных section,
        View target, string installationName, XYZ origin)
    {
        var source = document.GetElement(section.FilledRegion.OwnerViewId) as View
            ?? throw new InvalidOperationException("Не найден исходный вид базы.");
        var viewTransform = ElementTransformUtils.GetTransformFromViewToView(source, target);
        var translation = Transform.CreateTranslation(origin - viewTransform.OfPoint(section.Origin));
        using var options = new CopyPasteOptions();
        foreach (var item in section.PlacementItems)
        {
            var ids = new[] { item.Source.Id }.Concat(item.Tags.Select(t => t.Id)).Distinct().ToList();
            var copied = ElementTransformUtils.CopyElements(source, ids, target, translation, options)
                .Select(document.GetElement).Where(x => x != null).ToList();
            foreach (var family in copied.OfType<FamilyInstance>().Where(x => x.GroupId == ElementId.InvalidElementId))
            {
                if (!string.IsNullOrWhiteSpace(installationName))
                    SetTextIfWritable(family, "_Установка", installationName.Trim());
                SetTextIfWritable(family, "CJ_Рабочий набор", target.Name);
            }
            // Скрытое оборудование остаётся скрытым, а его марки — видимыми.
            if (item.Hidden)
            {
                var hidden = copied.Where(x => x.GetType() == item.Source.GetType()
                    && x.GroupId == ElementId.InvalidElementId && x.CanBeHidden(target)
                    && !x.IsHidden(target)).Select(x => x.Id).ToList();
                if (hidden.Count > 0) target.HideElements(hidden);
            }
        }
    }

    private static void SetTextIfWritable(Element element, string name, string value)
    {
        var parameter = element.LookupParameter(name);
        if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.String)
            parameter.Set(value);
    }
}

internal sealed class CatalogPlacementItem
{
    public Element Source { get; }
    public IReadOnlyList<IndependentTag> Tags { get; }
    public bool Hidden { get; }
    public CatalogPlacementItem(Element source, IReadOnlyList<IndependentTag> tags, bool hidden)
    { Source = source; Tags = tags; Hidden = hidden; }
}

    public class СекцияБазыДанных
    {
        internal List<CatalogPlacementItem> PlacementItems { get; } = new();
        public FilledRegion FilledRegion { get;}
        public string Name { get;}
        public string GroupName { get; }
        public string Code { get; }
        public string Type { get; }
        public XYZ Габариты { get; }
        public XYZ Центр { get;}
        public XYZ Origin { get; }
        
        
        public СекцияБазыДанных(FilledRegion filledRegion , View view)
        {
            FilledRegion = filledRegion;
                    var settings = UserSettings.Load();
            string Read(string name) => NgContext._FindParameter(filledRegion, name)?.AsString() ?? "";
            Code = Read(settings.RegionCodeParameter);
            Name = Read(settings.RegionNameParameter);
            if (string.IsNullOrWhiteSpace(Name)) Name = Code;
            GroupName = Read(settings.RegionGroupParameter);
            Type = filledRegion.Document.GetElement(filledRegion.GetTypeId())?.Name ?? "";
            var bb = filledRegion.get_BoundingBox(view);
            Габариты = bb.Max - bb.Min;
            Центр = Габариты / 2;
            Origin = bb.Min;
        }

    }
