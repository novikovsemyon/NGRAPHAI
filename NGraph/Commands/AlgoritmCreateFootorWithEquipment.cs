using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Core.FunctionalScheme;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;

namespace NGraph.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AlgoritmCreateFootorWithEquipment : ExternalCommand
{
    public override void Execute()
    {
        Command(Document);
    }

    private static void Command(Document Document)
    {
        FSAmethods fSAmethods = new FSAmethods();
        var FindedGroupHeaders = new List<string>(); //Список содержит перечень _Установок на схеме
        var FindedFooters = fSAmethods.FootorFind(Document);

     #region 0. Get ActiveViewDrafting
     ViewDrafting activeViewDrafting = Document.ActiveView as ViewDrafting;
     #endregion

     #region 1. Read shema FSA (Get list <FSAHeader>)
     List<FSAheader> headers = fSAmethods.ReadHederStruct(Document, activeViewDrafting);
     #endregion


     //*************************
     //Определение точек, которые находятся в пучке. Этим определяется принадлежность к одному элементу.
     //Далее для каждого ключа пишем в параметр Комментарий, что это один элемент, у которого несколько точек FAS_точка (кабелей)
                 
     List<ElementHeader> elementHeaders = []; // Точки сгруппированные в элементы
     
     var elementHeaders_FamilyInstance = Document.GetElements(activeViewDrafting.Id).ToElements()// Список оборудования на виде
         .OfType<FamilyInstance>()
         .Where(k => k.get_Parameter(new Guid(Const.Param_NS_Equpment_guid)) != null)
         .Where(i => i.Name is Const.Element_Header or "Шкаф" or "Empty");
     foreach (var fi in elementHeaders_FamilyInstance)
     {
         elementHeaders.Add(new ElementHeader(fi, activeViewDrafting, headers));
     }
    
        //Группировка точек по Установке 
        int group_index = 0; //Индекс по установке
        foreach (var group in headers.GroupBy(i => i.Group))
        {
            int i = 1; int ii = 10001;
            foreach (var fh in group.Take(group.Count()).ToList().OrderBy(i => (i.ElementHeader?.FamilyInstance.Location as LocationPoint).Point.X)
                         .ThenBy((i => i.XYZ.X)))
            {
                if (fh.isNumbering) //Если элемент подлежит маркировке - маркируем иначе пишем 0
                {
                    fh.Parameter_position_number_set = ((group_index + i).ToString());
                    i++;
                }
                else
                {
                    fh.Parameter_position_number_set = (group_index + ii).ToString(); //При сортировке эти элементы всегда будут справа
                    ii++;
                }
            }
            group_index += 100;
            
        }
        
        //Группировка оборудования по Установке /затем по ГОСТ
        
        foreach (var g in elementHeaders.GroupBy(i => (i.Param_NS_Equpment))) //по Установке
        {
            
            foreach (var gr in g.Take(g.Count()).ToList().GroupBy(i => i.Param_NS_GOST)) //По ГОСТ
            {
                
                int index = 1; //Индекс оборудования в группе по позиции
                foreach (ElementHeader eh in gr.Take(gr.Count()).ToList().OrderBy(i => (i?.FamilyInstance.Location as LocationPoint).Point.X)) // По каждому элементу оборудования, сортируем по X
                {
                    eh.index = index; //Назначаем оборудованию индекс по позиции
                    //eh.FamilyInstance.LookupParameter(Const.Param_NS_PanelName).Set($"{index}"); 
                    

                    foreach (FSAheader fh in eh.fSAheadersEQ)
                    {
                        fh.CableLog.ПерезаписьПараметров = fh.FI.LookupParameter(Const.Param_CJ_Reload).AsBool();
                        fh.CableLog.ОбозначениеКабеля = fh.Group +"/"+fh.Parameter_position_number_set;
                        fh.CableLog.Длина = fh.FI.LookupParameter(Const.Param_CJ_Lenght).AsInteger();
                        fh.CableLog.НачалоОбозначение =eh.FamilyInstance.LookupParameter("ADSK_Позиция").AsString()+"-"+eh.index;
                        fh.CableLog.НачалоОборудование = eh.FamilyInstance.LookupParameter("ADSK_Наименование").AsString();
                        fh.CableLog.КонецОбозначение = "ЩА-"+eh.Param_NS_Equpment;
                        fh.CableLog.КонецОборудование = "Щит автоматизации";
                        //fh.CableLog.Трасса = eh.FamilyInstance.LookupParameter("CJ_Трасса").AsString();
                        fh.Parameter_name_element_set = (eh.Param_NS_GOST + "-" + eh.index).ToString();
                    
                    }
                    index++;
                }
            }
        }
      
        using (Transaction tr = new Transaction(Document, $"Нумерация функциональной схемы"))
        {
            tr.Start();
            try
            {
                foreach (var eh in elementHeaders)
                {
                    eh.FamilyInstance.LookupParameter(Const.Param_NS_PanelName).Set(eh.index.ToString());
                    foreach (var fh in eh.fSAheadersEQ)
                    {
                        fh.FI.LookupParameter(Const.Param_CJ_Number).Set(fh.CableLog.ОбозначениеКабеля);
                        fh.FI.LookupParameter(Const.Param_CJ_Lenght).Set(fh.CableLog.Длина);
                        fh.FI.LookupParameter(Const.Param_CJ_Begin).Set(fh.CableLog.НачалоОбозначение);
                        fh.FI.LookupParameter(Const.Param_CJ_Begin_eq).Set(fh.CableLog.НачалоОборудование);
                        fh.Parameter_position_number.Set(fh.Parameter_position_number_set);
                        fh.Parameter_name_element.Set(fh.Parameter_name_element_set);
                        
                        if (fh.CableLog.ПерезаписьПараметров)
                        {
                            fh.FI.LookupParameter(Const.Param_CJ_End).Set(fh.CableLog.КонецОбозначение);
                            fh.FI.LookupParameter(Const.Param_CJ_End_eq).Set(fh.CableLog.КонецОборудование);
                        }
                     

                    }
                }
            }
            catch
            {
           
            }
            tr.Commit();

        }
   


/*

        //*************************

         #region 2. Numbering shema by XY from left to right then by from down to up
         using (Transaction tr = new Transaction(Document, $"Нумерация функциональной схемы"))
     {
         tr.Start();
         try
         {
             int group_interator = 0;
             var groupedHeaders = headers.GroupBy(i => i.Group); //Группируем по _Установка

             foreach (var group in groupedHeaders)
             {
                 FindedGroupHeaders.Add(group.Key);
                 var listSC = group.Take(group.Count()).ToList();
                 var groupedlXYZ = listSC
                         .OrderBy(i => (i.ElementHeader?.FamilyInstance.Location as LocationPoint).Point.X)
                         .ThenBy((i => i.XYZ.X))
                     ;


                 int i = 1; int ii = 10001;
                 foreach (FSAheader sControl in groupedlXYZ)
                 {
                     if (sControl.isNumbering) //Если элемент подлежит маркировке - маркируем иначе пишем 0
                     {
                         sControl.Parameter_position_number.Set((group_interator + i).ToString());
                         i++;

                     }
                     else
                     {
                         sControl.Parameter_position_number.Set((group_interator + ii).ToString()); //При сортировке эти элементы всегда будут справа
                         ii++;
                     }
                 }

                 var groupedlistSC = listSC.GroupBy(i => i.Gost); //Группировка по GOST
                 foreach (var gost in groupedlistSC)
                 {
                     string GostName = gost.Key;
                     int n = 1;
                     foreach (FSAheader sControl in gost.Take(gost.Count()).ToList())
                     {
                         sControl.Parameter_name_element.Set(GostName + "-" + n.ToString());
                         n++;
                     }

                 }
                 group_interator = group_interator + 100;

             }
         }
         catch
         {
             #region 2.1
             if (headers.Any(i => i.GroupRevit == null))
             {
                 TaskDialog.Show("Ошибка 2", "Семейства\n\"FAS_точка\"\n\"FAS_точка без маркировки\"\nнаходятся за границами\n\"Группы элементов узлов\"\nОшибка нумерации структурной схемы");
             }

             else { TaskDialog.Show("Ошибка 2", "#Error region 2."); }


             #endregion
         }
         tr.Commit();

     }
     #endregion

     */
     

        #region 3. Remoove and create footor

        //================================

        var footersCreated =new  List<FSAfooter>(); 

        if (FindedFooters.Count == 0) //Если нет футоров
        {
            //Создание футеров заново
            footersCreated = fSAmethods.FootorCreateByStruct(Document, headers);
        }

        else if (FindedFooters.Count== FindedGroupHeaders.Count()) // Если количество футоров == количеству установок
        {
            //Получаем координаты футоров и на их места расставляем заново новые
            var xyxFootors = new List<XYZ>();
            foreach (FSAfooter f in FindedFooters)
            {
                xyxFootors.Add((f.FI.Location as LocationPoint).Point);
            }
            //Удаляем футеры и их элементы
            fSAmethods.RemooveNotPinnedElementFromFootor(Document, "FSA_CABEL_");
            fSAmethods.RemooveFootor(Document, "_Подвал");

            //Создание футеров заново по координатам
            footersCreated = fSAmethods.FootorCreateByStruct(Document, headers, xyxFootors); //
        }

        else
        {
            fSAmethods.RemooveNotPinnedElementFromFootor(Document, "FSA_CABEL_");
            fSAmethods.RemooveFootor(Document, "_Подвал");
            footersCreated = fSAmethods.FootorCreateByStruct(Document, headers);
        }

        //================================

        #endregion


        //Сорировка элементов на футоре по номеру
        foreach (var footer in footersCreated)
        {
            footer.FSAheaders.Sort();
        }
        footersCreated.Count();

        //Создание групп на футере с элементами
        foreach (var footer in footersCreated)
        {
            fSAmethods.CreateGroupOnFootor(Document, activeViewDrafting, footer);
        }
        footersCreated.Count();






        //Вычисление Di Do Ai Ao

        using (Transaction tr = new Transaction(Document, $"Вычисление Di Do Ai Ao"))
        {
            var extention = true;
            tr.Start();
            try
            {

                foreach (var footer in footersCreated)
                {
                    footer.Set_di_do_ai_ao_group(Document);
                }
                footersCreated.Count();

            }
            catch { }
            tr.Commit();

        }





        Helpers helpers = new Helpers();



        #region Получение информации о чертежном виде
        ViewDrafting activeView = Document.ActiveView as ViewDrafting;
        string FAS_Установка = "c02346dd-96c0-4c88-8cd5-f7ab6a5fa159";
        var elements_AnnotationSymbol = Document.GetElements(activeView.Id).ToElements().OfType<AnnotationSymbol>().Where(k => k.get_Parameter(new Guid(FAS_Установка)) != null);
        var elements_AnnotationSymbol_FSA_CABEL = Document.GetElements(activeView.Id).ToElements().OfType<AnnotationSymbol>().Where(k => k.Name.Contains("FSA_CABEL_"));
        var elements_AnnotationSymbol_FSA_CABEL_FIRST = Document.GetElements(activeView.Id).ToElements().OfType<AnnotationSymbol>().Where(k => k.Name.Contains("FSA_CABEL_FIRST"));
        var elements_FamilyInstance = Document.GetElements(activeView.Id).ToElements()
            .OfType<FamilyInstance>()
            .Where(k => k.get_Parameter(new Guid(FAS_Установка)) != null)
            .Where(i => i.GetOrderedParameters().Where(k => k.Definition.Name.Contains("N_")).Count() > 0);


        #endregion



        #region Маркировка элементов подвала

        using (Transaction tr = new Transaction(Document, $"Марка"))
        {
            tr.Start();
            try
            {


                TagMode tagMode = TagMode.TM_ADDBY_CATEGORY;
                TagOrientation tagorn = TagOrientation.Horizontal;


                var symId = new FilteredElementCollector(Document).
                    OfCategory(BuiltInCategory.OST_DetailComponentTags).
                    WhereElementIsElementType().
                    ToList().Where(i => i.Name == "BE_Марка_Элемент_узла").FirstOrDefault().Id;

                FilteredElementCollector fsCollector = new FilteredElementCollector(Document, activeView.Id);

                //Исключение - не маркируются OST_DetailComponents, которые носят название FAS_точка
                fsCollector.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_DetailComponents);
                ICollection<Element> collection = fsCollector.ToElements();


                //Нумеруем только с названием Вертикально
                foreach (var element in collection.Where(i => i.Name == "Вертикально"))
                {
                    FamilyInstance familyInstance = element as FamilyInstance;
                    XYZ DetailComponentsLocation = familyInstance.get_BoundingBox(activeView).Min;
                    Reference elRef = new Reference(familyInstance);


                    IndependentTag newTag = IndependentTag.Create(Document, activeView.Id, elRef, true, tagMode, tagorn, DetailComponentsLocation);

                    if (null == newTag)
                    {
                        throw new Exception("Create IndependentTag Failed.");
                    }
                    newTag.ChangeTypeId(symId);
                    //newTag.LookupParameter("Тип").Set(symId.Id);
                    newTag.HasLeader = false;
                    newTag.TagHeadPosition = DetailComponentsLocation;
                }


            }
            catch { TaskDialog.Show("Ошибка", "Ошибка 127 "); }
            tr.Commit();
        }

        #endregion



     

    

 

    }
}

