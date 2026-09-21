using System.Net;
using Autodesk.Revit.UI;

namespace NGraph.Core.FunctionalScheme;

public class FSAmodelCreateFromDb
{


    public FSAmodelCreateFromDb()
    {

    }


    public Element? GetBDfromModel(Document doc, string NameInBD)
    {
        Helpers helpers = new Helpers();
        var view = helpers.AllElementsOfCategory(doc, BuiltInCategory.OST_Views).Where(x => x.Name == NameInBD)
            .FirstOrDefault();
        return view;

    }
    
    /// <summary>
    ///  Чтение с чертежного вида элементов из региона с заливкой название "BD_" (на выходе пустые элементы СекцияБазыДанных в списке)
    /// </summary>
    /// <param name="Document"></param>
    /// <param name="view"></param>
    /// <returns></returns>
    public static List<СекцияБазыДанных> CreateFromBd_ReadFilledRegion(Document Document, View? view)
    {
        List < СекцияБазыДанных > sections = [];
            
        if (view == null)
        {
            TaskDialog.Show("Внимание", "Отсутствует чертежный вид базы элементов.");
            return sections; 
        }
        var elements = Document.GetElements(view.Id);
            
        var filledRegions = elements
            .OfType<FilledRegion>()
            .Where(region => NgContext._FindParameter(region, "Тип")?.AsValueString()?.Contains("BD_") == true)
            .ToList();
            
        foreach (var VARIABLE in filledRegions)
        {
            sections.Add(new СекцияБазыДанных(VARIABLE, view));
        }
            
        return sections;
    }

    /// <summary>
    /// Заполнение СекцияБазыДанных по ссылке
    /// </summary>
    /// <param name="document"></param>
    /// <param name="view"></param>
    /// <param name="section"></param>
    public static void CreateFromBd_AddToSectionOtherElements(Document document, View? view,
        ref СекцияБазыДанных section)
    {
        //Все элементы с чертежного вида

        if (view is null)
        {
            return;
        }

        var elements = document.GetElements(view.Id);



        //Элементы из бызы отфильтрованные по нужным категориям
        var filteredElements = elements
            .Where(x => x.GetType() == typeof(FamilyInstance)
                        || (x.GetType() == typeof(Group) &&
                            x.Location != null) //Принадлежит группе и эта группа не входит в другую
                        || x.GetType() == typeof(DetailLine)
                        || x.GetType() == typeof(TextNote)

            ).ToList();
     
        var hiddenElements = new FilteredElementCollector(document)
            .OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
            .Where(x => x.IsHidden(view)).Select(x => x).ToList();
/*
        if (hiddenElements.Count != 0)
        */
        filteredElements.AddRange(hiddenElements);

    
            
        var elementsTag = document.GetElements(view.Id)
            
            .Where(x => 
                x.GetType() == typeof(IndependentTag)
            );
            
        List<Element> lelements = []; //Список элементов для области

                

        var bb = section.FilledRegion.get_BoundingBox(view);
                
        foreach ( var e in filteredElements)
        {
            try
            {
                if ((e.Location as LocationPoint) != null && (Helpers.Contains(bb, (e.Location as LocationPoint).Point, false))) //У групп и экземпляров семейств не должно быть нулевых локаций
                {
                    if (e.GetType() == typeof(FamilyInstance))
                    {
                        if (e.IsHidden(view))
                        {
                            lelements.Add(e); //Оборудование
                            var оборудование = new Оборудование(section, e as FamilyInstance);
                            section.Оборудование.Add(оборудование);
                           
                            foreach (var t in elementsTag)
                            {
                                var tag = t as IndependentTag;

#if REVIT2024_OR_GREATER
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId.Value==(e.Id.Value)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        оборудование.Марка = new Марка(section, tag , e, document);

                                    }
#elif REVIT2023
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId == (e.Id)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        оборудование.Марка = new Марка(section, tag, e, document);

                                    }



#else

                                if (tag.TaggedElementId.HostElementId.IntegerValue == e.Id.IntegerValue) //Если метка принадлежит элементу (находим хост)
                                {
                                    lelements.Add(tag);

                                    оборудование.Марка = new Марка(section, tag , e, document);

                                }
#endif
                            }
                            
                        }
                        else
                        {
                            lelements.Add(e); //Зеленая точка
                            Точка точка = new(section, e as FamilyInstance);
                            section.Точки.Add(точка);
                            foreach (var t in elementsTag)
                            {
                                var tag = t as IndependentTag;

#if REVIT2024_OR_GREATER
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId.Value==(e.Id.Value)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        точка.Марка = new Марка(section, tag , e, document);

                                    }
#elif REVIT2023
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId == (e.Id)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        точка.Марка = new Марка(section, tag, e, document);

                                    }



#else

                                if (tag.TaggedElementId.HostElementId.IntegerValue == e.Id.IntegerValue) //Если метка принадлежит элементу (находим хост)
                                {
                                    lelements.Add(tag);

                                    точка.Марка = new Марка(section, tag , e, document);

                                }
#endif
                            }
                                    
                        }
                                
                    }
                    else if (e.GetType() == typeof(Group))
                    {
                        lelements.Add(e);
                        Группа группа = new(section, e as Group);
                        section.Группы.Add(группа);

                    }

                }
                else if (e.GetType() == typeof(DetailLine) && Helpers.Contains(bb,e.get_BoundingBox(view),false)) //bb.Contains(e.get_BoundingBox(view))
                {
#if REVIT2026_OR_GREATER
                            if(e.GroupId.Value == -1)
#else                         
                    if(e.GroupId.IntegerValue == -1)
#endif
                    {
                        lelements.Add(e);
                        Линия линия = new(section, e as DetailLine);
                        section.Линии.Add(линия);
                    }

                    /*
                    try { var name = (e.GroupId.ToElement(Document) as Group).Name; }
                    catch
                    {
                        lelements.Add(e);
                        Линия линия = new(секцияБазыДанных, e as DetailLine);
                        секцияБазыДанных.Линии.Add(линия);
                    }
                    */
                }
                else if (e.GetType() == typeof(TextNote) && Helpers.Contains(bb,e.get_BoundingBox(view),false)) //bb.Contains(e.get_BoundingBox(view))
                {
                            
#if REVIT2026_OR_GREATER
                            if(e.GroupId.Value == -1)
#else                         
                    if(e.GroupId.IntegerValue == -1)
#endif
                    {
                        lelements.Add(e);
                        Текст текст = new Текст(section, e as TextNote);
                        section.Texts.Add(текст);
                    }
                            

                }
                        

            }
            catch { }
                    
        }

                
        }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="newBD"></param>
    /// <param name="new_view"></param>
    /// <param name="ИмяУстановки"></param>
    /// <param name="xyz"></param>
    public static void CreateFromBd_withoutTransaction(Document doc, ref СекцияБазыДанных section, View new_view, string ИмяУстановки, XYZ xyz )
    {

        
        
        var mass = new List<ElementId>();
                            foreach (var o in section.Оборудование)  //Группы
                            {
                                
                                FamilyInstance fi = doc.Create.NewFamilyInstance(o.Location + xyz, o.FamilySymbol, new_view);
                                fi.LookupParameter("Габарит_высота").Set(o.Габарит_высота);
                                fi.LookupParameter("Габарит_ширина").Set(o.Габарит_ширина);
                                fi.LookupParameter("ADSK_Наименование").Set(o.Наименование);
                                fi.LookupParameter("ADSK_Наименование краткое").Set(o.НаименованиеКраткое);
                                fi.LookupParameter("ADSK_Позиция").Set(o.Позиция);
                                fi.LookupParameter("ADSK_Примечание").Set(o.Примечание);
                                
                                
                                fi.LookupParameter("_ГОСТ 21.208").Set(o._ГОСТ212008);
                                fi.LookupParameter("_Принцип").Set(o._Принцип);
                                fi.LookupParameter("_Типизация").Set(o._Типизация);
                                //fi.LookupParameter("_Установка").Set(t._Установка);
                                fi.LookupParameter("_Установка").Set(ИмяУстановки);
                                fi.LookupParameter("_Элемент_на_щите").Set(o._Элемент_на_щите);
                                fi.LookupParameter("CJ_Рабочий набор").Set(new_view.Name);
                                fi.LookupParameter("NS_ElementId").Set(o.NS_ElementId);//Здесь находится ссылка на ID оборудования
                                fi.LookupParameter("_Din").Set(o._Din);
                                fi.LookupParameter("_Dout").Set(o._Dout);
                                fi.LookupParameter("_Ain").Set(o._Ain);
                                fi.LookupParameter("_Aout").Set(o._Aout);
                                fi.LookupParameter("_HMI").Set(o._HMI);
                                fi.LookupParameter("_Interface").Set(o._Interface);
                                
                                section.MarkaMethod(o.Марка, doc, new_view, fi, xyz);
                                
                                mass.Add(fi.Id);
                                
                            }
                            if (mass.Count>0){new_view.HideElements(mass);}
                            
                            foreach (var t in section.Точки)  //Точки и марки
                            {
                                FamilyInstance fi = doc.Create.NewFamilyInstance(t.Location + xyz, t.FamilySymbol, new_view);
                                
                                fi.LookupParameter("_ГОСТ 21.208").Set(t._ГОСТ212008);
                                fi.LookupParameter("_Принцип").Set(t._Принцип);
                                fi.LookupParameter("_Типизация").Set(t._Типизация);
                                //fi.LookupParameter("_Установка").Set(t._Установка);
                                fi.LookupParameter("_Установка").Set(ИмяУстановки);
                                fi.LookupParameter("_Элемент_на_щите").Set(t._Элемент_на_щите);
                                fi.LookupParameter("CJ_Рабочий набор").Set(new_view.Name);
                                fi.LookupParameter("NS_ElementId").Set(t.NS_ElementId);//Здесь находится ссылка на ID оборудования
                                fi.LookupParameter("_Din").Set(t._Din);
                                fi.LookupParameter("_Dout").Set(t._Dout);
                                fi.LookupParameter("_Ain").Set(t._Ain);
                                fi.LookupParameter("_Aout").Set(t._Aout);
                                fi.LookupParameter("_HMI").Set(t._HMI);
                                fi.LookupParameter("_Interface").Set(t._Interface);

                                section.MarkaMethod(t.Марка, doc, new_view, fi, xyz);






                            }
                            foreach (var g in section.Группы)  //Группы
                            {
                                Group group = doc.Create.PlaceGroup(g.Location + xyz, g.Group.GroupType);
                            }

                            foreach (var l in section.Линии)  //Линии
                            {
                                var line = Autodesk.Revit.DB.Line.CreateBound(l.Begin + xyz, l.End + xyz);
                                DetailCurve dc = doc.Create.NewDetailCurve(new_view, line); dc.LineStyle = l.GraphicsStyle;
                            }
                            
                            foreach (var t in section.Texts)  //Текст
                            {
                                
                                TextNoteOptions textNoteOptions = new TextNoteOptions(t.TextNote.TextNoteType.Id);

                                textNoteOptions.Rotation = t.TextNote.BaseDirection.AngleTo(new XYZ(1,0,0));
                                textNoteOptions.HorizontalAlignment = t.TextNote.HorizontalAlignment;
                                textNoteOptions.VerticalAlignment = t.TextNote.VerticalAlignment;
                                TextNote textNote = Autodesk.Revit.DB.TextNote.Create(doc, new_view.Id, t.Coord+xyz, t.TextNote.GetFormattedText().GetPlainText(), textNoteOptions);
                                textNote.SetFormattedText(t.TextNote.GetFormattedText());

                            }
                            
                           // xyz = xyz + new XYZ(0.3, 0.2, 0);
                        
    }
    
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
        public FilledRegion FilledRegion { get;}
        public string Name { get;}
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
            Name = filledRegion.LookupParameter("ADSK_Примечание").AsString();
            Type = filledRegion.LookupParameter("Тип").AsValueString();
            var bb = filledRegion.get_BoundingBox(view);
            Габариты = bb.Max - bb.Min;
            Центр = Габариты / 2;
            Origin = bb.Min;
        }

        public void MarkaMethod( Марка m, Document doc, View new_view, FamilyInstance fi, XYZ xyz)
        {
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

    
