using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;
using System.IO;
using Autodesk.Revit.UI;
using NGraph.Core;
namespace NGraph.Commands;

/// <summary>
///     External command entry point.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class PrintTo : ExternalCommand
{
     //rev2

     public override void Execute()
     {
         
         string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

         var mydocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

         //using (StreamWriter writer = new StreamWriter(@"D:\Documents\Кабельный журнал.txt")) { }

         var version = Document.Application.VersionName.Remove(0,15);
         var titele = Document.Title;
         var date = DateTime.Today.ToShortDateString() +"_"+ DateTime.Now.Hour.ToString() + "." + DateTime.Now.Minute.ToString();

         //.SaveAs(@$"{mydocumentsPath}\{titele}_{date}_каб.журн.xlsx");

         string directiryPDF = @$"{mydocumentsPath}\Экспорт из REVIT\{version}\{titele}_{date}\pdf\";
         string directiryDWG = @$"{mydocumentsPath}\Экспорт из REVIT\{version}\{titele}_{date}\dwg\";
         Directory.CreateDirectory(directiryPDF);
         Directory.CreateDirectory(directiryDWG);

         ICollection<ElementId> selectedEl = UiDocument.Selection.GetElementIds();

         using (Transaction tr = new Transaction(Document, "Печать"))
         {
             tr.Start("Печать");
             

             PrintTo_PDF(selectedEl, directiryPDF);
             PrintTo_DWG(selectedEl, directiryDWG);


             tr.Commit();
         }

     }






     void PrintTo_PDF(ICollection<ElementId> selectedEl, string directiryPDF)
     {



         var stalePdf = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
             .FirstOrDefault(i => i.Contains("NSolution.pdf"));
         if (!string.IsNullOrWhiteSpace(stalePdf) && File.Exists(stalePdf))
             File.Delete(stalePdf);

         foreach (ElementId e in selectedEl)
         {

             Helpers helpers = new Helpers();

             if (Document.GetElement(e) is not ViewSheet sheet)
                 continue;

             var titleBlocks = new FilteredElementCollector(Document, e)
                 .OfClass(typeof(FamilyInstance))
                 .OfCategory(BuiltInCategory.OST_TitleBlocks)
                 .OfType<FamilyInstance>()
                 .Where(fi => fi.get_BoundingBox(sheet) is not null)
                 .ToList();

             if (titleBlocks.Count == 0)
                 continue;





             //Найдем самую наименьшую координату в списке элементов 
             //OrderBy(k => k.Value).First(). === MinBy
             var minInfi_es_X = titleBlocks.OrderBy(fi => fi.get_BoundingBox(sheet)!.Min.X).First();
             var minInfi_es_Y = titleBlocks.OrderBy(fi => fi.get_BoundingBox(sheet)!.Min.Y).First();


             //Начало в координатах листа
             XYZ begin = new XYZ(
                 minInfi_es_X.get_BoundingBox(sheet)!.Min.X,
                 minInfi_es_Y.get_BoundingBox(sheet)!.Min.Y,
                 0);

             //Относительно этих координат будем определять начало бумаги (полей принтера)
             //Эмпирическим путем для принтера определили коэффициент 
             // ===12=== ??? 
             double K = 12;


             string findedfile;
             string newfile;
             string concatname = "";


             foreach (var sheet_fi in titleBlocks)
             {
                 var boundingBox = sheet_fi.get_BoundingBox(sheet)!;
                 XYZ min = boundingBox.Min;
                 XYZ max = boundingBox.Max;

                 XYZ deltaFromBegin = min - begin;
                 double deltaToRight = deltaFromBegin.X * Math.Sign(deltaFromBegin.X) * -1 * K;
                 double deltaToUp = deltaFromBegin.Y * Math.Sign(deltaFromBegin.Y) * -1 * K;

                 if (sheet_fi != null)
                 {


                     double widthValue = Math.Round(helpers.FeetToMillimeters((max.X - min.X) * Math.Sign(max.X - min.X)));
                     double heightValue = Math.Round(helpers.FeetToMillimeters((max.Y - min.Y) * Math.Sign(max.Y - min.Y)));


                     PrintManager printmgr = Document.PrintManager;


                     printmgr.PrintSetup.CurrentPrintSetting = printmgr.PrintSetup.InSession;




                     printmgr.PrintSetup.SaveAs("newprint");
                     //printmgr.SelectNewPrintDriver("Bullzip PDF Printer");
                     // printmgr.SelectNewPrintDriver("PDF Writer - bioPDF");
                     //try { printmgr.SelectNewPrintDriver("Microsoft Print to PDF"); }
                     try { printmgr.SelectNewPrintDriver("PDF-XChange Standard"); }
                     //try { printmgr.SelectNewPrintDriver("PDF24"); }
                     catch
                     {
                         TaskDialog.Show("Ошибка", "Не найден принтер PDF-XChange Standard.");
                         return;
                     }

                     //catch { TaskDialog.Show("Ошибка", "Установите принтер Microsoft Print to PDF https://www.biopdf.com/download.php"); }

                     FilteredElementCollector col = new FilteredElementCollector(Document).OfClass(typeof(PrintSetting));

                     PrintSetting? set = null;

                     foreach (PrintSetting ps in col)
                     {
                         if (ps.Name == "newprint")
                         {
                             set = ps;
                             break;

                         }
                     }




                     //closing print settings
                     if (set is null)
                     {
                         TaskDialog.Show("Ошибка", "Не удалось создать временную настройку печати.");
                         return;
                     }

                     printmgr.PrintSetup.CurrentPrintSetting = set;



                     printmgr.Apply();

                     //Paper placement

                     //printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperPlacement = PaperPlacementType.Center;


                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperPlacement = PaperPlacementType.Margins;
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.MarginType = MarginType.UserDefined;

                     //Если надо сместить вправо А3 от 0,0,0 то надо задать UserDefinedMarginX = -;


                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.UserDefinedMarginX = deltaToRight;
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.UserDefinedMarginY = deltaToUp;





                     //zoom
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.ZoomType = ZoomType.Zoom;
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.Zoom = 100;

                     //page orientetion

                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PageOrientation =
                         widthValue >= heightValue
                             ? PageOrientationType.Landscape
                             : PageOrientationType.Portrait;

                     string paperSize = "";

                     if (heightValue + widthValue == 507) { paperSize = "A4"; }
                     else if (heightValue + widthValue == 717) { paperSize = "A3"; }
                     else if (heightValue + widthValue == 1014) { paperSize = "A2"; }
                     else if (heightValue + widthValue == 1435) { paperSize = "A1"; }
                     else if (heightValue + widthValue == 2030) { paperSize = "A0"; }
                     else
                     {
                         paperSize = "A0"; printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.ZoomType = ZoomType.FitToPage;
                     }

                     Autodesk.Revit.DB.PaperSize pSize = printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperSize;
                     PaperSizeSet paperSizeSet = printmgr.PaperSizes;




                     foreach (Autodesk.Revit.DB.PaperSize p in paperSizeSet)
                     {


                         if (p.Name.ToString() == paperSize)
                         {
                             printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperSize = p;
                         }

                     }

                     //string nameFilePDF = element.GetParameter(BuiltInParameter.SHEET_NUMBER).AsString();
                     string nameFilePDF = sheet.SheetNumber + "-" + sheet.Name;

                     var splitname = nameFilePDF.Split(Path.GetInvalidFileNameChars());

                     concatname = string.Concat(splitname);
                     string ptoFN = @$"{directiryPDF}{concatname}.pdf";

                     printmgr.PrintToFileName = ptoFN;
                     printmgr.PrintToFile = true;
                     printmgr.Apply();


                     //printmgr.PrintToFileName = @$"{directiryPDF}\1.pdf";

                     //System.Threading.Thread.Sleep(1000); //Важно успеть прочитать файл
                     //string path = @$"C:\\Users\\{userName}\\AppData\\Roaming\\PDF Writer\\PDF Writer - bioPDF\\settings.ini";
                     //INIManager manager = new INIManager(path);


                     //string name = manager.GetPrivateString("PDF Printer", "output");
                     //manager.WritePrivateString("PDF Printer", "output", @$"{directiryPDF}\{element.Name +"_"+ i.ToString()}.pdf");
                     //System.Threading.Thread.Sleep(1000); //Важно успеть прочитать файл
                     printmgr.PrintSetup.Save();
                     printmgr.PrintRange = PrintRange.Select;

                     ViewSet viewSet = new ViewSet();
                     viewSet.Insert(sheet);
                     printmgr.ViewSheetSetting.CurrentViewSheetSet.Views = viewSet;
                     //printmgr.ViewSheetSetting.SaveAs("tempViewSet");

                     printmgr.SubmitPrint(sheet);

                     printmgr.PrintSetup.Delete();










                     //printmgr.ViewSheetSetting.Delete();

                 }

             }

             ///2. отрегулировать длину пути метода Copy

             System.Threading.Thread.Sleep(5000);
             string mydocPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
             var files = Directory.GetFiles(mydocPath);
             ///Сцука принтер в имени файла точки заменяет на -
             findedfile = files.FirstOrDefault(i => i.Contains("NSolution")) ?? string.Empty;

             if (concatname.Count() > 100)
             {
                 concatname = concatname.Remove(100) + "..";
             }

             newfile = directiryPDF + concatname + ".pdf";


             if (!string.IsNullOrWhiteSpace(findedfile) && File.Exists(findedfile))
             {
                 File.Copy(findedfile, newfile, true);
                 File.Delete(findedfile);
             }

         }

     }


     /*
     
     void PrintTo_PDF(ICollection<ElementId> selectedEl, string directiryPDF)
     {

         foreach (ElementId e in selectedEl)
         {

             Helpers helpers = new Helpers();

             Element element = Document.GetElement(e);



             var fi_es = new FilteredElementCollector(Document, e)
                 .OfClass(typeof(FamilyInstance))
                 .OfCategory(BuiltInCategory.OST_TitleBlocks);




             //Найдем самую наименьшую координату в списке элементов 
             //OrderBy(k => k.Value).First(). === MinBy
             var minInfi_es_X = fi_es.OrderBy(fi => fi.get_BoundingBox(Document.GetElement(fi.OwnerViewId) as ViewSheet).Min.X).First() as FamilyInstance;
             var minInfi_es_Y = fi_es.OrderBy(fi => fi.get_BoundingBox(Document.GetElement(fi.OwnerViewId) as ViewSheet).Min.Y).First() as FamilyInstance;


             //Начало в координатах листа
             XYZ begin = new XYZ(
                 minInfi_es_X.get_BoundingBox(Document.GetElement(minInfi_es_X.OwnerViewId) as ViewSheet).Min.X,
                 minInfi_es_Y.get_BoundingBox(Document.GetElement(minInfi_es_X.OwnerViewId) as ViewSheet).Min.Y,
                 0);

             //Относительно этих координат будем определять начало бумаги (полей принтера)
             //Эмпирическим путем для принтера определили коэффициент 
             // ===12=== ??? 
             double K = 12;


             foreach (var item in fi_es)
             {
                 FamilyInstance sheet_fi = item as FamilyInstance;

                 XYZ min = sheet_fi.get_BoundingBox(Document.GetElement(sheet_fi.OwnerViewId) as ViewSheet).Min;
                 XYZ max = sheet_fi.get_BoundingBox(Document.GetElement(sheet_fi.OwnerViewId) as ViewSheet).Max;

                 XYZ deltaFromBegin = min - begin;
                 double deltaToRight = deltaFromBegin.X * Math.Sign(deltaFromBegin.X) * -1 * K;
                 double deltaToUp = deltaFromBegin.Y * Math.Sign(deltaFromBegin.Y) * -1 * K;

                 if (sheet_fi != null)
                 {


                     ElementId sheet_type_id = sheet_fi.GetTypeId();
                     ElementType sheet_type = Document.GetElement(sheet_type_id) as ElementType;


                     double widthValue = Math.Round(helpers.FeetToMillimeters((max.X - min.X) * Math.Sign(max.X - min.X)));
                     double heightValue = Math.Round(helpers.FeetToMillimeters((max.Y - min.Y) * Math.Sign(max.Y - min.Y)));


                     PrintManager printmgr = Document.PrintManager;


                     printmgr.PrintSetup.CurrentPrintSetting = printmgr.PrintSetup.InSession;




                     printmgr.PrintSetup.SaveAs("newprint");
                     //printmgr.SelectNewPrintDriver("Bullzip PDF Printer");
                     // printmgr.SelectNewPrintDriver("PDF Writer - bioPDF");
                     //try { printmgr.SelectNewPrintDriver("Microsoft Print to PDF"); }
                     try { printmgr.SelectNewPrintDriver("PDF-XChange Standard"); }
                     //try { printmgr.SelectNewPrintDriver("PDF24"); }
                     catch { TaskDialog.Show("Ошибка", "Установите принтер PDF-XCHANGE https://disk.yandex.ru/d/jaz9RA_RKtAIhw"); }

                     //catch { TaskDialog.Show("Ошибка", "Установите принтер Microsoft Print to PDF https://www.biopdf.com/download.php"); }

                     FilteredElementCollector col = new FilteredElementCollector(Document).OfClass(typeof(PrintSetting));

                     PrintSetting set = null;

                     foreach (PrintSetting ps in col)
                     {
                         if (ps.Name == "newprint")
                         {
                             set = ps;
                             break;

                         }
                     }




                     //closing print settings

                     printmgr.PrintSetup.CurrentPrintSetting = set;



                     printmgr.Apply();

                     //Paper placement

                     //printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperPlacement = PaperPlacementType.Center;


                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperPlacement = PaperPlacementType.Margins;
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.MarginType = MarginType.UserDefined;
                     double deltaX = helpers.MillimetersToFeet(420);
                     double deltaY = helpers.MillimetersToFeet(297);

                     //Если надо сместить вправо А3 от 0,0,0 то надо задать UserDefinedMarginX = -;


                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.UserDefinedMarginX = deltaToRight;
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.UserDefinedMarginY = deltaToUp;





                     //zoom
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.ZoomType = ZoomType.Zoom;
                     printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.Zoom = 100;

                     

                     //page orientetion

                     if (heightValue > widthValue)
                     {
                         printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PageOrientation = PageOrientationType.Landscape;

                     }

                     if (widthValue < heightValue)
                     {
                         printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PageOrientation = PageOrientationType.Portrait;
                     }

                     string paperSize = "";

                     if (heightValue + widthValue == 507) { paperSize = "A4"; }
                     else if (heightValue + widthValue == 717) { paperSize = "A3"; }
                     else if (heightValue + widthValue == 1014) { paperSize = "A2"; }
                     else if (heightValue + widthValue == 1435) { paperSize = "A1"; }
                     else if (heightValue + widthValue == 2030) { paperSize = "A0"; }
                     else
                     {
                         paperSize = "A0"; printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.ZoomType = ZoomType.FitToPage;
                     }

                     Autodesk.Revit.DB.PaperSize pSize = printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperSize;
                     PaperSizeSet paperSizeSet = printmgr.PaperSizes;


                     foreach (Autodesk.Revit.DB.PaperSize p in paperSizeSet)
                     {
                         if (p.Name.ToString() == paperSize)
                         {
                             printmgr.PrintSetup.CurrentPrintSetting.PrintParameters.PaperSize = p;
                         }

                     }

                     //string nameFilePDF = element.GetParameter(BuiltInParameter.SHEET_NUMBER).AsString();
                     string nameFilePDF = element.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString() + "-" + element.get_Parameter(BuiltInParameter.SHEET_NAME).AsString();





                     printmgr.PrintToFileName = @$"{directiryPDF}\{string.Concat(nameFilePDF.Split(Path.GetInvalidFileNameChars()))}.pdf";
                     printmgr.PrintToFile = true;
                     printmgr.Apply();

                     //printmgr.PrintToFileName = @$"{directiryPDF}\1.pdf";

                     //System.Threading.Thread.Sleep(1000); //Важно успеть прочитать файл
                     //string path = @$"C:\\Users\\{userName}\\AppData\\Roaming\\PDF Writer\\PDF Writer - bioPDF\\settings.ini";
                     //INIManager manager = new INIManager(path);


                     //string name = manager.GetPrivateString("PDF Printer", "output");
                     //manager.WritePrivateString("PDF Printer", "output", @$"{directiryPDF}\{element.Name +"_"+ i.ToString()}.pdf");
                     //System.Threading.Thread.Sleep(1000); //Важно успеть прочитать файл
                     printmgr.PrintSetup.Save();
                     Autodesk.Revit.DB.ViewSheet vs = element as Autodesk.Revit.DB.ViewSheet;

                     printmgr.PrintRange = PrintRange.Select;

                     ViewSet viewSet = new ViewSet();
                     viewSet.Insert(vs);
                     printmgr.ViewSheetSetting.CurrentViewSheetSet.Views = viewSet;
                     //printmgr.ViewSheetSetting.SaveAs("tempViewSet");

                     printmgr.SubmitPrint(vs);

                     printmgr.PrintSetup.Delete();
                     //printmgr.ViewSheetSetting.Delete();

                 }







             }
         }

     }
     */
     void PrintTo_DWG(ICollection<ElementId> selectedEl, string directiryDWG)
     {
         int iteratorDWG = 0;
         foreach (ElementId e in selectedEl)
         {
             if (Document.GetElement(e) is not ViewSheet sheet)
                 continue;

             DWGExportOptions dwgOption = new DWGExportOptions();
             ExportDWGSettings dWGSettings = ExportDWGSettings.Create(Document, "export");
             try
             {
                 dwgOption = dWGSettings.GetDWGExportOptions();
                 dwgOption.Colors = ExportColorMode.TrueColorPerView;
                 dwgOption.FileVersion = ACADVersion.R2018;
                 dwgOption.MergedViews = true;
                 dwgOption.UseHatchBackgroundColor = false;

                 List<ElementId> sheetIds = new List<ElementId> { e };
                 string nameFile = sheet.SheetNumber + "-" + sheet.Name;
                 string baseName = string.Concat(nameFile.Split(Path.GetInvalidFileNameChars()));
                 string exportName = baseName;

                 while (File.Exists(Path.Combine(directiryDWG, exportName + ".dwg")))
                 {
                     iteratorDWG++;
                     exportName = baseName + "_" + iteratorDWG;
                 }

                 Document.Export(directiryDWG, exportName, sheetIds, dwgOption);
                 iteratorDWG = 0;
             }
             finally
             {
                 Document.Delete(dWGSettings.Id);
             }
         }
     }
 }


