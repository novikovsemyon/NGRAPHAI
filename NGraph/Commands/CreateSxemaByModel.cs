using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Views;
using NGraph.ViewModels;
using NGraph.Core.FunctionalScheme;
using Nice3point.Revit.Toolkit.External;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
namespace NGraph.Commands;

    /// <summary>
    ///     Создание чертежного вида на основании модели. Группирование элементов по параметрам (Рабочий набор/ADSK_Группирование/Уровень/Помещение/ADSK_Позиция/)
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class CreateSxemaByModel : ExternalCommand
    {
        /// <summary>
        /// Список секций в модели на основании оборудования и его размещения по этажам
        /// </summary>
        List<Section> Sections = [];

        ///
        readonly double koefficient = 2; //Коэффициент расстояния между элементами внутри помещения
        readonly double rooms_koefficient = 25 / 304.8; //Расстояние между помещениями
        readonly double levels_koefficient = 25 / 304.8; //Расстояние между уровнями
        readonly double sections_koefficient = 70 / 304.8; //Расстояние между секциями
        ///
        /// <summary>
        /// Начало чертежа
        /// </summary>
        XYZ BeginSections = new XYZ();
        static readonly XYZ A1 = new XYZ(584/304.8, 816 / 304.8, 0); //Размеры бумаги А1 вертикальный внутренний

        public override void Execute()
        {
            Helpers helpers = new Helpers();


            ///Выбираем по какому параметру и значению будет строиться схема
            ///
            var viewModel = new NGraphCreateSxemaByModelViewModel();
            viewModel.Doc = Document;
            var viewWindow = new NGraphCreateSxemaByModelView(viewModel);
            viewWindow.ShowDialog();

            var wpfParametr = viewWindow.CB_Param.Text;
            var wpfParametrValue = viewWindow.CB_Value.Text;

            if ((bool)viewWindow.Cancel == true)
            { return; }
            //Space
            //Parameter

                //Получаем элементы электрооборудование в модели на основании их параметров (Рабочий набор / ADSK_Группирование /

#if REVIT2020

            var elementsFromModel = helpers.AllElementsOfCategory(Document, BuiltInCategory.OST_ElectricalEquipment)

                // .Where(i => 
                //  (i.FindParameter("ADSK_Группирование").AsString().Contains("АК_"))).ToList()
                .Where(j =>
                (j.LookupParameter(wpfParametr).AsString() == wpfParametrValue)
                )
                ;

                
#else

                var elementsFromModel = helpers.AllElementsOfCategory(Document, BuiltInCategory.OST_ElectricalEquipment)

                .Where(
                (i =>
                (NgContext._FindParameter(i,"ADSK_Группирование").AsString().Contains("АК_"))
                &&
                (NgContext._FindParameter(i,wpfParametr).AsString()==wpfParametrValue)
                ));

#endif

                //Создаем по оборудованию из модели, корпус, этаж, помещение (Заполняем поле Section)
                foreach (var s in elementsFromModel.GroupBy(i => NgContext._FindParameter((i as FamilyInstance),"ADSK_Номер секции").AsString()))//По каждой секции
                {
                    Section section = new(s.Key);
                    Sections.Add(section);
                    //Группируем по этажу
#if REVIT2026_OR_GREATER
                     var groupByLevel = s.GroupBy(i => (i.LevelId.Value)); //Группируем по ID уровня
#else
                    var groupByLevel = s.GroupBy(i => (i.LevelId.IntegerValue)); //Группируем по ID уровня
#endif
                    
                   

                    foreach (var e in groupByLevel)
                    {
                        ElementId elementId = new ElementId(e.Key);
                        Element level_revit = Document.GetElement(elementId);
                        LevelOfSection level = new(section, level_revit as Level);
                        section.Levels.Add(level);
                        //Группируем по помещению (Пространству)
#if REVIT2026_OR_GREATER
                        var groupBySpace = e.GroupBy(i => (i as FamilyInstance).Space.Id.Value);
#else
                   var groupBySpace = e.GroupBy(i => (i as FamilyInstance).Space.Id.IntegerValue);
#endif
                       
                        List<EqupmentModel> equpments_on_level = [];
                        foreach (var sp in groupBySpace)
                        {
                            ElementId elementId_space = new ElementId(sp.Key);
                            Element space = Document.GetElement(elementId_space);
                            RoomSpace room = new(level, space as Space);
                            level.RoomSpaces.Add(room);
                            foreach (var eq in sp)
                            {
                                FamilyInstance fi = eq as FamilyInstance;
                                //Создаем оборудование в помещении
                                room.EqupmentModels.Add(new(fi, room));
                            }
                            level.EqupmentModels.AddRange(room.EqupmentModels); //Добавляем оборудование на этаж
                        }
                        section.EqupmentModels.AddRange(level.EqupmentModels); //Добавляем оборудование на корпус
                    }
                }













                //Создаем чертежный вид и их аннотативные обозначения (элементы узлов)
                ViewDrafting view = helpers.CreateViewDrafting(Document, "Структурная схема", true, UiDocument);
                //Получаем все оборудование в модели
                var equipmentModels = GetEqupmentModels(Sections);
                //Обрабатываем модель
                foreach (var eq_m in equipmentModels)
                {
                    //Считываем ini файл с параметрами для определения символа
                    //Добавляем FamilySymbol
                    eq_m.EQipment(eq_m, true);
                    //Добавляем модель оборудования для чертежного виды (без создания семейства)
                    eq_m.EqupmentDrafting = new EqupmentDrafting(Document, eq_m, view);
                }




            /*
            ////////rev2

            //Определяем размеры помещений и назначаем позиции XYZ относительно помещений для экземпляров семейств.
            var roomSpaces = GetRoomSpaces(Sections);
            foreach (var rs in roomSpaces)
            {

                rs.Length = 0;
                rs.Height = 0;

                var groupbyPiozition = rs.EqupmentModels.GroupBy(i => i.ADSK_Позиция);//Группируем элементы по позиции

                foreach (var g in groupbyPiozition)
                {
                    int i_em = 0;
                    foreach (var em in g)
                    {
                        em.EqupmentDrafting.XYZ_ByHost = new XYZ((em.EqupmentDrafting.Length * i_em * koefficient), rs.Height, 0);//Координаты относительно помещения
                                                                                                                                  //С каждым новым элементом растем вправо
                        i_em++;

                    }
                    rs.Height = rs.Height + g.FirstOrDefault().EqupmentDrafting.Height * koefficient; //Обновляем высоту помещения. Высота в группе по позиции одинакова
                    double l_group = g.Sum(i => i.EqupmentDrafting.Length) * koefficient;
                    if (rs.Length < l_group) { rs.Length = l_group; } //Длина помещения - максимальная длина в группе
                }


            }
            */


            ////////rev3 с учетом нумерации

            //Определяем размеры помещений и назначаем позиции XYZ относительно помещений для экземпляров семейств.
            var roomSpaces = GetRoomSpaces(Sections);
            foreach (var rs in roomSpaces)
            {

                rs.Length = 0;
                rs.Height = 0;

                var groupbyPiozition = rs.EqupmentModels.GroupBy(i => i.ADSK_Позиция);//Группируем элементы по позиции

                foreach (var g in groupbyPiozition)
                {
                    int i_em = 0;


                    var sortedElemensInGroupByIndex = g.OrderBy(g => g.ПорядковыйНомер).ToList(); //rev3 Сортируем по индексу (имя панели)

                    foreach (var em in sortedElemensInGroupByIndex)
                    {
                        em.EqupmentDrafting.XYZ_ByHost = new XYZ((em.EqupmentDrafting.Length * i_em * koefficient), rs.Height, 0);//Координаты относительно помещения
                                                                                                                                  //С каждым новым элементом растем вправо
                        i_em++;

                    }
                    rs.Height = rs.Height + g.FirstOrDefault().EqupmentDrafting.Height * koefficient; //Обновляем высоту помещения. Высота в группе по позиции одинакова
                    double l_group = g.Sum(i => i.EqupmentDrafting.Length) * koefficient;
                    if (rs.Length < l_group) { rs.Length = l_group; } //Длина помещения - максимальная длина в группе
                }


            }













            /////////////////////////////////////////

            //Определяем размеры уровней в зависимости от помещений и назначаем координаты помещениям относительно уровня
            var levels = GetLevels(Sections);
                foreach (var level in levels)
                {
                    level.Length = 0;
                    level.Height = level.RoomSpaces.Max(i => i.Height); //Высота уровня  - самое высокое помещение

                    foreach (var rs in level.RoomSpaces)
                    {
                        rs.XYZ_ByHost = new XYZ(level.Length, 0, 0);//Координаты относительно уровня
                                                                    //С каждым новым элементом растем вправо
                        level.Length = level.Length + rs.Length + rooms_koefficient; //Обновляем длину уровня
                    }


                }
                /////////////////////////////////////////////

                //Определяем размеры секций и назначаем позиции уровням относительно секции.
                double delta = 0;
                foreach (var s in Sections)
                {
                    //Находим максимальную ширину уровня - это ширина секции
                    s.Length = s.Levels.Max(i => i.Length);


                    //Сортируем уровни относительно их реального размещения в модели
                    var sortedLevels = s.Levels.OrderBy(i => i.Elevation);


                    foreach (var l in sortedLevels)
                    {
                        l.XYZ_ByHost = new XYZ(0, s.Height, 0);
                        s.Height = s.Height + l.Height + levels_koefficient;

                    }

                    s.XYZ_ByHost = new XYZ(delta, BeginSections.Y, BeginSections.Z);
                    delta = delta + s.Length + sections_koefficient;
                    //sections_begin = sections_begin + s.Length + sections_koefficient;


                }
                /////////////////////////////////////////////////
                //Обновляем координаты для всех элементов
                BoxXYZ.Reload(Sections);

                /////////////////////////////////////////////////

                //Расмещаем семейства
                using (Transaction tx = new Transaction(Document, "Размещение семейств"))
                {
                    tx.Start("Размещение семейств");

                    var filter = new FilteredElementCollector(Document).OfClass(typeof(TextNoteType));
                    foreach (var s in Sections)
                    {
                        CreateLineBox(s.XYZ_LineConturMin, s.XYZ_LineConturMax, view, [true, false, true, false]);



                        TextNoteType txtNT_section = filter.First(q => q.Name.Equals("ГОСТ тип А h=5 c=3.5")) as TextNoteType;
                        TextNoteOptions textNoteOptions_section = new TextNoteOptions(txtNT_section.Id);
                        TextNote.Create(Document, view.Id, s.XYZ_LineConturMin, 40 / 304.8, s.Name, textNoteOptions_section);

                        foreach (var l in s.Levels)
                        {
                            CreateLineBox(l.XYZ_LineConturMin, l.XYZ_LineConturMax, view, [false, false, false, true]);

                            var elevation_round = Helpers.LenghtInt(l.Elevation, 10); //Округление отметки

                            TextNoteType txtNT_level = filter.First(q => q.Name.Equals("ГОСТ тип А h=3.5 c=2.5")) as TextNoteType;
                            TextNoteOptions textNoteOptions_level = new TextNoteOptions(txtNT_level.Id);
                            textNoteOptions_level.Rotation = 3.14 / 2;
                            TextNote.Create(Document, view.Id, l.XYZ_LineConturMin + new XYZ(-5 / 304.8, 5 / 304.8, 0), 60 / 304.8, l.Name + " (отм. " + elevation_round.ToString() + ")", textNoteOptions_level);

                            foreach (var r in l.RoomSpaces)
                            {
                                CreateLineBox(r.XYZ_LineConturMin, r.XYZ_LineConturMax, view, [true, true, true, true]);

                                TextNoteType txtNT_room = filter.First(q => q.Name.Equals("ГОСТ тип А h=2 c=1.4")) as TextNoteType;
                                TextNoteOptions textNoteOptions_room = new TextNoteOptions(txtNT_room.Id);
                                TextNote.Create(Document, view.Id, r.XYZ_LineConturMin + new XYZ(0, 5 / 304.8, 0), 40 / 304.8, r.Name + "  (пом." + r.Number.ToString() + ")", textNoteOptions_room);


                            



                            foreach (var e in r.EqupmentModels)
                                {
                                    e.EqupmentDrafting.FI = Document.Create.NewFamilyInstance(e.EqupmentDrafting.XYZ_inView, e.EqupmentDrafting.FamilySymbol, view);

                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_NS_ElementId).Set(e.NS_ElementID); //Записываем соответстви ElementId
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_ADSK_Position).Set(e.ADSK_Позиция); //Записываем соответстви ADSK_Позиция
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_ADSK_Group).Set(e.ADSK_Группирование); //Записываем соответстви ADSK_Группирование
                                    e.EqupmentDrafting.FI.LookupParameter("NS_Имя панели").Set(e.Имя_панели);
                                    e.EqupmentDrafting.FI.LookupParameter("NS_Текст УГО").Set(e.ADSK_Позиция);
                                    e.EqupmentDrafting.FI.LookupParameter("ADSK_Наименование краткое").Set(e.ADSK_Наименование_краткое);
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_Work).Set(view.Name);

                                    //Обнуляем второстепенные значения
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_Begin).Set(" ");
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_End).Set(" ");
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_Begin_eq).Set(" ");
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_End_eq).Set(" ");
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_Number).Set(" ");
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_Mark).Set(" ");
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_Nets).Set(" ");
                                    e.EqupmentDrafting.FI.LookupParameter(Const.Param_CJ_Lenght).Set((int)1);



                                }
                            }
                        }

                    }


                    tx.Commit();
                }






                ///////////////////
                ///функции
                ///

                /// <summary>
                /// Создание прямоугольника
                /// </summary>
                /// <param name="doc"></param>
                /// <param name="XYZ_begin"></param>
                /// <param name="XYZ_end"></param>
                void CreateLineBox(XYZ XYZ_begin, XYZ XYZ_end, View viewDrafting, bool[] bools)
                {
                    GraphicsStyle _gstyle = CreateLineStyle("grey_1_штрих", 1, new Autodesk.Revit.DB.Color(128, 128, 128), BlockDiagram.TypeOfLine.Штрих);
                    XYZ P1 = XYZ_begin;
                    XYZ P2 = new XYZ(XYZ_begin.X, XYZ_end.Y, 0);
                    XYZ P3 = XYZ_end;
                    XYZ P4 = new XYZ(XYZ_end.X, XYZ_begin.Y, 0);
                    if (bools[0])
                    { Line line1 = Line.CreateBound(P1, P2); DetailCurve dc1 = Document.Create.NewDetailCurve(viewDrafting, line1); dc1.LineStyle = _gstyle; }
                    if (bools[1])
                    { Line line2 = Line.CreateBound(P2, P3); DetailCurve dc2 = Document.Create.NewDetailCurve(viewDrafting, line2); dc2.LineStyle = _gstyle; }
                    if (bools[2])
                    { Line line3 = Line.CreateBound(P3, P4); DetailCurve dc3 = Document.Create.NewDetailCurve(viewDrafting, line3); dc3.LineStyle = _gstyle; }
                    if (bools[3])
                    { Line line4 = Line.CreateBound(P4, P1); DetailCurve dc4 = Document.Create.NewDetailCurve(viewDrafting, line4); dc4.LineStyle = _gstyle; }
                }


                /// <summary>
                /// Создание стиля линии, если такого не существует.
                /// </summary>
                /// <param name="doc"></param>
                /// <param name="name"></param>
                /// <param name="weight"></param>
                /// <param name="color"></param>
                /// <param name="patternLineNAME"></param>
                /// <returns></returns>
                GraphicsStyle CreateLineStyle(string name, int weight, Autodesk.Revit.DB.Color color, BlockDiagram.TypeOfLine patternLineNAME)
                {
                    Categories categories = Document.Settings.Categories;
                    Category lineCategories = categories.get_Item(BuiltInCategory.OST_Lines);
                    CategoryNameMap lineStyleSubTypes = lineCategories.SubCategories;

                    //Проверяем существуют ли линии и если нет, то создаем их
                    if (lineStyleSubTypes.Contains(name))
                        return lineStyleSubTypes.get_Item(name).GetGraphicsStyle(GraphicsStyleType.Projection);
                    else
                    {
                        Category cat = categories.NewSubcategory(lineCategories, name);
                        cat.SetLineWeight(weight, GraphicsStyleType.Projection);
                        cat.LineColor = color;
                        if (patternLineNAME == BlockDiagram.TypeOfLine.Штрих)
                        {
                            var patternLine = LinePatternElement.GetLinePatternElementByName(Document, patternLineNAME.ToString()).Id;
                            cat.SetLinePatternId(patternLine, GraphicsStyleType.Projection);

                        }




                        return cat.GetGraphicsStyle(GraphicsStyleType.Projection);
                    }
                }

                static List<EqupmentModel> GetEqupmentModels(List<Section> sections)
                {
                    List<EqupmentModel> eq = [];
                    foreach (var i in sections) { eq.AddRange(i.EqupmentModels); }
                    return eq;
                }
                static List<RoomSpace> GetRoomSpaces(List<Section> sections)
                {
                    List<RoomSpace> rs = [];
                    foreach (var i in sections)
                    { foreach (var l in i.Levels) { rs.AddRange(l.RoomSpaces); } }
                    return rs;
                }
                static List<LevelOfSection> GetLevels(List<Section> sections)
                {
                    List<LevelOfSection> levels = [];
                    foreach (var i in sections) { levels.AddRange(i.Levels); }
                    return levels;
                }
            }
        
       
       






        /// <summary>
        /// Представление оборудования на чертеже
        /// </summary>
        class EqupmentDrafting: BoxXYZ
        {
            
           // public XYZ PositionInSpace { get; set; }
            public EqupmentModel EqupmentModel { get; }
            public FamilySymbol FamilySymbol { get; }
            internal ViewDrafting ViewDrafting { get;}
           // internal XYZ GabaritFamilySymbol { get;}
            internal FamilyInstance FI { get; set; }
            internal EqupmentDrafting(Autodesk.Revit.DB.Document doc, EqupmentModel model, ViewDrafting VD)
            {
                EqupmentModel = model;
                ViewDrafting = VD;
                FamilySymbol = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_DetailComponents).First(q => q.Name == model.NameSymbol_EqupmentDrafting) as FamilySymbol;

                double Xmax = FamilySymbol.get_BoundingBox(ViewDrafting).Max.X;
                double Xmin = FamilySymbol.get_BoundingBox(ViewDrafting).Min.X;
                double Ymax = FamilySymbol.get_BoundingBox(ViewDrafting).Max.Y;
                double Ymin = FamilySymbol.get_BoundingBox(ViewDrafting).Min.Y;
                double dX = Xmax - Xmin;
                double dY = Ymax - Ymin;
                XYZ_Gabarit = new XYZ(dX, dY, 0); //Учитываем на чертежном виде масштаб 1 к 1
                Length = dX;
                Height = dY;

            }
        }

        /// <summary>
        /// Представление оборудования в модели
        /// </summary>
        class EqupmentModel
        {
            internal string NameSymbol_EqupmentDrafting { get; set; }
            internal EqupmentDrafting EqupmentDrafting { get; set; }

            
            internal string ADSK_Группирование { get; }
            internal string ADSK_Позиция { get; }
            internal string NS_ElementID { get; }
            internal string Имя_панели { get; }

            internal string ПорядковыйНомер { get; }

            internal string ADSK_Наименование_краткое { get; }
            internal RoomSpace RoomSpace { get; }
            internal FamilyInstance FI { get;}
            internal EqupmentModel(FamilyInstance fi, RoomSpace p)
            {
                FI = fi;
                RoomSpace = p;
                ADSK_Позиция = NgContext._FindParameter(fi,"ADSK_Позиция").AsString();
                ADSK_Группирование = NgContext._FindParameter(fi,"ADSK_Группирование").AsString();
                NS_ElementID = fi.Id.ToString();
                Имя_панели = NgContext._FindParameter(fi, "Имя панели").AsString();
                ADSK_Наименование_краткое = NgContext._FindParameter(fi,"ADSK_Наименование краткое").AsString();

                ПорядковыйНомер = NgContext._FindParameter(fi,"Марка").AsString();

            }














            /// <summary>
            /// Получаем настройки из {mydocumentsPath}\REVIT\BETA-BIM\Настройки\", "*.ini"
            /// </summary>
            /// <param name="eQs"></param>
            /// <param name="fromIni"></param>
            internal void EQipment(EqupmentModel eQs, bool fromIni)
            {
                // Отсутствующая папка означает, что пользователь ещё не настроил соответствия.
                var directory = NGraph.Core.UserSettings.Load().IniDirectory;
                if (!Directory.Exists(directory)) return;
                string[] allFoundFiles = Directory.GetFiles(directory, "*.ini", SearchOption.AllDirectories);
                //Имя файла - ADSK_Группирование
                //aSection [ADSK_Позиция]
                foreach (string file in allFoundFiles)
                {
                    INIManager readini = new INIManager(file);
                    if (eQs.ADSK_Группирование == Path.GetFileNameWithoutExtension(file))
                    {
                        List<string> ADSK_Pozitions = new List<string>();
                        foreach (string line in File.ReadAllLines(file))
                        {
                            if (line.StartsWith("[") & line.Trim().EndsWith("]"))
                            {
                                ADSK_Pozitions.Add(line.Substring(1, line.Length - 2));
                            }
                        }
                        foreach (string aSection in ADSK_Pozitions)
                        {
                            if (eQs.ADSK_Позиция == aSection)
                            {
                                eQs.NameSymbol_EqupmentDrafting = readini.GetPrivateString(aSection, "Symbol");
                            }
                        }
                    }
                }
            }
        }

        class RoomSpace: BoxXYZ
        {
            internal List<EqupmentModel> EqupmentModels { get; set; } = [];
            internal LevelOfSection Level { get; }
            internal string Number { get; }
            internal string Name { get; }
            internal Space Space { get; }

            public RoomSpace(LevelOfSection e, Space space)
            {
                Space = space;
                Level = e;
                try
                {
                    Number = NgContext._FindParameter(space, BuiltInParameter.ROOM_NUMBER).AsString();
                }
                catch { Number = "не назначен номер"; }
                try
                {
                    Name = NgContext._FindParameter(space,BuiltInParameter.ROOM_NAME).AsString();
                }
                catch { Name = "не назначено имя"; }
                

            }


        }
        /// <summary>
        /// Этаж корпуса
        /// </summary>
        class LevelOfSection: BoxXYZ
        {
            internal List<RoomSpace> RoomSpaces { get; set; } = [];
            internal List<EqupmentModel> EqupmentModels { get; set; } = [];
            internal Section Section { get; }
            internal string Name { get; }
            internal int Elevation { get; }
            public LevelOfSection(Section k, Level level)

            {
                Name = NgContext._FindParameter(level,BuiltInParameter.DATUM_TEXT).AsString();
                Elevation = (int)NgContext._FindParameter(level,BuiltInParameter.LEVEL_ELEV).AsDouble().ToMillimeters();
                Section = k;
            }
        }
        /// <summary>
        /// Корпус или секция
        /// </summary>
        class Section: BoxXYZ
        {
            internal List<LevelOfSection> Levels { get; set; } = [];
            internal List<EqupmentModel> EqupmentModels { get; set; } = [];
            internal string Name { get; }
            public Section(string k)
            {
                Name = k;
            }
            
        }

        /// <summary>
        /// Габариты элемента на чертежном виде
        /// </summary>
        class BoxXYZ
        {
            internal double Length { get; set; }
            internal double Height { get; set; }
            /// <summary>
            /// Координаты для установки на вид
            /// </summary>
            internal XYZ XYZ_inView { get; set; } = new();
            /// <summary>
            /// Координаты относительно хостового элемента
            /// </summary>
            internal XYZ XYZ_ByHost { get; set; } = new();


            /// <summary>
            /// Максимальный размер на плане
            /// </summary>
            internal XYZ XYZ_Gabarit { get; set; } = new();
            

            /// <summary>
            /// Координата для создания контура вокруг элемента
            /// </summary>
            internal XYZ XYZ_LineConturMin { get; set; } = new();
            /// <summary>
            /// Координата для создания контура вокруг элемента
            /// </summary>
            internal XYZ XYZ_LineConturMax { get; set; } = new();
            public BoxXYZ()
            {
                
            }

            /// <summary>
            /// Обновляем координаты.
            /// </summary>
            /// <param name="section"></param>
            internal static void Reload(List<Section> Sections)
            {
                //Смещение элементов
                XYZ mm5 = new XYZ(5 / 304.8, 5 / 304.8, 0);
                foreach (var s in Sections)
                {
                    s.XYZ_inView = s.XYZ_ByHost;
                    s.XYZ_Gabarit = new XYZ(s.Length, s.Height, 0);
                    s.XYZ_LineConturMin = s.XYZ_inView- mm5*3;
                    s.XYZ_LineConturMax = s.XYZ_ByHost + s.XYZ_Gabarit+ mm5*3;

                    foreach (var l in s.Levels)
                    {
                        l.XYZ_inView = l.XYZ_ByHost + s.XYZ_inView;
                        l.XYZ_Gabarit = new XYZ(l.Length, l.Height, 0);
                        l.XYZ_LineConturMin = l.XYZ_inView - mm5 * 2;
                        l.XYZ_LineConturMax = l.XYZ_inView + l.XYZ_Gabarit+mm5 * 2;


                        foreach (var r in l.RoomSpaces)
                        {

                            r.XYZ_inView =  r.XYZ_ByHost + l.XYZ_inView;
                            r.XYZ_Gabarit = new XYZ(r.Length, r.Height, 0);
                            r.XYZ_LineConturMin = r.XYZ_inView - mm5;
                            r.XYZ_LineConturMax = r.XYZ_inView + r.XYZ_Gabarit + mm5;



                            

                            foreach (var e in r.EqupmentModels)
                            {
                                e.EqupmentDrafting.XYZ_inView = e.EqupmentDrafting.XYZ_ByHost + r.XYZ_inView + new XYZ(e.EqupmentDrafting.Length, e.EqupmentDrafting.Height,0 );
                                // положение относительно хоста + положение комнаты на виде + смещение на свой корпус
                            }
                        }
                    }
                }
            }



        }

    }
