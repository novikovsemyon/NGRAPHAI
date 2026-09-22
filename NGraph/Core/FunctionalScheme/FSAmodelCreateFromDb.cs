using Autodesk.Revit.UI;

namespace NGraph.Core.FunctionalScheme;

public class FSAmodelCreateFromDb
{
    public Element? GetBDfromModel(Document doc, string nameInDb) =>
        new Helpers().AllElementsOfCategory(doc, BuiltInCategory.OST_Views)
            .FirstOrDefault(x => x.Name == nameInDb);

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

public class Линия
    {
        public СекцияБазыДанных Cекция { get; }
        public DetailLine DetailLine { get; }
        public GraphicsStyle GraphicsStyle { get; }

        public XYZ Begin { get; }
        public XYZ End { get; }

        public Линия(СекцияБазыДанных cекция, DetailLine di)
        {
            Cекция = cекция;
            DetailLine = di;
            GraphicsStyle = DetailLine.LineStyle as GraphicsStyle;
            Begin = di.GeometryCurve.Tessellate().FirstOrDefault() - Cекция.Origin;
            End = di.GeometryCurve.Tessellate().LastOrDefault() - Cекция.Origin;
        }
    }

public class Оборудование
{
    public СекцияБазыДанных Cекция { get; }
    public XYZ Location { get; }
    public FamilySymbol FamilySymbol { get; }
    public Марка Марка { get; set; }
    public double Габарит_высота { get; }
    public double  Габарит_ширина { get;}
    public string  Наименование { get;}
    public string  НаименованиеКраткое { get;}
    public string  Позиция { get;}
    public string  Примечание { get;}
    public string NS_ElementId { get; }
    public string  _ГОСТ212008 { get;}
    public string _Принцип { get; }
    public string _Типизация { get; }
    public string _Установка { get; }
    public string _Элемент_на_щите { get; }
    public int _Din { get; }
    public int _Dout { get; }
    public int _Ain { get; }
    public int _Aout { get; }
    public int _HMI { get; }
    public int _Interface { get; }
    public Оборудование(СекцияБазыДанных cекция, FamilyInstance familyInstance)
    {
        Cекция = cекция;
        FamilySymbol = familyInstance.Symbol as FamilySymbol;
        Location = (familyInstance.Location as LocationPoint).Point - Cекция.Origin;
        Габарит_высота = familyInstance.LookupParameter("Габарит_высота").AsDouble();
        Габарит_ширина = familyInstance.LookupParameter("Габарит_ширина").AsDouble();
        Наименование = familyInstance.LookupParameter("ADSK_Наименование").AsString();
        НаименованиеКраткое = familyInstance.LookupParameter("ADSK_Наименование краткое").AsString();
        Позиция = familyInstance.LookupParameter("ADSK_Позиция").AsString();
        Примечание = familyInstance.LookupParameter("ADSK_Примечание").AsString();
        
        NS_ElementId = familyInstance.LookupParameter("NS_ElementId").AsString();
        _ГОСТ212008 = familyInstance.LookupParameter("_ГОСТ 21.208").AsString(); 
        _Принцип = familyInstance.LookupParameter("_Принцип").AsString();
        _Типизация = familyInstance.LookupParameter("_Типизация").AsString();
        _Установка = familyInstance.LookupParameter("_Установка").AsString();
        _Элемент_на_щите = familyInstance.LookupParameter("_Элемент_на_щите").AsString();
        _Din = familyInstance.LookupParameter("_Din").AsInteger();
        _Dout = familyInstance.LookupParameter("_Dout").AsInteger();
        _Ain = familyInstance.LookupParameter("_Ain").AsInteger();
        _Aout = familyInstance.LookupParameter("_Aout").AsInteger();
        _HMI = familyInstance.LookupParameter("_HMI").AsInteger();
        _Interface = familyInstance.LookupParameter("_Interface").AsInteger();
    }
}

public class Точка
    {
        public СекцияБазыДанных Cекция { get; }
       // public FamilyInstance NewFamilyInstance { get; set; }
        public XYZ Location { get; }
        public FamilySymbol FamilySymbol { get; }
        public Марка Марка { get; set; }

        public string NS_ElementId { get; }
        public string  _ГОСТ212008 { get;}
        public string _Принцип { get; }
        public string _Типизация { get; }
        public string _Установка { get; }
        public string _Элемент_на_щите { get; }
        public int _Din { get; }
        public int _Dout { get; }
        public int _Ain { get; }
        public int _Aout { get; }
        public int _HMI { get; }
        public int _Interface { get; }

        public Точка(СекцияБазыДанных cекция, FamilyInstance familyInstance)
        {
            Cекция = cекция;
            FamilySymbol = familyInstance.Symbol as FamilySymbol;
            Location = (familyInstance.Location as LocationPoint).Point - Cекция.Origin;
            NS_ElementId = familyInstance.LookupParameter("NS_ElementId").AsString();
            _ГОСТ212008 = familyInstance.LookupParameter("_ГОСТ 21.208").AsString(); 
            _Принцип = familyInstance.LookupParameter("_Принцип").AsString();
            _Типизация = familyInstance.LookupParameter("_Типизация").AsString();
            _Установка = familyInstance.LookupParameter("_Установка").AsString();
            _Элемент_на_щите = familyInstance.LookupParameter("_Элемент_на_щите").AsString();
            _Din = familyInstance.LookupParameter("_Din").AsInteger();
            _Dout = familyInstance.LookupParameter("_Dout").AsInteger();
            _Ain = familyInstance.LookupParameter("_Ain").AsInteger();
            _Aout = familyInstance.LookupParameter("_Aout").AsInteger();
            _HMI = familyInstance.LookupParameter("_HMI").AsInteger();
            _Interface = familyInstance.LookupParameter("_Interface").AsInteger();

            /*
            //Создаем экземпляр в футоре, каждый раз сдвигая на footer.StepBehindElementsInFooter;
            var fiElement = doc.Create.NewFamilyInstance(xyz,
              new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).First(q => q.Name == dd.NameTypeElement) as FamilySymbol,
              viewDrafting);
            */
        }
    }
    public class Марка
    {
        public СекцияБазыДанных Cекция { get; }
        public IndependentTag Host { get; }
        public XYZ LocationHeader { get; }
        public XYZ LocationLeaderElbow { get; }
        public bool HasLeader { get; }
        //public bool IsLeaderVisible { get; }
        public bool HasElbow { get; }
       
        public Element Element { get;}

        public FamilySymbol FamilySymbol { get; set; }
        //public ElementId SymId { get; set; }
        internal Марка(СекцияБазыДанных секция, IndependentTag host , Element e , Document document)
        {
            Element = e;
            FamilySymbol = host.GetTypeId().ToElement(document) as FamilySymbol;
            /*
            SymId = new FilteredElementCollector(Context.ActiveDocument).
                            OfCategory(BuiltInCategory.OST_DetailComponentTags).
                            WhereElementIsElementType().
                            ToList().Where(i => i.Name == FamilySymbol.Name).FirstOrDefault().Id;

            */
            Cекция = секция;
            Host = host;
            LocationHeader = host.TagHeadPosition - Cекция.Origin;

#if REVIT2023_OR_GREATER
            
            if (host.HasLeader)
            {
                
                Reference linkedElementRef = new Reference(Element);
                bool hasElbowHelper = host.HasLeaderElbow(linkedElementRef);
                HasLeader = host.HasLeader;
                if (hasElbowHelper)
                {
                    HasElbow = hasElbowHelper;
                }
                else HasElbow = false;

            }
            else { HasLeader = false;
                
            }



            if (HasElbow)
            {
                Reference linkedElementRef = new Reference(Element);
                var xyzLeaderElbow = host.GetLeaderElbow(linkedElementRef);
                LocationLeaderElbow = xyzLeaderElbow - Cекция.Origin;
            }


#elif REVIT2022
            if (host.HasLeader)
            {

                Reference linkedElementRef = new Reference(Element);
                bool hasElbowHelper = host.HasLeaderElbow(linkedElementRef);
                HasLeader = host.HasLeader;
                if (hasElbowHelper)
                {
                    HasElbow = hasElbowHelper;
                }
                else HasElbow = false;

            }
            else
            {
                HasLeader = false;

            }

            if (HasElbow)
            {
                Reference linkedElementRef = new Reference(Element);
                var xyzLeaderElbow = host.GetLeaderElbow(linkedElementRef);
                LocationLeaderElbow = xyzLeaderElbow - Cекция.Origin;
            }


#else

            if (host.HasLeader)
            {
                HasLeader = host.HasLeader;
                if (host.HasElbow)
                {
                    HasElbow = host.HasElbow;
                }
                else HasElbow = false;

            }
            else { HasLeader = false; }



            if (HasElbow)
            {
                LocationLeaderElbow = host.LeaderElbow - Cекция.Origin;
            }
#endif

        }
    }

    public class Группа
    {
        public СекцияБазыДанных Cекция { get; }
        public XYZ Location { get;}
        public Group Group { get; set; }

        //Document.Create.PlaceGroup()
        public Группа(СекцияБазыДанных секция, Group group)
        {
            Group = group;
            Cекция = секция;
            Location = (group.Location as LocationPoint).Point - Cекция.Origin;
        }

    }

    public class Текст
    {
       public XYZ BaseDirection { get; }
        public XYZ Coord { get; }
        public TextNote TextNote { get; }
        public Текст(СекцияБазыДанных секция, TextNote textnote)
        {
            BaseDirection = textnote.BaseDirection;
            Coord = textnote.Coord - секция.Origin;
            TextNote = textnote;
        }
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
        public List<Группа> Группы { get; set; } = [];
        public List<Точка> Точки { get; set; } = [];
        public List<Линия> Линии { get; set; } = [];
        public List<Текст> Texts { get; set; } = [];
        
        public List<Оборудование> Оборудование { get; set; } = [];
        
        //public List<Element> Elements { get; }
        //public List<ElementId> ElementsIds { get;}
        public СекцияБазыДанных(FilledRegion filledRegion , View view)
        {
            FilledRegion = filledRegion;
            //Elements = elements;
            //ElementsIds = Elements.ConvertAll<ElementId>(x=>x.Id);
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

        public void MarkaMethod( Марка? m, Document doc, View new_view, FamilyInstance fi, XYZ xyz)
        {
            if (m == null) return;
            Reference elRef = new Reference(fi);
            IndependentTag it = IndependentTag.Create(doc, new_view.Id, elRef, true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, m.LocationHeader + xyz);
            it.ChangeTypeId(m.FamilySymbol.Id);
            it.TagHeadPosition = m.LocationHeader + xyz;

                
#if REVIT2023_OR_GREATER

                                if (m.HasLeader)
                                {
                                    if (m.HasElbow) {
                                        Reference referensLEader = new Reference(fi);
                                       // it.LeaderEndCondition = LeaderEndCondition.Free;
                                        it.GetTaggedReferences().Add(referensLEader);
                                        it.SetLeaderElbow(referensLEader, m.LocationLeaderElbow + xyz);
                                       //it.SetIsLeaderVisible(referensLEader, true);
                                    } // Важен порядок комманд
                                    
                                }
                                else { it.HasLeader = false; }

#elif REVIT2022
                                if (m.HasLeader)
                                {
                                    if (m.HasElbow)
                                    {
                                        Reference referensLEader = new Reference(fi);
                                        // it.LeaderEndCondition = LeaderEndCondition.Free;
                                        it.GetTaggedReferences().Add(referensLEader);
                                        it.SetLeaderElbow(referensLEader, m.LocationLeaderElbow + xyz);
                                        //it.SetIsLeaderVisible(referensLEader, true);
                                    } // Важен порядок комманд

                                }
                                else { it.HasLeader = false; }



#else
                                if (m.HasLeader)
                                {
                                    if (m.HasElbow) { it.LeaderElbow = m.LocationLeaderElbow + xyz;} // Важен порядок комманд
                                }
                                else { it.HasLeader = false; }
                                    
#endif
        }
    
    }
    public class БазаДанных
    {
        public List<СекцияБазыДанных> Секции { get; set; } = [];

    }

    
