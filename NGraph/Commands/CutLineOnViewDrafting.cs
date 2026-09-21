using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;
using NGraph.ViewModels;
using NGraph.Views;
using NGraph.Core;
using NGraph.Core.FunctionalScheme;
namespace NGraph.Commands;


/// <summary>
/// Определяет линию, которая пересекает элементы в точках
/// </summary>
class LineCrossing
{
    public List<XYZ> CutingLines { get; }
    public Curve Curve { get; }
    public GraphicsStyle GraphicsStyle { get; }
    public Element CurveElement { get; }
    public List<FamilyInstance> FamilyInstances { get; }
    public List<XYZ> XYZs { get; }
    public LineCrossing(Curve curve, List<FamilyInstance> familyInstances, List<XYZ> cutingLines, Element curveElement, GraphicsStyle graphicsStyle)
    {
        Curve = curve;
        FamilyInstances = familyInstances;
        CutingLines = cutingLines;
        CurveElement = curveElement;
        GraphicsStyle = graphicsStyle;

    }

}
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CutLineOnViewDrafting : ExternalCommand
{
    public override void Execute()
  {
      Helpers helpers = new Helpers();
      FSAmethods fSAmethods = new FSAmethods();

      #region 0. Get ActiveViewDrafting
      ViewDrafting activeViewDrafting = Document.ActiveView as ViewDrafting;
      #endregion

      
      List<FSAheader> equipments = fSAmethods.ReadHederStruct_inversible(Document, activeViewDrafting);//Элементы узлов
      List<FSAheader> cabel = fSAmethods.ReadHederStruct(Document, activeViewDrafting); //Зеленые точки

      using (Transaction tr = new Transaction(Document, $"LineSplit"))
      {

          tr.Start();
          try
          {
              var list = cabel.Where(i => i.FI.Pinned == false).Select(i => i.FI.Id).ToList();
              Document.Delete(list);

          }
          catch { }
          tr.Commit();

      }




      //Выбираем линиии - кабель 
      var lines = UiDocument.Selection.GetElementIds().Select(i => i.ToElement(Document))
      
          .Where(i => i.LookupParameter("Стиль линий").AsValueString().Contains("*NG*"));
      /*
      var collector = new FilteredElementCollector(Document, (Document.ActiveView as ViewDrafting).Id);
      var lines = collector.OfCategory(BuiltInCategory.OST_Lines).WhereElementIsNotElementType().ToElements()
         .Where(i => i.LookupParameter("Стиль линий").AsValueString().Contains("*NG*"))
      ;
      */
      //Находим экземпляры, которые пересекает линия для этого создадим словарь
      List<LineCrossing> LineCrossingsList = [];
      foreach (Element el in lines)
      {
          Curve line = (el.Location as LocationCurve).Curve;
          GraphicsStyle graphicsStyle = line.GraphicsStyleId.ToElement(Document) as GraphicsStyle;
          List<FamilyInstance> fi = new List<FamilyInstance>();
          List<XYZ> cutingLines = [];

          foreach (FSAheader equpent in equipments)
          {
              Options options = new Options() { View = activeViewDrafting, IncludeNonVisibleObjects = true, ComputeReferences = true }; //Настройки содержат невидимые элементы
              var geometry = equpent.FI.get_Geometry(options); //Получаем геометрию
              var filteredGeometry = geometry.Where(i => i.GetType() == typeof(Line)); //Нужны только линии
              var filteredGeometry_solid = geometry.Where(i => i.GetType() == typeof(Solid)); //Нужны только линии
              List<XYZ> xYZsFoEqupment = [];
              if (filteredGeometry.Any(i => line.Intersect((i as Curve)) == SetComparisonResult.Overlap)) //Если присутствуют пересечения линий
              {
                  fi.Add(equpent.FI);

                  
                  foreach (Line l in filteredGeometry)
                  {

                      IntersectionResultArray intersectionResultArray = [];
                      var m = line.Intersect((l as Curve), out intersectionResultArray);


                      if (intersectionResultArray != null)
                      {

                          foreach (var i in intersectionResultArray)
                          {
                              XYZ crossPoint = (i as IntersectionResult).XYZPoint;
                              xYZsFoEqupment.Add(crossPoint);
                          }
                      }
                  }



                  // 1. xYZsFoEqupment - Удалям одинаковые точки (значения duble сравнить до 9 разряда)
                  // 2. Если точка одна в списке - то это начало или конец для новой линии
                  // 3. Удаляем серединные точки если в списке их более 2х. Т.е
                  if (xYZsFoEqupment.Count >= 1)
                  {
                      //cutingLines.Add(xYZsFoEqupment.FirstOrDefault
                      var min = xYZsFoEqupment.OrderBy(i => (i.X, i.Y)).FirstOrDefault();
                      var max = xYZsFoEqupment.OrderBy(i => (i.X, i.Y)).LastOrDefault();
                      cutingLines.Add(min);
                      cutingLines.Add(max);
                  }
                  
              }






          }
          LineCrossing lc = new LineCrossing(line, fi, cutingLines,el, graphicsStyle);
          LineCrossingsList.Add(lc);


      }

      using (Transaction tr = new Transaction(Document, $"LineSplit"))
      {

          tr.Start();
          try
          {
              foreach (LineCrossing l in LineCrossingsList)
              {
                  l.CutingLines.Add(l.Curve.Tessellate()[0]);
                  l.CutingLines.Add(l.Curve.Tessellate()[1]);
                  var newlist = l.CutingLines.OrderBy(i => (i.X, i.Y)).ToList();


                  for (int i = 0; i < newlist.Count; i++)
                  {
                      CreateLine(newlist[i], newlist[i+1], activeViewDrafting, l.GraphicsStyle );
                      i++;
                  }
                  Document.Delete(l.CurveElement.Id);
              }
          }
          catch { }
          tr.Commit();

      }







  }

    /// <summary>
    /// Создание отрезка
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="XYZ_begin"></param>
    /// <param name="XYZ_end"></param>
    void CreateLine(XYZ XYZ_begin, XYZ XYZ_end, View viewDrafting, GraphicsStyle _gstyle)
    {
        //GraphicsStyle _gstyle = CreateLineStyle("grey_1_штрих", 1, new Autodesk.Revit.DB.Color(128, 128, 128), TypeOfLine.Сплошная);
        Line line1 = Line.CreateBound(XYZ_begin, XYZ_end);
        DetailCurve dc1 = Document.Create.NewDetailCurve(viewDrafting, line1); dc1.LineStyle = _gstyle;
     
    }


}