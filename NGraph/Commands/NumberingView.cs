using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using System.Text.RegularExpressions;
using NGraph.Core;
using NGraph.ViewModels;
using NGraph.Views;
using Nice3point.Revit.Toolkit.External;



namespace NGraph.Commands;

/// <summary>
///     Settings for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class NumberingView : ExternalCommand
{
    
    public override void Execute()
    {

    
        var viewModel = new NGraphNumberingSheetsViewModel();
        viewModel.Doc = Document;
        var viewWindow = new NGraphNumberingSheetsView(viewModel);
        viewWindow.ShowDialog();


        string nameparam = (viewWindow.CB_Param.SelectedItem as Parameter).Definition.Name;


        // 1. Выбираем виды
        int page = 1;
        int.TryParse(viewWindow.TB_Value.Text, out page); //Сквозная нумерация (квадратик вверху)
        int pageNumberSheet = 1; //Нумерация по графической части


        ICollection<ElementId> selectedElementId = UiDocument.Selection.GetElementIds();


        var sortedSelectedElementId = selectedElementId
            .OrderBy(i => NgContext._FindParameter(Document.GetElement(i),BuiltInParameter.SHEET_NUMBER).AsString() , new NaturalStringComparer());

    

        using (Transaction t = new Transaction(Document, "NumberingView"))
        {
            t.Start();
            try
            {


                foreach (ElementId e in sortedSelectedElementId)
                {

                    Helpers helpers = new Helpers();

                    Element element = Document.GetElement(e);

                    //element.FindParameter("CISP_Номер страницы для выпуска").Set(page.ToString());
                    NgContext._FindParameter(element,nameparam).Set(page.ToString());
                    NgContext._FindParameter(element,"Номер листа для выпуска").Set(pageNumberSheet.ToString());

                    page++;
                    pageNumberSheet++;


                }





                t.Commit();
            }
            catch
            {
                t.RollBack();
            }
        }


    
    }
    
    
    public class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            string[] partsX = Regex.Split(x.Replace(" ", ""), "([0-9]+)");
            string[] partsY = Regex.Split(y.Replace(" ", ""), "([0-9]+)");


            for (int i = 0; i < Math.Min(partsX.Length, partsY.Length); i++)
            {
                //Console.WriteLine(partsX[i] + " " + partsY[i]);
                if (i % 2 == 1) // Это числовая часть
                {
                    int numX = int.Parse(partsX[i]);
                    int numY = int.Parse(partsY[i]);
                    if (numX != numY) return numX.CompareTo(numY);
                }
                else // Это текстовая часть
                {
                    int stringCompare = partsX[i].CompareTo(partsY[i]);
                    if (stringCompare != 0) return stringCompare;
                }
            }
            return partsX.Length.CompareTo(partsY.Length); // Если один префикс короче другого
        }
    }
    // сравнение по длине строки
    class CustomStringComparer : IComparer<String>
    {
        public int Compare(string? x, string? y)
        {
            int xLength = x?.Length ?? 0; // если x равно null, то длина 0
            int yLength = y?.Length ?? 0;
            return xLength - yLength;
        }
    }
        
}