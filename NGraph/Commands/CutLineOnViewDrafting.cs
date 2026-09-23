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
      if (Application.ActiveUIDocument.ActiveView is not ViewDrafting activeViewDrafting)
      {
          Autodesk.Revit.UI.TaskDialog.Show("NGraph", "Откройте чертёжный вид перед разрезанием линий.");
          return;
      }
      #endregion

      
      List<FSAheader> equipments = fSAmethods.ReadHederStruct_inversible(Application.ActiveUIDocument.Document, activeViewDrafting);//Элементы узлов
      List<FSAheader> cabel = fSAmethods.ReadHederStruct(Application.ActiveUIDocument.Document, activeViewDrafting); //Зеленые точки

      using (Transaction tr = new Transaction(Application.ActiveUIDocument.Document, $"LineSplit"))
      {

          tr.Start();
          try
          {
              var list = cabel.Where(i => i.FI.Pinned == false).Select(i => i.FI.Id).ToList();
              Application.ActiveUIDocument.Document.Delete(list);

          }
          catch { }
          tr.Commit();

      }




      //Выбираем линиии - кабель 
      var lines = Application.ActiveUIDocument.Selection.GetElementIds().Select(i => i.ToElement(Application.ActiveUIDocument.Document))
      
          .OfType<CurveElement>().Where(i => i.LineStyle?.Name.Contains("*NG*") == true);
      /*
      var collector = new FilteredElementCollector(Application.ActiveUIDocument.Document, (Application.ActiveUIDocument.Document.ActiveView as ViewDrafting).Id);
      var lines = collector.OfCategory(BuiltInCategory.OST_Lines).WhereElementIsNotElementType().ToElements()
         .OfType<CurveElement>().Where(i => i.LineStyle?.Name.Contains("*NG*") == true)
      ;
      */
      //Находим экземпляры, которые пересекает линия для этого создадим словарь
      List<LineCrossing> LineCrossingsList = [];
      foreach (CurveElement el in lines)
      {
          Curve line = el.GeometryCurve;
          if (el.LineStyle is not GraphicsStyle graphicsStyle) continue;
          List<FamilyInstance> fi = new List<FamilyInstance>();
          List<XYZ> cutingLines = [];

          foreach (FSAheader equpent in equipments)
          {
              Options options = new Options() { View = activeViewDrafting, IncludeNonVisibleObjects = true, ComputeReferences = true }; //Настройки содержат невидимые элементы
              var geometry = equpent.FI.get_Geometry(options); //Получаем геометрию
              var filteredGeometry = geometry.Where(i => i.GetType() == typeof(Line)); //Нужны только линии
              var filteredGeometry_solid = geometry.Where(i => i.GetType() == typeof(Solid)); //Нужны только линии
              List<XYZ> xYZsFoEqupment = [];
#if REVIT2026_OR_GREATER
              if (filteredGeometry.Any(i =>
                      line.Intersect((Curve)i, CurveIntersectResultOption.Simple).Result == SetComparisonResult.Overlap))
#else
              if (filteredGeometry.Any(i => line.Intersect((Curve)i) == SetComparisonResult.Overlap))
#endif
              // Если присутствуют пересечения линий
              {
                  fi.Add(equpent.FI);

                  
                  foreach (Line l in filteredGeometry)
                  {

#if REVIT2026_OR_GREATER
                      var intersection = line.Intersect(l, CurveIntersectResultOption.Detailed);
                      foreach (var overlapPoint in intersection.GetOverlaps())
                      {
                          xYZsFoEqupment.Add(overlapPoint.Point);
                      }
#else
                      IntersectionResultArray intersectionResultArray = [];
                      line.Intersect(l, out intersectionResultArray);

                      if (intersectionResultArray != null)
                      {
                          foreach (IntersectionResult intersectionResult in intersectionResultArray)
                          {
                              xYZsFoEqupment.Add(intersectionResult.XYZPoint);
                          }
                      }
#endif
                  }



                  // 1. xYZsFoEqupment - Удалям одинаковые точки (значения duble сравнить до 9 разряда)
                  // 2. Если точка одна в списке - то это начало или конец для новой линии
                  // 3. Удаляем серединные точки если в списке их более 2х. Т.е
                  if (xYZsFoEqupment.Count >= 1)
                  {
                      //cutingLines.Add(xYZsFoEqupment.FirstOrDefault
                      var min = xYZsFoEqupment.OrderBy(i => (i.X, i.Y)).First();
                      var max = xYZsFoEqupment.OrderBy(i => (i.X, i.Y)).Last();
                      cutingLines.Add(min);
                      cutingLines.Add(max);
                  }
                  
              }






          }
          LineCrossing lc = new LineCrossing(line, fi, cutingLines,el, graphicsStyle);
          LineCrossingsList.Add(lc);


      }

      using (Transaction tr = new Transaction(Application.ActiveUIDocument.Document, $"LineSplit"))
      {

          tr.Start();
          try
          {
              foreach (LineCrossing l in LineCrossingsList)
              {
                  l.CutingLines.Add(l.Curve.Tessellate()[0]);
                  l.CutingLines.Add(l.Curve.Tessellate()[1]);
                  var newlist = l.CutingLines.OrderBy(i => (i.X, i.Y)).ToList();


                  for (int i = 0; i + 1 < newlist.Count; i++)
                  {
                      CreateLine(newlist[i], newlist[i+1], activeViewDrafting, l.GraphicsStyle );
                      i++;
                  }
                  Application.ActiveUIDocument.Document.Delete(l.CurveElement.Id);
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
        DetailCurve dc1 = Application.ActiveUIDocument.Document.Create.NewDetailCurve(viewDrafting, line1); dc1.LineStyle = _gstyle;
     
    }


}