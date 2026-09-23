using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using NGraph.Core;
using Nice3point.Revit.Toolkit.External;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Diagnostics;
using Nice3point.Revit.Extensions.Runtime;

namespace NGraph.Commands;

/// <summary>
///  CrossLineWithElementsInViewByGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CrossLineWithElementsInViewByGraph : ExternalCommand
{
    
    public override void Execute()
    {

        //TaskDialog.Show("Выбор линии ", "Выбирете линию начала отсчета");

        //Выбираем оборудование
        
        OST_ElectricalEquipmentSelectionFilter electricalEquipmentSelectionFilter = new OST_ElectricalEquipmentSelectionFilter();
        var elements = Application.ActiveUIDocument.Selection.PickObjects(ObjectType.Element, electricalEquipmentSelectionFilter, "Выбирете элементы").Select(i => Application.ActiveUIDocument.Document.GetElement(i.ElementId)).OfType<Element>().ToList();


        //var elements = UiDocument.Selection.GetElementIds().Select(i => i.ToElement(Document));
        //TaskDialog.Show("Элементы", elements.Count().ToString());
        //   .Where(i => i.LookupParameter("Стиль линий").AsValueString().Contains("*NG*"));
        //var activeViewDrafting = Document.ActiveView;

        //Выбираем линиии - кабель 
        //var lines = UiDocument.Selection.GetElementIds().Select(i => i.ToElement(Document))
        //   .Where(i => i.LookupParameter("Стиль линий").AsValueString().Contains("*NG*"));
        //var activeViewDrafting = Document.ActiveView;

        var lineBegin = ElementId.InvalidElementId;
        OST_LinesSelectionFilter linesSelectionFilter = new OST_LinesSelectionFilter();
        int count = 0;
        while (count<2)
        {

            lineBegin = Application.ActiveUIDocument.Selection.PickObject(ObjectType.Element, linesSelectionFilter, "Укажите линию начала отсчета").ElementId;

            if (Application.ActiveUIDocument.Document.GetElement(lineBegin) is not DetailLine selectedLine)
                return;
            if (
                (selectedLine.GetAdjoinedCurveElements(0).Count == 1)
                &
                 (selectedLine.GetAdjoinedCurveElements(1).Count == 1)
                )
            {
                TaskDialog.Show("Ошибка ", "Укажитете первую линию");
                lineBegin = ElementId.InvalidElementId;
                count++;
            }
            else
            {
               

                break;
            }

        }


        //var lineBegin = Document.GetElement(new ElementId(1248253)).Id;
        if (lineBegin == ElementId.InvalidElementId) return;
        List<ElementId> list = new List<ElementId>();
        list.Add(lineBegin);
        var falag = true;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        
        while (falag)
        {

            if (Application.ActiveUIDocument.Document.GetElement(list.Last()) is not DetailLine lastDetailLine) break; //Последняя линия, добавленная в списке
            var connectedDetailLine0 = lastDetailLine.GetAdjoinedCurveElements(0).FirstOrDefault();
            var connectedDetailLine1 = lastDetailLine.GetAdjoinedCurveElements(1).FirstOrDefault();
            
            if (
                (list.Any(i=>i==connectedDetailLine0) & connectedDetailLine1==null)
                ||
                (list.Any(i => i == connectedDetailLine1) & connectedDetailLine0 == null)

                )
            {
                
                break;
                
            }
            
            if (connectedDetailLine0 != null && !list.Contains(connectedDetailLine0))
            {
                if (connectedDetailLine0 != null)
                    list.Add(connectedDetailLine0);
            }
            if (connectedDetailLine1 != null && !list.Contains(connectedDetailLine1))
            {
                if (connectedDetailLine1 != null)
                    list.Add(connectedDetailLine1);
            }
            



            if (stopwatch.ElapsedMilliseconds >= 10000) break;

        }
        

        //TaskDialog.Show("Выбор линии ", "Последовательность из "+ list.Count().ToString() + " отрезков");
        var lines = list.Select(i => Application.ActiveUIDocument.Document.GetElement(i)).OfType<CurveElement>().ToList();
        
        //Выбираем элементы
        /*
        var el1 = Document.GetElement(new ElementId(1247896));
        var el2 = Document.GetElement(new ElementId(1247897));
        var el3 = Document.GetElement(new ElementId(1247902));
        var el4 = Document.GetElement(new ElementId(1247898));
        var el5 = Document.GetElement(new ElementId(1247903));
        var el6 = Document.GetElement(new ElementId(1247904));
        var el7 = Document.GetElement(new ElementId(1247888));
        var el8 = Document.GetElement(new ElementId(1247887));
        var el9 = Document.GetElement(new ElementId(1247905));
        var el10 = Document.GetElement(new ElementId(1247899));
        var el11 = Document.GetElement(new ElementId(1247895));
        
        List<Element> elements = new List<Element> { el1,el2,el3,el4, el5, el6, el7, el8 , el9, el10, el11 };
        */
       


        //Для каждого элемента строим перпендикуляр (или находим ближайшее расстояние) для его центра до прямой и для концов прямой. Выбираем наименьшее
        //Запоминаем эту точку на прямой - это вершина. Т.е у линии может быть более двух вершин (уже виртуальных) и в них будет оборудование, которое с ней связано
       
        List<BigSegment> bigSegments = new List<BigSegment>();
        List<ElementInGraph> elementsInGrahp = new List<ElementInGraph>();
        
        foreach (CurveElement e in lines)
        {
            bigSegments.Add(new BigSegment(e.GeometryCurve));
        }
        
        foreach (Element e in elements)
        {
            elementsInGrahp.Add(new ElementInGraph(e, bigSegments));
        }


        //Добавляем точки на BigSegment. Ищем эти точки в тех бигсегментах, которые есть у элементов
        foreach (ElementInGraph e in elementsInGrahp)
        {
            if (e.NearestoBigSegment.Points.Any(i=> HaveSameXyz(i , e.NearestDistanceToCurve.ProjectedPoint,5)))
                continue;
            else
            {
                //Если точка не находится в списке прямой, то добавляем точку на нее, далее по ним будем строить сегмиенты
                e.NearestoBigSegment.Points.Add(e.NearestDistanceToCurve.ProjectedPoint);
            }
        }


        
        List<SegmentOfLine> segments = new List<SegmentOfLine>();
        //Создаем сегменты
        foreach (BigSegment b in bigSegments)
        {
            if (b.Points.Count != 2)
            {
                var sorted = b.Points.OrderBy(i => i.X).ThenBy(i => i.Y).ToList();
                for (int i = 0; i < sorted.Count-1; i++)
                {
                    SegmentOfLine segmentOfLine = new SegmentOfLine(sorted[i], sorted[i + 1], b);
                    segments.Add(segmentOfLine);
                    b.Segments.Add(segmentOfLine);
                }

            }
            else {
                SegmentOfLine segmentOfLine = new SegmentOfLine(b.Points.First(), b.Points.Last(),b);
                segments.Add(segmentOfLine);
                b.Segments.Add(segmentOfLine);
            }
        }


        GraphId graphOfLines_rev1 = new GraphId();
        Get_GraphOfLines(graphOfLines_rev1, segments);
        

        //Добавляем к вершинам оборудование
        foreach (ElementInGraph e in elementsInGrahp)
        {
            var v = GetVertexWithSamePoint(graphOfLines_rev1, e.NearestDistanceToCurve.ProjectedPoint)
                ?? throw new InvalidOperationException($"Не найдена вершина для элемента {e.Element.Id}.");
            v.Objects.Add(e);
            
        }



        //Выбираем линию по ElementId
        //var line = lines.Where(i => i.Id == new ElementId(1234762)).FirstOrDefault().Location as LocationCurve;
        if (Application.ActiveUIDocument.Document.GetElement(lineBegin) is not DetailLine firstLine) return;
        var curve = firstLine.GeometryCurve;
        var endpoints = new[]
        {
            GetVertexWithSamePoint(graphOfLines_rev1, curve.GetEndPoint(0)),
            GetVertexWithSamePoint(graphOfLines_rev1, curve.GetEndPoint(1))
        };
        var vertexId_begin = endpoints.OfType<VertexId>().FirstOrDefault(vertex => vertex.Edges.Count == 1);
        var vertexId_end = graphOfLines_rev1.Vertices.FirstOrDefault(vertex => vertex.Edges.Count == 1 && vertex != vertexId_begin);
        if (vertexId_begin is null || vertexId_end is null)
        {
            TaskDialog.Show("NGraph", "Не найдены начало и конец цепочки линий.");
            return;
        }

        GraphId.Dijkstra(graphOfLines_rev1, vertexId_begin, out var paths, out _);
        if (!paths.TryGetValue(vertexId_end, out var vertexIds) || vertexIds.Count == 0)
        {
            TaskDialog.Show("NGraph", "Между началом и концом цепочки нет пути.");
            return;
        }
        vertexIds.Insert(0, vertexId_begin);

        int j = 1;
        using (Transaction tx = new Transaction(Application.ActiveUIDocument.Document, "Нумерация по линии"))
        {
            tx.Start("Нумерация по линии");

            
            foreach (var v in vertexIds)
            {

                foreach (var e in v.Objects.OfType<ElementInGraph>())
                {

                    NgContext.RequireParameter(e.Element, "Имя панели").Set(j.ToString());

                    j++;
                }

            }

            


            tx.Commit();
        }


        //TaskDialog.Show("Результат ", "Последовательность из " + list.Count().ToString() + " отрезков" + "\n"+ "Пронумеровано "+ j.ToString() + " элементов" );







        /// <summary>
        /// Построение графа из списка линий 
        /// </summary>
        /// <param name="element">Линия с началом и концом </param>
        /// <returns></returns>
        void Get_GraphOfLines(GraphId graph, List<SegmentOfLine> element)
        {
            foreach (SegmentOfLine e in element) //Добавляем вершины
            {
                
                var pointsCurve = e.xYZs;
                var beginPointCurve = pointsCurve[0];
                var beginVertex = GetVertexWithSamePoint(graph, beginPointCurve);
                var endPointCurve = pointsCurve[1];
                var endVertex = GetVertexWithSamePoint(graph, endPointCurve);
                if (beginVertex == null && endVertex == null)
                {
                    VertexId v1 = new VertexId(beginPointCurve);
                    VertexId v2 = new VertexId(endPointCurve);
                    graph.Vertices.Add(v1);
                    graph.Vertices.Add(v2);
                    v1.Edges.Add(new EdgeId(v1, v2 , e.VirtualCurve));
                    v2.Edges.Add(new EdgeId(v2, v1, e.VirtualCurve));
                }
                else if (beginVertex != null && endVertex != null)
                {
                    beginVertex.Edges.Add(new EdgeId(beginVertex, endVertex, e.VirtualCurve));
                    endVertex.Edges.Add(new EdgeId(endVertex, beginVertex, e.VirtualCurve));
                }
                else if (beginVertex == null && endVertex != null)
                {
                    VertexId v1 = new VertexId(beginPointCurve);
                    graph.Vertices.Add(v1);
                    v1.Edges.Add(new EdgeId(v1, endVertex, e.VirtualCurve));
                    endVertex.Edges.Add(new EdgeId(endVertex, v1, e.VirtualCurve));
                }
                else if (beginVertex != null && endVertex == null)
                {
                    VertexId v2 = new VertexId(endPointCurve);
                    graph.Vertices.Add(v2);
                    beginVertex.Edges.Add(new EdgeId(beginVertex, v2, e.VirtualCurve));
                    v2.Edges.Add(new EdgeId(v2, beginVertex, e.VirtualCurve));
                }
            }
                           
        }


        
       
        
        VertexId? GetVertexWithSamePoint(GraphId graph, XYZ point)
        {
            VertexId? vertexId = graph.Vertices.Where(i => HaveSameXyz(i.Xyz, point, 1)).FirstOrDefault();

            return vertexId;
        }
        
        bool HaveEndPoint(Curve curve, VertexId v)
        {
            if (curve.Tessellate().ToList().Any(i => HaveSameXyz(i, v.Xyz, 1)))
            {
                return true;
            }
            else return false;
        }

        
        bool HaveSameXyz(XYZ xyz1, XYZ xyz2, int roundMm)
        {
        if (xyz1.X.ToMillimeters().Round(roundMm) == xyz2.X.ToMillimeters().Round(roundMm) && xyz1.Y.ToMillimeters().Round(roundMm) == xyz2.Y.ToMillimeters().Round(roundMm))
        {
            return true;
        }
        else return false; }

}
    
    

        /// <summary>
        /// Преддставляет виртуальную линию между элементами
        /// </summary>
        public class SegmentOfLine
        {
            public XYZ Point1 { get;}
            public XYZ Point2 { get; }
            public IList<XYZ> xYZs { get; } = [];

            public VirtualCurve VirtualCurve { get; }

            public BigSegment BigSegment { get; }

            public SegmentOfLine(XYZ point1, XYZ point2, BigSegment bigSegment)

            {
                BigSegment = bigSegment;
                Point1 = point1;
                Point2 = point2;
                xYZs.Add(point1);
                xYZs.Add(point2);
                VirtualCurve = new VirtualCurve(point1, point2);    



            }

        }


        /// <summary>
        /// Представление элемента в графе
        /// </summary>
        public class ElementInGraph
        {
            public Element Element { get;}

            public DistanceToCurve NearestDistanceToCurve { get; }
            public BigSegment NearestoBigSegment { get; }

            List<BigSegment> BigSegments { get; }
            public List<DistanceToCurve> DistancesToCurve { get; } = [];

            public ElementInGraph(Element element, List<BigSegment> bigSegments)
            {
                BigSegments = bigSegments;
                Element = element;

                foreach (BigSegment curve in BigSegments)
                {
                    DistanceToCurve distanceToCurve =  new DistanceToCurve(curve, element);
                    DistancesToCurve.Add(distanceToCurve);
                }

                NearestDistanceToCurve = DistancesToCurve.OrderBy(i => i.Distance).FirstOrDefault()
                    ?? throw new InvalidOperationException("Не выбраны линии для нумерации оборудования.");
                NearestoBigSegment = NearestDistanceToCurve.BigSegment;
            }


           


        }


        /// <summary>
        /// Описывает расстояние от элемента до кривой
        /// </summary>
        public class DistanceToCurve
        {
            public BigSegment BigSegment { get; }
           
            public Element Element { get; }
            public XYZ ProjectedPoint { get; }
            public double Distance { get; }

            public DistanceToCurve(BigSegment bigSegment, Element element)
            {
                BigSegment = bigSegment;
                Element = element;
                //Для каждой линии находим расстояние до элемента (по перпендикуляру и конечным точкам) . Перпендикуляр проверить на возможность построения к прямой
                // Выполняем проекцию
                var Projection = BigSegment.Curve.Project(element.GetPlacementPoint())
                    ?? throw new InvalidOperationException($"Не удалось спроецировать элемент {element.Id} на линию.");

                {
                    // Координаты точки проекции на линии
                    ProjectedPoint = Projection.XYZPoint;

                    // Расстояние от исходной точки до линии в мм.
                    Helpers helpers = new Helpers();
                    Distance = helpers.FeetToMillimeters(Projection.Distance);

                    // Параметр точки на кривой
                    //double parameter = Projection.Parameter;

                }

            }

           


        }
        /// <summary>
        /// Описывает расстояние от элемента до кривой
        /// </summary>
        public class BigSegment
        {
            public Curve Curve { get; }

            public List<XYZ> Points { get; } = [];

            public List<SegmentOfLine> Segments { get; set; } = [];

            public BigSegment(Curve curve)
            {
                Curve = curve;

                Points.AddRange(Curve.Tessellate());

            }


        }

        public class OST_ElectricalEquipmentSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element.Category.Name == "Электрооборудование")
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }
        }

        public class OST_LinesSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element is DetailLine)
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }
        }
    
}