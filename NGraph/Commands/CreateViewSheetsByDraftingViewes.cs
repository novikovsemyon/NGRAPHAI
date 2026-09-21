using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;
using NGraph.Core;
namespace NGraph.Commands;

/// <summary>
///     External command entry point.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CreateViewSheetsByDraftingViewes : ExternalCommand
{
    
        public override void Execute()
        {

            ///  1. Выбираем чертежные виды
            ///  2. Получаем из них (габариты и список таблиц выбора AnnotationSymbol/Таблица применимости/ CISP_УГО_Список_шапка / CISP_УГО_Список
            ///  GetTypeId / AnnotationSymbolType / ElementType / FamilyName
            ///  Создаем лист
            ///  Задаем ему параметры (имя с постфиксом и размеры) Передаем ему чертежный вид.
            

            // 1. Выбираем чертежные виды

            ICollection<ElementId> selectedElementId = UiDocument.Selection.GetElementIds();


            int int_pageNumberSheet = 1;
            string prefix = "5.10.";
            string korpus = "СТ. ";
            string Номер_проекта = "СЕ-2024/02-ИОС5.4";

            foreach (ElementId e in selectedElementId)
            {

                Helpers helpers = new Helpers();

                Element element = Document.GetElement(e);


                var fi_es = new FilteredElementCollector(Document, e)
                    .OfClass(typeof(FamilyInstance))
                //    .OfType<AnnotationSymbolType>(
                    .Where(i => i.GetType() == typeof(AnnotationSymbol) && (i as FamilyInstance).Symbol.FamilyName == "CISP_УГО_Список");
                //.OfCategory(BuiltInCategory.OST_TitleBlocks); Листы

                string nameViewSheets = "";

                foreach (var fi in fi_es)
                {
                    var nameFAS = NgContext._FindParameter((fi as AnnotationSymbol),"FAS_Установка").AsString();
                    nameViewSheets = nameViewSheets + nameFAS + ", ";


                }

                string cutNameViewSheets = nameViewSheets.Remove(nameViewSheets.Length - 2);
                string adj = "";
                if (fi_es.Count() == 1) { adj = "а "; } else { adj = "и "; }
                var newNameViewSheets = "";
                if (cutNameViewSheets.Length > 150) //Если по длине не влезает описапние в штамп (200 символов для шрифта 2мм и 180 символов для шрифта 2.5мм)
                {
                    
                    newNameViewSheets = korpus + "Установк" + adj + GetFirstWord(cutNameViewSheets) +"..."+ GetLastWord(cutNameViewSheets) + ". Функциональная схема автоматизации.";

                }
                else
                {
                    newNameViewSheets = korpus + "Установк" + adj + cutNameViewSheets + ". Функциональная схема автоматизации.";
                }

                //TaskDialog.Show("ИМЯ", newstring);



                //(element as ViewDrafting).get_BoundingBox

                var collector = new FilteredElementCollector(Document);
                    string date = "12.25";
                    string Разработал = "Мигонькина";
                    string Проверил = "Новиков";
                    string ГлСпец = "Горячев";
                    string НормоКнотр = "Ерошкин";
                    string ГИП = "Коренной";
                    string[] family = [Разработал, Проверил, ГлСпец, НормоКнотр, ГИП];
                    Element[] elFamily = [
                                 collector.OfCategory(BuiltInCategory.OST_GenericAnnotation).WhereElementIsElementType().ToElements()
                             .Where(i => i.Name == Разработал).FirstOrDefault(),
                             collector.OfCategory(BuiltInCategory.OST_GenericAnnotation).WhereElementIsElementType().ToElements()
                             .Where(i => i.Name == Проверил).FirstOrDefault(),
                             collector.OfCategory(BuiltInCategory.OST_GenericAnnotation).WhereElementIsElementType().ToElements()
                             .Where(i => i.Name == ГлСпец).FirstOrDefault(),
                             collector.OfCategory(BuiltInCategory.OST_GenericAnnotation).WhereElementIsElementType().ToElements()
                             .Where(i => i.Name == НормоКнотр).FirstOrDefault(),
                             collector.OfCategory(BuiltInCategory.OST_GenericAnnotation).WhereElementIsElementType().ToElements()
                             .Where(i => i.Name == ГИП).FirstOrDefault()

                    ];





                    // ViewSheet viewSheet = ViewSheet.Create(Document, fs.Id);

                    //var annotation = collector.OfCategory(BuiltInCategory.OST_GenericAnnotation).WhereElementIsElementType().ToElements()
                    //.Where(i => i.Name == "Новиков").FirstOrDefault();
                    //.OfCategory(BuiltInCategory.OST_TitleBlocks); Листы


                    string pageNumberSheet = prefix + int_pageNumberSheet.ToString();
                    int_pageNumberSheet++;

                    CreateSheetView(Document, element as ViewDrafting, newNameViewSheets, elFamily, date, pageNumberSheet, Номер_проекта);








                }





        }

        double MtoF(double mm)
        {
            double a = mm;
            return a / 304.8;
        }
        
        double FtoMM(double feet)
        {
            double a = feet;
            return a * 304.8;
        }


        public static string GetFirstWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int index = text.IndexOf(' ');
            return index == -1 ? text : text.Substring(0, index);
        }
        public static string GetLastWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int index = text.LastIndexOf(' ');
            return index == -1 ? text : text.Substring(index);
        }


        /// <summary>
        /// Определение координат середины полезной области листа.
        /// </summary>
        /// <param name="paperSize"> 0,1, 2, 3</param>
        /// <param name="PageOrientation">0- альбомный,1-книжный,(искл.2 оверсайз в альбомном режиме)</param>
        /// <returns></returns>
        XYZ getCentroidBySheet(int paperSize, int PageOrientation)
        {
            Helpers helpers = new Helpers();
            XYZ xYZ = new XYZ();

            switch (PageOrientation) //Масштаб
            {
                case 0:  
                    switch (paperSize)
                    {
                        case 0:
                            xYZ = new XYZ(MtoF(-587), MtoF(448), 0);
                            break;
                        case 1:
                            xYZ = new XYZ(MtoF(-413), MtoF(325), 0);
                            break;
                        case 2:
                            xYZ = new XYZ(MtoF(-290), MtoF(238), 0);
                            break;
                        case 3:
                            xYZ = new XYZ(MtoF(-203), MtoF(176), 0);
                            break;



                    }
                    break;
                case 1:
                    switch (paperSize)
                    {
                        case 0:
                            xYZ = new XYZ(MtoF(-413), MtoF(622), 0);
                            break;
                        case 1:
                            xYZ = new XYZ(MtoF(-290), MtoF(448), 0);
                            break;
                        case 2:
                            xYZ = new XYZ(MtoF(-203), MtoF(325), 0);
                            break;
                        case 3:
                            xYZ = new XYZ(MtoF(-141), MtoF(238), 0);
                            break;



                    }
                    break;
                case 2:
                    switch (paperSize)
                    {
                        case 0:
                            xYZ = new XYZ(MtoF(-834), MtoF(595), 0);
                            break;
                        



                    }
                    break;
            }

            return xYZ;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="document"></param>
        /// <param name="viewDrafting"></param>
        /// <param name="newNameViewSheets"></param>
        /// <param name="elFamily"></param>
        private void CreateSheetView(Autodesk.Revit.DB.Document document, ViewDrafting viewDrafting, string newNameViewSheets , Element[] elFamily, string date, string pageNumberSheet, string Номер_проекта)
        {

           
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FamilySymbol));
            collector.OfCategory(BuiltInCategory.OST_TitleBlocks);

            FamilySymbol fs = collector.Where(i=>((i as FamilySymbol).FamilyName == "00_ОсновнаяНадписьФорма3_ГОСТ21-1101-2013") && (i as FamilySymbol).Name =="Форма 3").FirstOrDefault() as FamilySymbol;
            if (fs != null)
            {
                using (Transaction t = new Transaction(document, "Create a new ViewSheet"))
                {
                    t.Start();
                    try
                    {
                        // Новый лист
                        ViewSheet viewSheet = ViewSheet.Create(document, fs.Id);
                        if (null == viewSheet)
                        {
                            throw new Exception("Failed to create new ViewSheet.");
                        }

                        var fi_es = new FilteredElementCollector(Document, viewSheet.Id)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_TitleBlocks).FirstOrDefault();

                        Helpers helpers = new Helpers();

                        int PageOrientation = 0; //0- Означает альбомный лист, 1- Книжный
                        int paperSize = 0;
                        


                        double DistanceFromX = FtoMM(Math.Abs((viewDrafting.Outline.Max.U)- (viewDrafting.Outline.Min.U))); //Размер чертежа по Х
                        double DistanceFromY = FtoMM(Math.Abs((viewDrafting.Outline.Max.V) - (viewDrafting.Outline.Min.V))); //Размер чертежа по Y
                        double sumDistance = DistanceFromX+DistanceFromY;

                        double devide = DistanceFromX / DistanceFromY;
                        double sq = DistanceFromX * DistanceFromY;

                        //В определение размера фрмат А4 не реализован (посчитал, что не нужно)

                        // Внутренний габарит чертежа с учетом рамки и штампа (полезная область, которую занимает чертежный вид на листе)
                           


                        if (devide >= 1) // Альбомная
                        {
                            PageOrientation = 0;


                            ///* Для альбомной ориентации
                            ///*А3 = 395 + 232 = 627
                            ///* А2 = 569 + 355 = 924
                            ///* А1 = 816 + 529 = 1345
                            ///* А0 = 1164 + 776 = 1940

                            if (DistanceFromX <= 395)
                            {
                                if (DistanceFromY < 232) paperSize = 3;
                                else paperSize = 2;
                            }
                            else if (DistanceFromX <= 569 & DistanceFromX > 395)
                            {
                                if (DistanceFromY < 355) paperSize = 2;
                                else paperSize = 1;
                            }

                            else if (DistanceFromX <= 816 & DistanceFromX > 569)
                            {
                                if (DistanceFromY < 529) paperSize = 1;
                                else paperSize = 0;
                            }
                            else if (DistanceFromX <= 1164 & DistanceFromX > 816)
                            {
                                if (DistanceFromY < 776) paperSize = 0;
                                else { paperSize = 0; PageOrientation = 2; }
                            }
                            else 
                            {
                                paperSize = 0; PageOrientation = 2; //Иначе оверсайз 2А0 (Параметры листа: Формат А = 0, Множитель = 2)

                            }
                            
                            

                        }
                        else
                        {
                            PageOrientation = 1; //Книжная
                            ///** Для книжной ориентации результат тотже
                            ///*А3 = 272 + 355 = 627
                            ///* А2 = 395 + 529 = 924
                            ///* А1 = 569 + 776 = 1345
                            ///* А0 = 816 + 1124 = 1940
                            if (DistanceFromY <= 355)
                            {
                                if (DistanceFromX < 272) paperSize = 3;
                                else paperSize = 2;
                            }
                            else if (DistanceFromY <= 529 & DistanceFromY > 355)
                            {
                                if (DistanceFromX < 395) paperSize = 2;
                                else paperSize = 1;
                            }

                            else if (DistanceFromY <= 776 & DistanceFromY > 529)
                            {
                                if (DistanceFromX < 569) paperSize = 1;
                                else paperSize = 0;
                            }

                            else
                            {
                                paperSize = 0; //Иначе оверсайз 2А0 (Параметры листа: Формат А = 0, Множитель = 2)

                            }






                        }
                        NgContext._FindParameter(fi_es,"Формат А").Set(paperSize);
                        NgContext._FindParameter(fi_es,"Множитель").Set(PageOrientation);

                        //Лист определили, теперь определим координату куда ставить чертежный вид на лист. Координата зависит от  PageOrientation и paperSize (по уму надо бы класс создать с этими значениями, который в к конструкторе это вычисляет)
                        //метод определения координат середины полезной области листа.


                        XYZ viewSheetCentroid = getCentroidBySheet(paperSize,PageOrientation);

                         
                        //Размещение чертежного вида на листе (На листе вид находится в классе Viewport)
                        var newViewport = Viewport.Create(document, viewSheet.Id, viewDrafting.Id, viewSheetCentroid);
                        


                        NgContext._FindParameter(viewSheet,"ADSK_Штамп Строка 1 фамилия").Set("Мигонькина");
                        NgContext._FindParameter(viewSheet,"ADSK_Штамп Строка 2 фамилия").Set("Новиков");
                        NgContext._FindParameter(viewSheet,"ADSK_Штамп Раздел проекта").Set(Номер_проекта);
                        NgContext._FindParameter(viewSheet,BuiltInParameter.SHEET_NAME).Set(newNameViewSheets);
                        NgContext._FindParameter(viewSheet,BuiltInParameter.SHEET_NUMBER).Set(pageNumberSheet);
                        NgContext._FindParameter(viewSheet,BuiltInParameter.SHEET_ISSUE_DATE).Set(date);

                        //ElementId elid = new ElementId(894848); //Новиков
                        NgContext._FindParameter(fi_es,"Штамп.Подпись1").Set(elFamily[0].Id);
                        NgContext._FindParameter(fi_es,"Штамп.Подпись2").Set(elFamily[1].Id);
                        NgContext._FindParameter(fi_es,"Штамп.Подпись3").Set(elFamily[2].Id);
                       // fi_es.FindParameter("Штамп.Подпись4").Set(elFamily[---].Id);
                       NgContext._FindParameter(fi_es,"Штамп.Подпись5").Set(elFamily[3].Id);
                       NgContext._FindParameter(fi_es,"Штамп.Подпись6").Set(elFamily[4].Id);






                        

                        t.Commit();
                    }
                    catch
                    {
                        t.RollBack();
                    }
                }
            }
        }

}