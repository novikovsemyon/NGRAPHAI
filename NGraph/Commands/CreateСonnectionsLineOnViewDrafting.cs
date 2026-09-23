using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using NGraph.Core;
using NGraph.Views;
using NGraph.ViewModels;
using NGraph.Core.FunctionalScheme;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Extensions.Runtime;

//using Autodesk.Revit.Creation;
namespace NGraph.Commands;

    /// <summary>
    ///     Создание чертежного вида на основании модели. Группирование элементов по параметрам (Рабочий набор/ADSK_Группирование/Уровень/Помещение/ADSK_Позиция/)
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class CreateСonnectionsLineOnViewDrafting : ExternalCommand
    {
        public override void Execute()
        {
            Helpers helpers = new Helpers();
            FSAmethods fSAmethods = new FSAmethods();
            
            #region 0. Get ActiveViewDrafting
            if (Application.ActiveUIDocument.ActiveView is not ViewDrafting activeViewDrafting)
            {
                TaskDialog.Show("NGraph", "Откройте чертёжный вид перед построением соединений.");
                return;
            }
            #endregion
            
            #region 1. Read shema FSA (Get list <FSAHeader>)
            List<FSAheader> equipment = fSAmethods.ReadHederStruct_inversible(Application.ActiveUIDocument.Document, activeViewDrafting);//Элементы узлов

            List<FSAheader> cabel = fSAmethods.ReadHederStruct(Application.ActiveUIDocument.Document, activeViewDrafting); //Зеленые точки

            //удаляем зеленые точки если они не закреплены

            using (Transaction tr = new Transaction(Application.ActiveUIDocument.Document, $"Удаляем зеленые точки если они не закреплены"))
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

            //



            var collector = new FilteredElementCollector(Application.ActiveUIDocument.Document, activeViewDrafting.Id);
            var lines = collector.OfCategory(BuiltInCategory.OST_Lines).WhereElementIsNotElementType().ToElements()
                    .OfType<CurveElement>().Where(i => i.LineStyle?.Name.Contains("*NG*") == true).Cast<Element>()
                ;

            #endregion 1.
            
            GraphId graphOfLines = new GraphId();
            ;
            Get_GraphOfLines(graphOfLines,lines.ToList(), activeViewDrafting, equipment); //Получаем граф из линий, активного вида, и сиска элементов узлов - оборудования
            Set_GraphOfLines(graphOfLines);//Разделяем на подграфы
            
            
            /*
            Данном контексте граф представляет из себя список кординат - вершин
            вершины (координаты) являются началом и концом для линии (кривой - Curve)
            Абстрактность состоят в том, что соединения между элементами выполняются прямыми аннотативными линиями
            Кабелем является зеленая точка
            Элементом - оборудованием будут остальные элементы узла.

            Расставляем зеленые точки по вершинам, начало и конец - вершины в подграфах с одним ребром.
            Для зеленой точки наследуем название от вершины по FI.
            Подграф может иметь вид -|- или ---
            */
            
            using (Transaction tr = new Transaction(Application.ActiveUIDocument.Document, $"Создаем зеленые точки заново"))
            {

                tr.Start();
                try
                {
                    int minus = 0;//Участвует в нумерации (вычитаем если есть неопределенность в графе, т.е. граф не используется
                    foreach (var g in graphOfLines.SubGraph)
                    {
                        var vertexEement = g.Value.Vertices.Where(i => i.Name != ElementId.InvalidElementId).ToList();//Список вершин с оборудованием
                        var vertexEement_ends = vertexEement.Where(i => i.Edges.Count == 1);//Список вершин с оборудованием  c одним ребром
                        var vertexEement_nodes = vertexEement.Where(i => i.Edges.Count > 1);//Список вершин с оборудованием узловые


                        var vertexEementOnBus = vertexEement.Where(i => i.Edges.Any(j => j.ElementOfCurve?.Pinned == true)).ToList();//Список вершин с оборудованием, которое находится на закрепленной кривой


                        //Если есть элемент в вершине на закрепленной кривой - то это начальная вершина шины, а граф представляет множество вершин
                        if (vertexEementOnBus.Any())
                        {
                            VertexId begin = vertexEementOnBus.First();
                            Element begin_element = Application.ActiveUIDocument.Document.GetElement(begin.Name);
                            //Выбираем остальные вершины
                            var ends = g.Value.Vertices.Where(i => (i.Name != ElementId.InvalidElementId) && (i != begin));
                            int numberCabel = g.Key- minus;
                            int numberInBus = 1;
                            foreach (VertexId end in ends)
                            {
                                var fi = TransactionCreateCabel(activeViewDrafting, begin, end, numberInBus, numberCabel);
                                numberInBus++;
                                
                                var needEdge = end.Edges.FirstOrDefault();
                                 if (needEdge is null) continue;
                                CreateMetkaByCenterEdgeId(activeViewDrafting, needEdge, fi);

                            }
                        }

                        //Если всего два элемента (две вершины с оборудованием) в графе
                        else if (vertexEement.Count()==2 )
                        {
                            VertexId begin = vertexEement.Last();
                            Element begin_element = Application.ActiveUIDocument.Document.GetElement(begin.Name);
                            VertexId end = vertexEement.First();
                            Element end_element = Application.ActiveUIDocument.Document.GetElement(end.Name);
                            
                            int numberCabel = g.Key- minus;
                            int numberInBus = 0;
                            var fi = TransactionCreateCabel(activeViewDrafting, begin, end, numberInBus, numberCabel);
                            //Маркировка
                            List<EdgeId> list = new List<EdgeId>();
                            foreach (var item in g.Value.Vertices)
                            {
                                list.AddRange(item.Edges);
                            }
                            //Ребро с максимальной длиной
                            var needEdge = list.Distinct().OrderBy(i => i.Length_mm).LastOrDefault();
                             if (needEdge is null) continue;
                            CreateMetkaByCenterEdgeId(activeViewDrafting, needEdge, fi);
                        }

                        else
                        {
                            minus += 1;
                        }

                    }


                }
                catch { }
                tr.Commit();

            }
            
            
            
        }
        
        
        FamilyInstance FICreateWithoutTransaction(Document doc, XYZ XYZ, ViewDrafting activeView, string elementName)
        {
    
            FamilyInstance fi = (doc.Create.NewFamilyInstance(XYZ, new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfType<FamilySymbol>().First(q => q.Name == elementName), activeView));
            return fi;
    

        }
        
        FamilyInstance TransactionCreateCabel(ViewDrafting activeViewDrafting, VertexId begin_vertexId, VertexId end_vertexId, int numberInBus, int numberCabel)
        {
            Element begin_element = Application.ActiveUIDocument.Document.GetElement(begin_vertexId.Name);
            Element end_element = Application.ActiveUIDocument.Document.GetElement(end_vertexId.Name);
            //Размещаем семейство зеленой точки
            FamilyInstance fi = FICreateWithoutTransaction(Application.ActiveUIDocument.Document, end_vertexId.Xyz, activeViewDrafting, Const.Element_Header_Users);

            //Назначение параметров
            string detailLineTypeName = end_vertexId.Edges.Select(edge => edge.ElementOfCurve)
                .OfType<CurveElement>().Select(curve => curve.LineStyle?.Name)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? throw new InvalidOperationException("У конечного элемента не найден стиль кабельной линии.");
            string marka = detailLineTypeName.TrimStart('*', 'N', 'G').Split('*')[0];
            string nets = detailLineTypeName.TrimStart('*', 'N', 'G').Split('*')[1];

            fi.LookupParameter(Const.Param_CJ_Begin).Set(begin_element.LookupParameter("NS_Имя панели").AsString());
            fi.LookupParameter(Const.Param_CJ_End).Set(end_element.LookupParameter("NS_Имя панели").AsString());
            fi.LookupParameter(Const.Param_CJ_Begin_eq).Set(begin_element.LookupParameter("ADSK_Наименование краткое").AsString());
            fi.LookupParameter(Const.Param_CJ_End_eq).Set(end_element.LookupParameter("ADSK_Наименование краткое").AsString());
            if (numberInBus == 0)
            {
                fi.LookupParameter(Const.Param_CJ_Number).Set(numberCabel.ToString());
            }
            else
            {
                fi.LookupParameter(Const.Param_CJ_Number).Set(numberCabel.ToString() + "." + numberInBus.ToString()); 
            }

            fi.LookupParameter(Const.Param_CJ_Mark).Set(marka);
            fi.LookupParameter(Const.Param_CJ_Nets).Set(nets);
            fi.LookupParameter(Const.Param_CJ_Work).Set(activeViewDrafting.Name);

            //Обнуляем второстепенные значения
            fi.LookupParameter(Const.Param_NS_ElementId).Set(" ");

            //Получаем расстояние между элементами
            int round = 5000;
            ElementId elementId_begin = RevitElementAccess.CreateId(long.Parse(begin_element.LookupParameter(Const.Param_NS_ElementId).AsString()));
            XYZ xyx_begin = (Application.ActiveUIDocument.Document.GetElement(elementId_begin)
                ?? throw new InvalidOperationException($"Не найден элемент модели {elementId_begin}.")).GetPlacementPoint();

            ElementId elementId_end = RevitElementAccess.CreateId(long.Parse(end_element.LookupParameter(Const.Param_NS_ElementId).AsString()));
            XYZ xyx_end = (Application.ActiveUIDocument.Document.GetElement(elementId_end)
                ?? throw new InvalidOperationException($"Не найден элемент модели {elementId_end}.")).GetPlacementPoint();
            int lenght_behind_two_point = Helpers.LenghtInt((int)(((Math.Abs(xyx_begin.X - xyx_end.X) + Math.Abs(xyx_begin.Y - xyx_end.Y) + Math.Abs(xyx_begin.Z - xyx_end.Z)) * 304.8)), round) / 1000;

            fi.LookupParameter(Const.Param_CJ_Lenght).Set(lenght_behind_two_point);
            return fi;
        }
        void CreateMetkaByCenterEdgeId(ViewDrafting activeViewDrafting, EdgeId needEdge, FamilyInstance fi)
        {
            //для маркировки
            TagMode tagMode = TagMode.TM_ADDBY_CATEGORY;
            TagOrientation tagorn = TagOrientation.Horizontal;
            var symId = new FilteredElementCollector(Application.ActiveUIDocument.Document).
                OfCategory(BuiltInCategory.OST_DetailComponentTags).
                WhereElementIsElementType().
                First(i => i.Name == "BE_Марка_Элемент_узла").Id;
            //Найдем центр ребра
            var Point1 = needEdge.From.Xyz;
            var Point2 = needEdge.To.Xyz;
            var Point = new XYZ
            (
                (Point1.X + Point2.X) / 2,
                (Point1.Y + Point2.Y) / 2,
                (Point1.Z + Point2.Z) / 2
            );

            Reference elRef = new Reference(fi);
            IndependentTag newTag = IndependentTag.Create(Application.ActiveUIDocument.Document, symId, activeViewDrafting.Id, elRef, false, tagorn, Point);


    
        }
        
        /// <summary>
        /// Построение графа из списка линий на чертежном виде
        /// </summary>
        /// <param name="element">Линия с началом и концом </param>
        /// <returns></returns>
        public void Get_GraphOfLines(GraphId graph, List<Element> element, ViewDrafting activeViewDrafting, List<FSAheader> fSAheaders)
        {
            foreach (CurveElement e in element.OfType<CurveElement>()) //Добавляем вершины
            {
                var curve = e.GeometryCurve;

                var pointsCurve = curve.Tessellate();

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
                    v1.Edges.Add(new EdgeId(v1, v2, e));
                    v2.Edges.Add(new EdgeId(v2, v1, e));
                }
                else if (beginVertex != null && endVertex != null)
                {
                    beginVertex.Edges.Add(new EdgeId(beginVertex, endVertex, e));
                    endVertex.Edges.Add(new EdgeId(endVertex, beginVertex, e));
                }
                else if (beginVertex == null && endVertex != null)
                {
                    VertexId v1 = new VertexId(beginPointCurve);
                    graph.Vertices.Add(v1);
                    v1.Edges.Add(new EdgeId(v1, endVertex, e));
                    endVertex.Edges.Add(new EdgeId(endVertex, v1, e));
                }
                else if (beginVertex != null && endVertex == null)
                {
                    VertexId v2 = new VertexId(endPointCurve);
                    graph.Vertices.Add(v2);
                    beginVertex.Edges.Add(new EdgeId(beginVertex, v2, e));
                    v2.Edges.Add(new EdgeId(v2, beginVertex, e));
                }
            }
            
            //Добавляем оборудование
            foreach(var v in graph.Vertices)
            {
                foreach (var eq in fSAheaders)
                {
                    if(Helpers.Contains(eq.FI.get_BoundingBox(activeViewDrafting), v.Xyz,false))
                    {
                        v.Name = eq.FI.Id;
                    }
                }
            }
            //Добавляем в граф мершины на закрепленных кривых
            //rev1---
            //Добавляем вершины и ребра на шины

            //получаем все ребра и находим закрепленные
            var ed = GraphId.GetAllEgesId(graph);//Все ребра
            var EdPinned = ed.Where(i => i.ElementOfCurve?.Pinned == true);
            //Ищем вершины на закрепленных ребрах
            foreach (EdgeId e in ed)//Ищем вершины по всем ребрам подграфа на его кривых
            {
                if (e.ElementOfCurve?.get_BoundingBox(activeViewDrafting) is not BoundingBoxXYZ boundingBoxXYZ) continue;
                foreach (var v in graph.Vertices)
                {
                    if (Helpers.Contains(boundingBoxXYZ, v.Xyz,false) && v.Equals(e.From) != true && v.Equals(e.To) != true)
                    //!!!!!!!!! Если нашли такую вершину => Связываем граф (создаем ребро c между этой вершиной и началом ребра)
                    {
                        VirtualCurve virtualCurve = new VirtualCurve(e.From.Xyz, v.Xyz);
                        EdgeId edgeToV = new EdgeId(e.From, v, virtualCurve);
                        EdgeId edgeFromV = new EdgeId(v, e.From, virtualCurve);
                        //Добавляем это ребро двум вершинам
                        e.From.Edges.Add(edgeToV);
                        v.Edges.Add(edgeFromV);
                    }
                }
            }
            //-----
        }
        /// <summary>
        /// Получить вершину с такимиже координатами (проверяем, есь ли вершина с такими координатами)
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        VertexId? GetVertexWithSamePoint(GraphId graph, XYZ point)
        {
            VertexId? vertexId = graph.Vertices.Where(i => HaveSameXYZ(i.Xyz, point, 1)).FirstOrDefault();
     
            return vertexId;
        }
        
        /// У кривой есть вершина (по координатам)
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        /// <summary>
        bool HaveEndPoint(Curve curve, VertexId v)
        {
            if (curve.Tessellate().ToList().Any(i => HaveSameXYZ(i,v.Xyz,1)))
            {
                return true;
            }
            else return false;
        }
        
        /// Точки лежат на плоскости в одной координате допуском в миллиметрах
        /// </summary>
        /// <param name="xyz1"></param>
        /// <param name="xyz2"></param>
        /// <param name="round_mm"></param>
        /// <returns></returns>
        bool HaveSameXYZ (XYZ xyz1, XYZ xyz2, int round_mm)
        {
            if (xyz1.X.ToMillimeters().Round(round_mm) == xyz2.X.ToMillimeters().Round(round_mm) && xyz1.Y.ToMillimeters().Round(round_mm) == xyz2.Y.ToMillimeters().Round(round_mm))
            {
                return true;
            }
            else return false;
        }

        public void Set_GraphOfLines(GraphId graphOfLines)
        {
            //Разделим граф на части SubGraph, которые не соединяются между собой
            Dictionary<int, GraphId> dic_graphOfLiness = [];
            int count = 1;

            var vertises = graphOfLines.Vertices;
            foreach (var vertex in graphOfLines.Vertices)
            {
                //Если от вершины v можно попасть к другой вершине, то они принадлежат одному графу.

                var dic = GraphId.Dijkstra(graphOfLines, vertex);
                var cutdic = dic.Where(i => i.Value < 2000000000 && i.Value >= 0);
          

          
          
                if (dic_graphOfLiness.Any(i => i.Value.Vertices.Any(j => cutdic.Any(k => k.Key == j))))
                {
                    continue;
                }
                else
                {
                    GraphId graph = new GraphId();
                    foreach (var i in cutdic)
                    {
                        graph.Vertices.Add(i.Key);
                    }


                    dic_graphOfLiness.Add(count, graph);
                    count++;
                }
          
            }
            graphOfLines.SubGraph = dic_graphOfLiness;

        }
    }
