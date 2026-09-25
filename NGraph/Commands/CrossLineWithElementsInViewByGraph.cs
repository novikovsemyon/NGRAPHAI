using Autodesk.Revit.Attributes;
using NGraph.Core;
using NGraph.Views;
using Nice3point.Revit.Toolkit.External;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows.Interop;
using Nice3point.Revit.Extensions.Runtime;

namespace NGraph.Commands;

/// <summary>
/// Нумерует выбранное электрооборудование вдоль цепочки линий с заданным форматом номера.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CrossLineWithElementsInViewByGraph : ExternalCommand
{
    
    public override void Execute()
    {
        try { ExecuteNumbering(); }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // Esc при выборе в Revit завершает команду до открытия транзакции.
        }
        catch (Exception ex)
        {
            TaskDialog.Show("NGraph — Нумератор по линии", "Не удалось выполнить нумерацию.\n" + ex.Message);
        }
    }

    private void ExecuteNumbering()
    {

        var elements = UiDocument.Selection.PickObjects(ObjectType.Element,
                new OST_ElectricalEquipmentSelectionFilter(), "Выберите оборудование и нажмите «Готово»")
            .Select(reference => Document.GetElement(reference.ElementId)).OfType<Element>()
            .GroupBy(element => element.Id).Select(group => group.First()).ToList();
        if (elements.Count == 0) return;

        var parameters = LineNumberingParameters.GetCommon(elements);
        if (parameters.Count == 0)
        {
            TaskDialog.Show("NGraph — Нумератор по линии",
                "У выбранного оборудования нет общих текстовых параметров экземпляра, доступных для записи.");
            return;
        }

        DetailLine firstLine;
        while (true)
        {
            var reference = UiDocument.Selection.PickObject(ObjectType.Element,
                new OST_LinesSelectionFilter(), "Укажите крайний отрезок — начало нумерации");
            firstLine = (DetailLine)Document.GetElement(reference.ElementId);
            if (firstLine.GetAdjoinedCurveElements(0).Count == 0 || firstLine.GetAdjoinedCurveElements(1).Count == 0)
                break;
            TaskDialog.Show("NGraph — Нумератор по линии", "Выберите крайний отрезок незамкнутой цепочки. Для отмены нажмите Esc.");
        }

        // Проходим цепочку один раз. Развилка или повтор линии не дают однозначного порядка.
        var lines = GetLineChain(firstLine);

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

        var orderedElements = vertexIds.SelectMany(vertex => vertex.Objects.OfType<ElementInGraph>())
            .Select(item => item.Element).ToList();
        if (orderedElements.Count != elements.Count || orderedElements.Select(e => e.Id).Distinct().Count() != elements.Count)
            throw new InvalidOperationException("Не удалось однозначно упорядочить всё выбранное оборудование. Проверьте цепочку линий и расположение элементов.");

        var window = new LineNumberingView(parameters, orderedElements.Count);
        new WindowInteropHelper(window).Owner = Application.MainWindowHandle;
        if (window.ShowDialog() != true || window.SelectedParameter is not { } choice || window.Options is not { } options)
            return;

        // Проверяем все назначения заранее. В транзакции выполняется только готовый план записи.
        var assignments = orderedElements.Select((element, index) => new
        {
            Element = element,
            Parameter = LineNumberingParameters.RequireWritable(element, choice),
            Value = options.Format(index)
        }).ToList();
        using (var transaction = new Transaction(Document, "Нумерация по линии"))
        {
            transaction.Start();
            foreach (var assignment in assignments)
            {
                // Set возвращает false и для уже совпадающего значения — это не ошибка.
                if (assignment.Parameter.AsString() == assignment.Value) continue;
                if (!assignment.Parameter.Set(assignment.Value))
                    throw new InvalidOperationException($"Revit отклонил значение «{assignment.Value}» для элемента {assignment.Element.Id}. Все изменения отменены.");
            }
            if (transaction.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException("Revit не подтвердил запись номеров.");
        }
        TaskDialog.Show("NGraph — Нумератор по линии", $"Пронумеровано элементов: {assignments.Count}.\nПараметр: {choice.Name}.");

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
        

        
        bool HaveSameXyz(XYZ xyz1, XYZ xyz2, int roundMm)
        {
        if (xyz1.X.ToMillimeters().Round(roundMm) == xyz2.X.ToMillimeters().Round(roundMm) && xyz1.Y.ToMillimeters().Round(roundMm) == xyz2.Y.ToMillimeters().Round(roundMm))
        {
            return true;
        }
        else return false; }

}
    
    

    private List<CurveElement> GetLineChain(DetailLine firstLine)
    {
        var lines = new List<CurveElement>();
        var visited = new HashSet<ElementId>();
        DetailLine? current = firstLine;
        ElementId? previous = null;
        while (current != null)
        {
            if (!visited.Add(current.Id))
                throw new InvalidOperationException("Цепочка линий замкнута. Выберите незамкнутую цепочку без развилок.");
            lines.Add(current);
            var end0 = current.GetAdjoinedCurveElements(0);
            var end1 = current.GetAdjoinedCurveElements(1);
            if (end0.Count > 1 || end1.Count > 1)
                throw new InvalidOperationException("У цепочки есть развилка. Для нумерации нужна одна незамкнутая цепочка.");
            var next = end0.Concat(end1).Where(id => id != previous).Distinct().ToList();
            if (next.Count > 1)
                throw new InvalidOperationException("Начало нумерации должно находиться на краю цепочки.");
            previous = current.Id;
            if (next.Count == 0) break;
            current = Document.GetElement(next[0]) as DetailLine
                ?? throw new InvalidOperationException("Цепочка должна состоять из прямых линий детализации.");
        }
        return lines;
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

                // Координаты точки проекции на линии и расстояние до неё в мм.
                ProjectedPoint = Projection.XYZPoint;
                Distance = Projection.Distance.ToMillimeters();

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
                return element.Category?.Id == new ElementId(BuiltInCategory.OST_ElectricalEquipment)
                    && element.Location is LocationPoint;
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
