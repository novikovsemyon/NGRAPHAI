using System.ComponentModel;
using System.Reflection;
namespace NGraph.Core.FunctionalScheme;

/// <summary>
/// Методы для построения ФСА
/// </summary>
 public class FSAmethods

 {


     /// <summary>
     /// Удаление всех незакрепленных элементов из футоров на активном виде
     /// </summary>
     /// <param name="doc"></param>
     /// <returns></returns>
     public bool RemooveNotPinnedElementFromFootor(Document doc, string FSA_CABEL_)
     {
         
         if (doc.ActiveView is not ViewDrafting activeView)
             return false;

         var elements_where_is_not_Pinned = new FilteredElementCollector(doc, activeView.Id).ToElements()
                .OfType<FamilyInstance>()
                .Where(k => k.Name.Contains(FSA_CABEL_)
                || k.Name.Contains(Const.Element_CabelForFootor))
                .Where(k => k.Pinned == false)
                .Select(i => i.Id).ToList();
         /*
         var elementsTAG_where_is_not_Pinned = new FilteredElementCollector(doc, activeView.Id).ToElements()
           .OfType<IndependentTag>()
           .Where(k => k.Name == Const.Element_Tag_CabelForFootor)
           .Where(k => k.Pinned == false)
           .Select(i => i.Id).ToList();
         */
         using (Transaction tr = new Transaction(doc, $"Удаление незакрепленных элементов"))
         {
             var extention = true;
             tr.Start();
             try
             {
                 //doc.Delete(elementsTAG_where_is_not_Pinned);//Сначала удаляем марки
                 doc.Delete(elements_where_is_not_Pinned);


             }
             catch { extention = false; }
             tr.Commit();
             return extention;

         }
     }

     /// <summary>
     /// Удаление всех незакрепленных элементов из футоров на активном виде
     /// </summary>
     /// <param name="doc"></param>
     /// <returns></returns>
     public bool RemooveFootor(Document doc, string Footer)
     {

         if (doc.ActiveView is not ViewDrafting activeView)
             return false;

         var elements = new FilteredElementCollector(doc, activeView.Id).ToElements()
                .OfType<FamilyInstance>()
                .Where(k => k.Name.Contains(Footer)
                )
                .Where(k => k.Pinned == false)
                .Select(i => i.Id).ToList();
         /*
         var elementsTAG_where_is_not_Pinned = new FilteredElementCollector(doc, activeView.Id).ToElements()
           .OfType<IndependentTag>()
           .Where(k => k.Name == Const.Element_Tag_CabelForFootor)
           .Where(k => k.Pinned == false)
           .Select(i => i.Id).ToList();
         */
         using (Transaction tr = new Transaction(doc, $"Удаление футора "))
         {
             var extention = true;
             tr.Start();
             try
             {
                 //doc.Delete(elementsTAG_where_is_not_Pinned);//Сначала удаляем марки
                 doc.Delete(elements);


             }
             catch { extention = false; }
             tr.Commit();
             return extention;

         }
     }


    

     /// <summary>
     /// Чтение структурной схемы с активного вида (элементы определяются по присутствию параметра Param_NS_Equpment и через имя типа FSA"
     /// </summary>
     /// <param name="doc"></param>
     /// <returns></returns>
     public List<FSAheader> ReadHederStruct(Document doc, ViewDrafting activeView)
     {
         List<FSAheader> fSAelementOfHeaderStructs = [];
         //ViewDrafting activeView = doc.ActiveView as ViewDrafting;
         var elements_FamilyInstance = new FilteredElementCollector(doc, activeView.Id).ToElements()
             .OfType<FamilyInstance>()
             .Where(k => k.get_Parameter(new Guid(Const.Param_NS_Equpment_guid)) != null)
             .Where(i => i.Name == Const.Element_Header_Users || i.Name == Const.Element_Header_Users_NoTag);

         var groups = new FilteredElementCollector(doc, activeView.Id).ToElements()
             .OfType<Group>().Where(i => i.Location != null).ToList();

         


         foreach (var fi in elements_FamilyInstance)
         {
             FSAheader fSAelementOfHeaderStruct = new FSAheader(fi);
             
             foreach (Group g in groups)
             {
                 
                 if (fi.Location is not LocationPoint fiLocation)
                     continue;

                 var point = fiLocation.Point;
                 var b = g.get_BoundingBox(activeView);
                 if (b is null)
                     continue;

                 if (point.X < b.Max.X && point.X > b.Min.X && point.Y < b.Max.Y && point.Y > b.Min.Y)
                 {
                     fSAelementOfHeaderStruct.GroupRevit = g;
                     

                 }
                 
             }
             

             fSAelementOfHeaderStructs.Add(fSAelementOfHeaderStruct);
         }
         return fSAelementOfHeaderStructs;

     }
     /// <summary>
     /// Чтение структурной схемы с активного вида (исключено горизонтально вертикально FAS точка, групп нет)
     /// </summary>
     /// <param name="doc"></param>
     /// <returns></returns>
     public List<FSAheader> ReadHederStruct_inversible(Document doc, ViewDrafting activeView)
     {
         List<FSAheader> fSAelementOfHeaderStructs = [];
         //ViewDrafting activeView = doc.ActiveView as ViewDrafting;
         var elements_FamilyInstance = new FilteredElementCollector(doc, activeView.Id).ToElements()
             .OfType<FamilyInstance>()
             .Where(i => i.Category.Name == "Элементы узлов")
             .Where(k => k.get_Parameter(new Guid(Const.Param_NS_Equpment_guid)) != null)
             .Where(i =>
             i.Name != Const.Element_Header_Users
             && i.Name != Const.Element_Header_Users_NoTag
             && i.Name != "Вертикально"
             && i.Name != "Горизонтально"
             );
             

        // var groups = new FilteredElementCollector(doc, activeView.Id).ToElements()
          //   .OfType<Group>().Where(i => i.Location != null).ToList();




         foreach (var fi in elements_FamilyInstance)
         {
             FSAheader fSAelementOfHeaderStruct = new FSAheader(fi);
             /*
             foreach (Group g in groups)
             {

                 var point = (fi.Location as LocationPoint).Point;
                 var b = g.get_BoundingBox(activeView);
                 if (point.X < b.Max.X && point.X > b.Min.X && point.Y < b.Max.Y && point.Y > b.Min.Y)
                 {
                     fSAelementOfHeaderStruct.GroupRevit = g;


                 }

             }
             */

             fSAelementOfHeaderStructs.Add(fSAelementOfHeaderStruct);
         }
         return fSAelementOfHeaderStructs;

     }


     /// <summary>
     /// Поиск футоров на активном виде (без списка на структурной схеме)
     /// </summary>
     /// <param name="doc"></param>
     /// <returns></returns>
     public List<FSAfooter> FootorFind(Document doc)
     {
         List<FSAfooter> fSAfootors = new List<FSAfooter>();
         if (doc.ActiveView is not ViewDrafting activeView)
             return fSAfootors;

         var elements_FamilyInstance = new FilteredElementCollector(doc, activeView.Id).ToElements()
             .OfType<FamilyInstance>()
             .Where(i => i.Name == Const.Element_Footor).ToList();
         foreach(var fi in elements_FamilyInstance)
         {
             FSAfooter fSAfootor = new FSAfooter(fi);
             fSAfootors.Add(fSAfootor);


         }
         return fSAfootors;

     }


     /// <summary>
     /// Создание групп элементов для футора 
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="fSAelementOfHeaderStructs"></param>
     /// <returns></returns>
     public void CreateGroupOnFootor(Document doc, ViewDrafting viewDrafting, FSAfooter footer)
     {

         using (Transaction tr = new Transaction(doc, $"Создание элементов футора"))
         {
             tr.Start();
             try
             {
                 var groupedHeaders = footer.FSAheaders
                     .Where(i => i.GroupRevit is not null)
                     .GroupBy(i => i.GroupRevit!.Id)
                     .ToList();
                 var ungroupedHeaders = footer.FSAheaders.Where(i => i.GroupRevit is null).ToList();

                 int index = 1; // Индекс создания
                 foreach (var header in groupedHeaders)
                 {
                     foreach (var h in header)
                     {
                         FSAfooterGroupedElements groupedElements = new FSAfooterGroupedElements(doc, viewDrafting, h, footer, ref index);
                         footer.FSAfooterGroupedElements.Add(groupedElements);
                     }
                 }

                 foreach (var header in ungroupedHeaders)
                 {
                     FSAfooterGroupedElements groupedElements = new FSAfooterGroupedElements(doc, viewDrafting, header, footer, ref index);
                     footer.FSAfooterGroupedElements.Add(groupedElements);
                 }

                 var footerInstance = doc.GetElement(footer.ID) as FamilyInstance
                     ?? throw new InvalidOperationException("Не найден экземпляр футора в документе.");
                 var lengthParameter = footerInstance.LookupParameter(Const.Param_NS_LinghtOfFooter)
                     ?? throw new InvalidOperationException($"У футора отсутствует параметр '{Const.Param_NS_LinghtOfFooter}'.");
                 if (lengthParameter.IsReadOnly)
                     throw new InvalidOperationException($"Параметр '{Const.Param_NS_LinghtOfFooter}' доступен только для чтения.");

                 lengthParameter.Set(index);
                 tr.Commit();
             }
             catch
             {
                 if (tr.GetStatus() == TransactionStatus.Started)
                     tr.RollBack();
                 throw;
             }
         }
         

     }


     /// <summary>
     /// Создание футора с элементами по структурной схеме  <br/> Группируем элементы структурной схемы по параметру Param_NS_Equpment
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="fSAelementOfHeaderStructs"></param>
     /// <returns></returns>
     public List<FSAfooter> FootorCreateByStruct(Document doc, List<FSAheader> fSAelementOfHeaderStructs)
     {
         
         List<FSAfooter> fSAfootors  = new List<FSAfooter>();
         XYZ begin = new XYZ(0, -0.8, 0);
         XYZ step = new XYZ(0, -160/304.8, 0);
         foreach (var fsastruct in fSAelementOfHeaderStructs.GroupBy(i => i.Group))// Группируем элементы структурной схемы по параметру Param_NS_Equpment
         {
             string group = fsastruct.Key; //Имя группы
             FSAfooter footor = FootorCreate(doc, begin); //Количество футоров совпадает с количеством групп
             footor.Group = group; //присваиваем группе футора то же имя что и на структурной схеме
             
             fSAfootors.Add(footor);
             begin = begin + step;
             foreach (FSAheader el in fsastruct.Take(fsastruct.Count())) //проходим по группированому списку элементов на структурной схеме
             {
                 footor.FSAheaders.Add(el); //добавляем к футеру в список его модели структурной схемы.
             }
         }

         return fSAfootors;

     }

     /// <summary>
     /// Создание футора с элементами по структурной схеме  <br/> Группируем элементы структурной схемы по параметру Param_NS_Equpment
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="fSAelementOfHeaderStructs"></param>
     /// <returns></returns>
     public List<FSAfooter> FootorCreateByStruct(Document doc, List<FSAheader> fSAelementOfHeaderStructs, List<XYZ> xyz)
     {
         List<FSAfooter> fSAfootors = new List<FSAfooter>();
         var groupedStructures = fSAelementOfHeaderStructs.GroupBy(i => i.Group).ToList();

         if (groupedStructures.Count != xyz.Count)
             throw new ArgumentException(
                 $"Количество точек размещения ({xyz.Count}) не совпадает с количеством групп ФСА ({groupedStructures.Count}).",
                 nameof(xyz));

         for (int count = 0; count < groupedStructures.Count; count++)
         {
             var fsastruct = groupedStructures[count];
             string group = fsastruct.Key;
             FSAfooter footor = FootorCreate(doc, xyz[count]);
             footor.Group = group;

             fSAfootors.Add(footor);
             foreach (FSAheader el in fsastruct)
                 footor.FSAheaders.Add(el);
         }

         return fSAfootors;

     }


     public void RenameStruct(Document doc, List<FSAheader> fSAelementOfHeaderStructs)
     {
                     
         foreach (var fsastruct in fSAelementOfHeaderStructs.GroupBy(i => i.Group))// Группируем элементы структурной схемы по параметру Param_NS_Equpment
         {
                             
             foreach (FSAheader el in fsastruct.Take(fsastruct.Count())) //проходим по группированому списку элементов на структурной схеме
             {
                 
             }
         }



     }


     /// <summary>
     /// Сознание футора в точке координат на активном виде IsPinned
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="XYZ"></param>
     /// <returns></returns>
     FSAfooter FootorCreate(Document doc, XYZ point)
     {
         if (doc.ActiveView is not ViewDrafting activeView)
             throw new InvalidOperationException("Футор можно создать только на чертежном виде.");

         var symbol = new FilteredElementCollector(doc)
             .OfClass(typeof(FamilySymbol))
             .Cast<FamilySymbol>()
             .FirstOrDefault(q => q.Name == Const.Element_Footor)
             ?? throw new InvalidOperationException($"Не найден тип семейства футора '{Const.Element_Footor}'.");

         using (Transaction tr = new Transaction(doc, "Создание футора"))
         {
             tr.Start();
             try
             {
                 if (!symbol.IsActive)
                 {
                     symbol.Activate();
                     doc.Regenerate();
                 }

                 var footerInstance = doc.Create.NewFamilyInstance(point, symbol, activeView);
                 tr.Commit();
                 return new FSAfooter(footerInstance);
             }
             catch
             {
                 if (tr.GetStatus() == TransactionStatus.Started)
                     tr.RollBack();
                 throw;
             }
         }
     }



     public static string GetDescription(Enum value)
     {
         return
             value
                 .GetType()
                 .GetMember(value.ToString())
                 .FirstOrDefault()
                 ?.GetCustomAttribute<DescriptionAttribute>()
                 ?.Description
             ?? value.ToString();
     }


 }