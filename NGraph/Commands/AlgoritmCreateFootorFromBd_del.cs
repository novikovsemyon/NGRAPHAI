using System.Windows.Annotations;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Core.FunctionalScheme;
using Nice3point.Revit.Toolkit.External;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using NGraph.ViewModels;
using NGraph.Views;
using Document = Autodesk.Revit.DB.Document;

namespace NGraph.Commands;

/// <summary>
///     Info for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AlgoritmCreateFootorFromBd_del : ExternalCommand
{
    

        public override void Execute()
        {

            var базаДанных = AlgoritmCreateFootorFromBd_ReadData(Document);

       

            //базаДанных.Секции.Count();

            //var groupedBD = базаДанных.Секции.GroupBy(x => x.Name).ToList();

            #region CHANGE DATA
            //Показываем окно и задаем вид установке
            var viewModel = new NGraphCreateFootorFromBdViewModel();
            
            viewModel.BD = базаДанных;
            var viewWindow = new NGraphCreateFootorFromBdView(viewModel);
            
            viewWindow.ShowDialog();

            var Секция = (СекцияБазыДанных)viewWindow.selectedSections;

            bool closingwindowCancel = (bool)viewWindow.Cancel;



            БазаДанных newBD = new БазаДанных();
            newBD.Секции.Add(Секция);
            #endregion

            if (closingwindowCancel)
            {

            }
            else
            {


                #region WRITE DATA
                
                
                Helpers helpers = new Helpers();
                //Создаем чертежный вид и их аннотативные обозначения (элементы узлов)
                ViewDrafting new_view = helpers.CreateViewDrafting(Document, "Схема_"+viewWindow.ИмяУстановки.Text, true, UiDocument);

                using (Transaction tr = new Transaction(Document, $"CopyElements"))
                {

                    tr.Start();
                    try
                    {
                        XYZ xyz = new XYZ(0, -0.3, 0); //Позиция на функциональной схеме для секции
                        foreach (var section in newBD.Секции)
                        {

                            foreach (var t in section.Точки)  //Точки и марки
                            {
                                FamilyInstance fi = Document.Create.NewFamilyInstance(t.Location + xyz, t.FamilySymbol, new_view);
                                fi.LookupParameter("_ГОСТ 21.208").Set(t._ГОСТ212008);
                                fi.LookupParameter("_Принцип").Set(t._Принцип);
                                fi.LookupParameter("_Типизация").Set(t._Типизация);
                                //fi.LookupParameter("_Установка").Set(t._Установка);
                                fi.LookupParameter("_Установка").Set(viewWindow.ИмяУстановки.Text);
                                fi.LookupParameter("_Элемент_на_щите").Set(t._Элемент_на_щите);
                                fi.LookupParameter("CJ_Рабочий набор").Set(new_view.Name);
                                fi.LookupParameter("NS_ElementId").Set("нет связи");
                                fi.LookupParameter("_Din").Set(t._Din);
                                fi.LookupParameter("_Dout").Set(t._Dout);
                                fi.LookupParameter("_Ain").Set(t._Ain);
                                fi.LookupParameter("_Aout").Set(t._Aout);
                                fi.LookupParameter("_HMI").Set(t._HMI);
                                fi.LookupParameter("_Interface").Set(t._Interface);

                                Reference elRef = new Reference(fi);
                                IndependentTag it = IndependentTag.Create(Document, new_view.Id, elRef, true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, t.Марка.LocationHeader + xyz);
                                it.ChangeTypeId(t.Марка.FamilySymbol.Id);
                                it.TagHeadPosition = t.Марка.LocationHeader + xyz;


#if REVIT2023_OR_GREATER

                                if (t.Марка.HasLeader)
                                {
                                    if (t.Марка.HasElbow) {
                                        Reference referensLEader = new Reference(fi);
                                       // it.LeaderEndCondition = LeaderEndCondition.Free;
                                        it.GetTaggedReferences().Add(referensLEader);
                                        it.SetLeaderElbow(referensLEader, t.Марка.LocationLeaderElbow + xyz);
                                       //it.SetIsLeaderVisible(referensLEader, true);
                                    } // Важен порядок комманд
                                    
                                }
                                else { it.HasLeader = false; }

#elif REVIT2022
                                if (t.Марка.HasLeader)
                                {
                                    if (t.Марка.HasElbow)
                                    {
                                        Reference referensLEader = new Reference(fi);
                                        // it.LeaderEndCondition = LeaderEndCondition.Free;
                                        it.GetTaggedReferences().Add(referensLEader);
                                        it.SetLeaderElbow(referensLEader, t.Марка.LocationLeaderElbow + xyz);
                                        //it.SetIsLeaderVisible(referensLEader, true);
                                    } // Важен порядок комманд

                                }
                                else { it.HasLeader = false; }



#else
                                if (t.Марка.HasLeader)
                                {
                                    if (t.Марка.HasElbow) { it.LeaderElbow = t.Марка.LocationLeaderElbow + xyz;} // Важен порядок комманд
                                }
                                else { it.HasLeader = false; }
                                    
#endif




                            }
                            foreach (var g in section.Группы)  //Группы
                            {
                                Group group = Document.Create.PlaceGroup(g.Location + xyz, g.Group.GroupType);
                            }

                            foreach (var l in section.Линии)  //Линии
                            {
                                var line = Autodesk.Revit.DB.Line.CreateBound(l.Begin + xyz, l.End + xyz);
                                DetailCurve dc = Document.Create.NewDetailCurve(new_view, line); dc.LineStyle = l.GraphicsStyle;
                            }
                            
                            foreach (var t in section.Texts)  //Текст
                            {
                                
                                TextNoteOptions textNoteOptions = new TextNoteOptions(t.TextNote.TextNoteType.Id);

                                textNoteOptions.Rotation = t.TextNote.BaseDirection.AngleTo(new XYZ(1,0,0));
                                textNoteOptions.HorizontalAlignment = t.TextNote.HorizontalAlignment;
                                textNoteOptions.VerticalAlignment = t.TextNote.VerticalAlignment;
                                TextNote textNote = Autodesk.Revit.DB.TextNote.Create(Document, ActiveView.Id, t.Coord+xyz, t.TextNote.GetFormattedText().GetPlainText(), textNoteOptions);
                                textNote.SetFormattedText(t.TextNote.GetFormattedText());

                            }
                            
                            xyz = xyz + new XYZ(0.3, 0.2, 0);
                        }
                    }
                    // catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return;}
                    catch  {}
                    tr.Commit();

                }

                if ((bool)viewWindow.CreateFSA)
                {
                    
                    AlgoritmCreateFootor.AlgoritmCreateFootorCommand(Document);
                }

                #endregion

            }
        }

        public static БазаДанных AlgoritmCreateFootorFromBd_ReadData(Document Document)
        {
            
            FSAmodelCreateFromDb fSAmodelHVAC   = new FSAmodelCreateFromDb();

 #region READ BASE DATA
            //Получаем базу данных (чертежный вид с элементами)

            string nameOfBdViewDrafting = "!_000_NGraph_БАЗА ДАННЫХ_ФСА";
            var el_view = fSAmodelHVAC.GetBDfromModel(Document,nameOfBdViewDrafting);

            if (el_view == null)
            {
                TaskDialog.Show("Внимание", "Отсутствует чертежный вид с именем " + nameOfBdViewDrafting);
                return null; 
            }

            //Передаем в форму значения составных элементов из базы данных
            //обновляем форму
            //Названия элементов 
            //Например: Датчик "Температура притока"  это область в которую входит геометрия для Цветовой областти.
            //Используются параметры для Цветовой области (FilledRegion) Наименование типа (BD_Датчики) и АDSK_Группирование (Температура притока).
            //Центр региона - точка размещения FamilyInstance


            //Все элементы с чертежного вида
            var elements = Document.GetElements(el_view.Id);

            //Элементы из бызы отфильтрованные по нужным категориям
            var filteredElements = elements
                .Where(x => x.GetType() == typeof(FamilyInstance)
                || (x.GetType() == typeof(Group) &&  x.Location != null) //Принадлежит группе и эта группа не входит в другую
                || x.GetType() == typeof(DetailLine)
                || x.GetType() == typeof(TextNote)
                );
            
            var elementsTag = Document.GetElements(el_view.Id)
            
                .Where(x => 
                x.GetType() == typeof(IndependentTag)
                );


            //Цветовые области, которые обозначают элементы из базы
            var filledRegions = elements.ToList().Where(x => x.GetType() == typeof(FilledRegion)).ToList()
                .Where(i => NgContext._FindParameter(i,"Тип").AsValueString().Contains("BD_"));
            var filledRegionsGrouped = filledRegions.GroupBy(i => NgContext._FindParameter(i,"Тип").AsValueString());


            БазаДанных базаДанных = new БазаДанных();

            foreach ( var filledRegion in filledRegions)
            {
                List<Element> lelements = []; //Список элементов для области

                var fl = (filledRegion as FilledRegion);

                var view = el_view as View;
                СекцияБазыДанных секцияБазыДанных = new(fl , view);
                базаДанных.Секции.Add(секцияБазыДанных);

                var bb = filledRegion.get_BoundingBox(view);
                
                foreach ( var e in filteredElements)
                {
                    try
                    {
                        if ((e.Location as LocationPoint) != null && (Helpers.Contains(bb, (e.Location as LocationPoint).Point, false))) //У групп и экземпляров семейств не должно быть нулевых локаций
                        {
                            if (e.GetType() == typeof(FamilyInstance))
                            {
                                lelements.Add(e); //Зеленая точка
                                Точка точка = new(секцияБазыДанных, e as FamilyInstance);
                                секцияБазыДанных.Точки.Add(точка);

                                foreach (var t in elementsTag)
                                {
                                    var tag = t as IndependentTag;

#if REVIT2024_OR_GREATER
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId.Value==(e.Id.Value)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        точка.Марка = new Марка(секцияБазыДанных, tag , e, Document);

                                    }
#elif REVIT2023
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId == (e.Id)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        точка.Марка = new Марка(секцияБазыДанных, tag, e, Document);

                                    }



#else

                                    if (tag.TaggedElementId.HostElementId.IntegerValue == e.Id.IntegerValue) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(tag);

                                        точка.Марка = new Марка(секцияБазыДанных, tag , e, Document);

                                    }
#endif
                                }


                            }
                            else if (e.GetType() == typeof(Group))
                            {
                                lelements.Add(e);
                                Группа группа = new(секцияБазыДанных, e as Group);
                                секцияБазыДанных.Группы.Add(группа);

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
                                Линия линия = new(секцияБазыДанных, e as DetailLine);
                                секцияБазыДанных.Линии.Add(линия);
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
                                Текст текст = new Текст(секцияБазыДанных, e as TextNote);
                                секцияБазыДанных.Texts.Add(текст);
                            }
                            

                        }
                        

                    }
                    catch { }
                    
                }
                
            }
            #endregion

            return базаДанных;
        }
        
        public static БазаДанных AlgoritmCreateFootorFromBd_ReadData(Document Document, string NameSection)
        {
            
            FSAmodelCreateFromDb fSAmodelHVAC   = new FSAmodelCreateFromDb();

 #region READ BASE DATA
            //Получаем базу данных (чертежный вид с элементами)

            string nameOfBdViewDrafting = "!_000_NGraph_БАЗА ДАННЫХ_ФСА";
            var el_view = fSAmodelHVAC.GetBDfromModel(Document,nameOfBdViewDrafting);

            if (el_view == null)
            {
                TaskDialog.Show("Внимание", "Отсутствует чертежный вид с именем " + nameOfBdViewDrafting);
                return null; 
            }

            //Передаем в форму значения составных элементов из базы данных
            //обновляем форму
            //Названия элементов 
            //Например: Датчик "Температура притока"  это область в которую входит геометрия для Цветовой областти.
            //Используются параметры для Цветовой области (FilledRegion) Наименование типа (BD_Датчики) и АDSK_Группирование (Температура притока).
            //Центр региона - точка размещения FamilyInstance


            //Все элементы с чертежного вида
            var elements = Document.GetElements(el_view.Id);

            //Элементы из бызы отфильтрованные по нужным категориям
            var filteredElements = elements
                .Where(x => x.GetType() == typeof(FamilyInstance)
                || (x.GetType() == typeof(Group) &&  x.Location != null) //Принадлежит группе и эта группа не входит в другую
                || x.GetType() == typeof(DetailLine)
                || x.GetType() == typeof(TextNote)
                );
            
            var elementsTag = Document.GetElements(el_view.Id)
            
                .Where(x => 
                x.GetType() == typeof(IndependentTag)
                );


            //Цветовые области, которые обозначают элементы из базы
            var filledRegions = elements.ToList().Where(x => x.GetType() == typeof(FilledRegion)).ToList()
                .Where(i => NgContext._FindParameter(i,"Тип").AsValueString().Contains("BD_"));
            var filledRegionsGrouped = filledRegions.GroupBy(i => NgContext._FindParameter(i,"Тип").AsValueString());


            БазаДанных базаДанных = new БазаДанных();

            foreach ( var filledRegion in filledRegions)
            {
                List<Element> lelements = []; //Список элементов для области

                var fl = (filledRegion as FilledRegion);

                var view = el_view as View;
                СекцияБазыДанных секцияБазыДанных = new(fl , view);
                базаДанных.Секции.Add(секцияБазыДанных);

                var bb = filledRegion.get_BoundingBox(view);
                
                foreach ( var e in filteredElements)
                {
                    try
                    {
                        if ((e.Location as LocationPoint) != null && (Helpers.Contains(bb, (e.Location as LocationPoint).Point, false))) //У групп и экземпляров семейств не должно быть нулевых локаций
                        {
                            if (e.GetType() == typeof(FamilyInstance))
                            {
                                lelements.Add(e); //Зеленая точка
                                Точка точка = new(секцияБазыДанных, e as FamilyInstance);
                                секцияБазыДанных.Точки.Add(точка);

                                foreach (var t in elementsTag)
                                {
                                    var tag = t as IndependentTag;

#if REVIT2024_OR_GREATER
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId.Value==(e.Id.Value)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        точка.Марка = new Марка(секцияБазыДанных, tag , e, Document);

                                    }
#elif REVIT2023
                                    if (tag.GetTaggedElementIds().FirstOrDefault().HostElementId == (e.Id)) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(e);
                                        точка.Марка = new Марка(секцияБазыДанных, tag, e, Document);

                                    }



#else

                                    if (tag.TaggedElementId.HostElementId.IntegerValue == e.Id.IntegerValue) //Если метка принадлежит элементу (находим хост)
                                    {
                                        lelements.Add(tag);

                                        точка.Марка = new Марка(секцияБазыДанных, tag , e, Document);

                                    }
#endif
                                }


                            }
                            else if (e.GetType() == typeof(Group))
                            {
                                lelements.Add(e);
                                Группа группа = new(секцияБазыДанных, e as Group);
                                секцияБазыДанных.Группы.Add(группа);

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
                                Линия линия = new(секцияБазыДанных, e as DetailLine);
                                секцияБазыДанных.Линии.Add(линия);
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
                                Текст текст = new Текст(секцияБазыДанных, e as TextNote);
                                секцияБазыДанных.Texts.Add(текст);
                            }
                            

                        }
                        

                    }
                    catch { }
                    
                }
                
            }
            #endregion

            return базаДанных;
        }
        
        
        
}