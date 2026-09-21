using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Core.FunctionalScheme;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;
using Document = Autodesk.Revit.DB.Document;

namespace NGraph.Commands;

/// <summary>
///     Info for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AlgoritmAddOnViewElementsFromBd : ExternalCommand
{
    
        public override void Execute()
        {

           // Читаем настройки при запуске: сохранённое имя действует без перезапуска Revit.
        string nameOfBdViewDrafting = NGraph.Core.UserSettings.Load().ElementsView;
            var helper = new Helpers();
            var view = helper.AllElementsOfCategory(Document, BuiltInCategory.OST_Views)
                .FirstOrDefault(x => x.Name == nameOfBdViewDrafting);
            var sections =  FSAmodelCreateFromDb.CreateFromBd_ReadFilledRegion(Document, view as View);

            #region CHANGE DATA
            //Показываем окно и задаем вид установке
            var viewModel = new NGraphCreateFootorFromBdViewModel
            {
                Sections = sections
            };
            var viewWindow = new NGraphCreateFootorFromBdView(viewModel);
            viewWindow.ShowDialog();
            var секция = (СекцияБазыДанных)viewWindow.selectedSections;
            var closingwindowCancel = (bool)viewWindow.Cancel;
            
            FSAmodelCreateFromDb.CreateFromBd_AddToSectionOtherElements(Document, view as View, ref секция);
            

            var newBd = new БазаДанных();
            newBd.Секции.Add(секция);

            #endregion

            if (closingwindowCancel)
            {

            }
            else
            {
                var fs = new FilteredElementCollector(Document).OfClass(typeof(FamilySymbol)).First(q => q.Name == "Размещение семейств") as FamilySymbol;
                try
                { UiDocument.PromptForFamilyInstancePlacement(fs); }
                catch (Exception e)
                {
                    // ignored
                }

                var elements= Document.GetElements(ActiveView.Id).Where(i=>i.Name == "Размещение семейств").ToList();

                var xyzes = elements.Select(e => ((e as FamilyInstance)?.Location as LocationPoint)?.Point).OfType<XYZ>().ToList();

                #region WRITE DATA
                
                var newView = Document.ActiveView as ViewDrafting;
                using (var tr = new Transaction(Document, $"CopyElements"))
                {
                  tr.Start();
                    try
                    {
                        Document.Delete(elements.Select(i => i.Id).ToList());
                        foreach (var coord in xyzes)
                        {
                            var section = newBd.Секции.FirstOrDefault();
                            if (section == null) continue;
                            var xyz = coord - section.Центр;
                           if (newView != null)
                                FSAmodelCreateFromDb.CreateFromBd_withoutTransaction(Document, ref section,
                                    newView, viewWindow.ИмяУстановки.Text, xyz);
                        }
                        
                    }
                    // catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return;}
                    catch
                    {
                        // ignored
                    }

                    tr.Commit();
                    
                    

                }

                if ((bool)viewWindow.CreateFSA)
                {
                    
                    AlgoritmCreateFootor.AlgoritmCreateFootorCommand(Document);
                }

                #endregion

            }
        }

        
        public static List<СекцияБазыДанных>? AlgoritmCreateFootorFromBd_ReadFilledRegion(Document Document, View? view)
        {
            List < СекцияБазыДанных > sections = [];
            
            if (view == null)
            {
                TaskDialog.Show("Внимание", "Отсутствует чертежный вид с именем " + view?.Name);
                return null; 
            }
            var elements = Document.GetElements(view.Id);
            
            var filledRegions = elements.ToList().Where(x => x.GetType() == typeof(FilledRegion)).ToList()
                .Where(i => NgContext._FindParameter(i,"Тип").AsValueString().Contains("BD_")).ToList();
            
            foreach (var variable in filledRegions)
            {
                sections.Add(new СекцияБазыДанных(variable as FilledRegion, view as View));
            }
            
            
            return sections;
        }



        public static void AlgoritmCreateFootorFromBd_AddToSectionOtherElements(Document document , View view , ref СекцияБазыДанных section)
        {
            //Все элементы с чертежного вида
            var elements = document.GetElements(view.Id);

            //Элементы из бызы отфильтрованные по нужным категориям
            var filteredElements = elements
                .Where(x => x.GetType() == typeof(FamilyInstance)
                            || (x.GetType() == typeof(Group) &&  x.Location != null) //Принадлежит группе и эта группа не входит в другую
                            || x.GetType() == typeof(DetailLine)
                            || x.GetType() == typeof(TextNote)
                );
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
        
        
        
        
      
        
        
        
}