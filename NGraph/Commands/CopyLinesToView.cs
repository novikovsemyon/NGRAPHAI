using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;
using NGraph.Core.FunctionalScheme;
using NGraph.Core;
namespace NGraph.Commands;

/// <summary>
///     External command entry point.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CopyLinesToView : ExternalCommand
{
          public override void Execute()
      {
          Helpers helpers = new Helpers();
          FSAmethods fSAmethods = new FSAmethods();

          #region 0. Get ActiveViewDrafting
          ViewDrafting activeViewDrafting = Document.ActiveView as ViewDrafting;
          #endregion


          var vds = UiDocument.Selection.GetElementIds();
          var vd_First = vds.FirstOrDefault().ToElement(Document) as ViewDrafting;
          var vd_Last = vds.LastOrDefault().ToElement(Document) as ViewDrafting;

          List<FSAheader> equipments = fSAmethods.ReadHederStruct_inversible(Document, vd_First);//Элементы узлов

          List<FSAheader> cabel = fSAmethods.ReadHederStruct(Document, vd_First); //Зеленые точки

          //Выбираем линиии - кабель 
          var lines = Document.GetElements(vd_First.Id)

              .Where(i=>i.GetType()==typeof(DetailLine))

              .Where(i => i.LookupParameter("Стиль линий").AsValueString().Contains("*NG*"));
          /*
          var collector = new FilteredElementCollector(Document, (Document.ActiveView as ViewDrafting).Id);
          var lines = collector.OfCategory(BuiltInCategory.OST_Lines).WhereElementIsNotElementType().ToElements()
             .Where(i => i.LookupParameter("Стиль линий").AsValueString().Contains("*NG*"))
          ;
          */
          //Находим экземпляры, которые пересекает линия для этого создадим словарь

         


          using (Transaction tr = new Transaction(Document, $"LineSplit"))
          {

              tr.Start();
              try
              {
                  foreach (Element l in lines)
                  {
                      
                      Curve c = (l.Location as LocationCurve).Curve;
                      DetailCurve dc1 = Document.Create.NewDetailCurve(vd_Last, c);
                      dc1.LineStyle = c.GraphicsStyleId.ToElement(Document) as GraphicsStyle;

                      if(l.Pinned == true)
                      {
                          dc1.Pinned = true;
                      }


                  }






              }
              catch { }
              tr.Commit();

          }







      }
}