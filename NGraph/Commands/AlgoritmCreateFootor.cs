using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Core.FunctionalScheme;
using Nice3point.Revit.Toolkit.External;
using Autodesk.Revit.Creation;
using Document = Autodesk.Revit.DB.Document;

namespace NGraph.Commands;

/// <summary>
///     Info for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AlgoritmCreateFootor : ExternalCommand
{
    public override void Execute()
    {
        AlgoritmCreateFootorCommand(Application.ActiveUIDocument.Document);
    }
    public static void AlgoritmCreateFootorCommand(Document Document)
    {
        
 
     
     
     FSAmethods fSAmethods = new FSAmethods();

     

     var FindedGroupHeaders = new List<string>(); //Список содержит перечень _Установок на схеме
     var FindedFooters = fSAmethods.FootorFind(Document);

     #region 0. Get ActiveViewDrafting
     if (Document.ActiveView is not ViewDrafting activeViewDrafting)
     {
         TaskDialog.Show("NGraph", "Откройте чертёжный вид перед построением схемы.");
         return;
     }
     #endregion

     #region 1. Read shema FSA (Get list <FSAHeader>)
     List<FSAheader> headers = fSAmethods.ReadHederStruct(Document, activeViewDrafting);
     #endregion


     //*************************
     //Определение точек, которые находятся в пучке. Этим определяется принадлежность к одному элементу.
     //Далее для каждого ключа пишем в параметр Комментарий, что это один элемент, у которого несколько точек FAS_точка (кабелей)
                 
     List<ElementHeader> elementHeaders = []; // Точки сгруппированные в элементы
     List<FSAheader> fSAheaders = [.. headers]; //Список точек
     foreach (var header in fSAheaders) //
     {
         Predicate<FSAheader> isCross = (FSAheader x) => ElementHeader.IsCrossingElement_withHimself(x, header, activeViewDrafting);
         List<FSAheader> neighborCrossing = fSAheaders.FindAll(isCross); //Список пересекающихся соседних элементов
         header.Crossing.AddRange(neighborCrossing);
     }
     
     while (fSAheaders.Any())
     {
         var first = fSAheaders.First();
         if (first.Crossing.Count == 0)
         {
             ElementHeader elementHeader = new ElementHeader();
             elementHeader.fSAheaders.Add(first);
             elementHeaders.Add(elementHeader); //Создаем элемент и добавляем к нему в список
             fSAheaders.Remove(first);
         }

         else
         {
             ElementHeader elementHeader = new ElementHeader();
             elementHeader.fSAheaders.AddRange(first.Crossing);
             elementHeaders.Add(elementHeader);
             
             while(elementHeader.fSAheaders.Any(x=> fSAheaders.Any(y=>y.Crossing.Any(z=>z==x))))
             {
                 for (int i = 0; i < fSAheaders.Count; i++)
                 {
                     if (elementHeader.fSAheaders.Any(x => fSAheaders[i].Crossing.Any(y => y == x)))
                     {
                         elementHeader.fSAheaders.AddRange(fSAheaders[i].Crossing);
                         elementHeader.fSAheaders = [.. elementHeader.fSAheaders.Distinct()];//Удаление дубликатов
                         fSAheaders.Remove(fSAheaders[i]);
                     }
                     
                 }
             }
             fSAheaders.Remove(first);
         }
     }

     using (Transaction tr0 = new Transaction(Document, $"Нумерация функциональной схемы"))
     {
         tr0.Start();
         int indexcrossing = 1;
         foreach (var d in elementHeaders)
         {
             foreach (var i in d.fSAheaders)
             {
                 i.FI.LookupParameter("Комментарии").Set($"Прибор {indexcrossing}");
             }
             indexcrossing++;
         }
         tr0.Commit();

     }
     
     

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

                 string nameGroup = group.Key;
                 var listSC = group.Take(group.Count()).ToList();
                 

                 
                 var groupedlXYZ = listSC
                     .OrderBy(i => ((i.GroupRevit?.Location as LocationPoint)?.Point ?? i.XYZ).X)
                     //.ThenByDescending((v => v.GroupRevit.GetPlacementPoint().Y))
                     .ThenBy((i => i.XYZ.X))
                     //.ThenByDescending((y => y.XYZ.Y))
                     ;

                 //var groupedlXYZ = listSC.OrderBy((i => i.XYZ.X)).ThenByDescending((i => i.XYZ.Y)).OrderBy(i => i.GroupRevit.Id.IntegerValue); //Группировка по координатам и группам ревита
                 
                 


                 
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
             xyxFootors.Add(f.FI.GetPlacementPoint());
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
     ViewDrafting activeView = activeViewDrafting;
     string FAS_Установка = "c02346dd-96c0-4c88-8cd5-f7ab6a5fa159";
     var elements_AnnotationSymbol = Document.CollectElements(activeView.Id).ToElements().OfType<AnnotationSymbol>().Where(k => k.get_Parameter(new Guid(FAS_Установка)) != null);
     var elements_AnnotationSymbol_FSA_CABEL = Document.CollectElements(activeView.Id).ToElements().OfType<AnnotationSymbol>().Where(k => k.Name.Contains("FSA_CABEL_"));
     var elements_AnnotationSymbol_FSA_CABEL_FIRST = Document.CollectElements(activeView.Id).ToElements().OfType<AnnotationSymbol>().Where(k => k.Name.Contains("FSA_CABEL_FIRST"));
     var elements_FamilyInstance = Document.CollectElements(activeView.Id).ToElements()
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
                     First(i => i.Name == "BE_Марка_Элемент_узла").Id;

                 FilteredElementCollector fsCollector = new FilteredElementCollector(Document, activeView.Id);

                 //Исключение - не маркируются OST_DetailComponents, которые носят название FAS_точка
                 fsCollector.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_DetailComponents);
                 ICollection<Element> collection = fsCollector.ToElements();


                 //Нумеруем только с названием Вертикально
                 foreach (FamilyInstance familyInstance in collection.OfType<FamilyInstance>().Where(i => i.Name == "Вертикально"))
                 {
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