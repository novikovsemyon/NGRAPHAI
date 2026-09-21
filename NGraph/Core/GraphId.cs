using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB;


//using static Autodesk.Revit.DB.SpecTypeId;


namespace NGraph.Core
{
    //========================================In Revit for ElementId======================================================//
    /// <summary>
    /// Virtual VertexId and EdgeId in Revit for DuctSystems (using GraphIdFromSystem method)
    /// </summary>
    public class GraphId
    {
        /// <summary>
        /// List of Vertices
        /// </summary>
        public List<VertexId> Vertices { get; }
        /// <summary>
        /// Revit for DuctSystems (fore project has more than one duct system)
        /// </summary>
        static public MEPSystem MEPSystem { get; private set; }
        /// <summary>
        /// Construction graph
        /// </summary>
        public GraphId()
        {
            Vertices = new List<VertexId>();
        }
        public Dictionary<int, GraphId> SubGraph { get; set; }




        /// <summary>
        /// Разделям граф на подграфы.
        /// </summary>
        /// <param name="graphOfLines"></param>
        public void set_GraphOfLines(GraphId graphOfLines)
        {
            //Разделим граф на части SubGraph, которые не соединяются между собой
            Dictionary<int, GraphId> dic_graphOfLiness = [];
            int count = 1;
            foreach (var v in graphOfLines.Vertices)
            {
                //Если от вершины v можно попасть к другой вершине, то они принадлежат одному графу.

                var dic = GraphId.Dijkstra(graphOfLines, v);
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

















        /// <summary>
        /// Graph Constructions
        /// </summary>
        /// <param name="MEPSystemForGraph">REVIT MEPSystem must be! DUCT SYSTEM</param>
        /// <returns></returns>
        public GraphId GraphIdFromSystem(MEPSystem MEPSystemForGraph, Autodesk.Revit.DB.Document doc)
        {
            GraphId graphId = new GraphId(); //создание графа
            //Helpers helpers = new Helpers();
            MEPSystem = MEPSystemForGraph;
            List<ElementId> elIds = new List<ElementId>();
            List<EdgeId> edges = new List<EdgeId>();
            var count = MEPSystemForGraph.SectionsCount;
            var mechanicalSys = MEPSystemForGraph as MechanicalSystem;
            List<VertexId> vertexIds = new List<VertexId>();
            foreach (var e in mechanicalSys.DuctNetwork.OfType<FamilyInstance>())
            {
                if (Helpers.IsPartType(e, PartType.Elbow) == false)
                {
                    VertexId vertexId = new VertexId(e.Id, e);
                    vertexIds.Add(vertexId);
                }
            }
            for (int i = 0; i < count; i++)
            {
                try
                {
                    int leight_feet = (int)MEPSystemForGraph.GetSectionByIndex(i).TotalCurveLength;
                    int leight = Convert.ToInt32(leight_feet * 304.8);
                    var elementIds = MEPSystemForGraph.GetSectionByIndex(i).GetElementIds();

                    //NB====================================

                    var elementIds_onlyDuct = new List<Duct>();
                    foreach (var duct_ID in elementIds)
                    {
                        
                        var element_finding = doc.GetElement(duct_ID);

                        
                        
                        if (GetCategory.GetBuiltInCategory(element_finding.Category) == BuiltInCategory.OST_DuctCurves)
                        {
                            elementIds_onlyDuct.Add(element_finding as Duct);
                        }


                        /* since 2022

                        if (element_finding.Category.BuiltInCategory == BuiltInCategory.OST_DuctCurves)
                        {
                            elementIds_onlyDuct.Add(element_finding as Duct);
                        }
                        */

                    }

                    foreach (Duct item in elementIds_onlyDuct)
                    {
                        foreach (var connectors in (item as MEPCurve).ConnectorManager.Connectors)
                        {
                            var allref = (connectors as Connector).AllRefs;


                            foreach (Connector con in allref)
                            {
                                
                                if ((GetCategory.GetBuiltInCategory((con as Connector).Owner.Category) == BuiltInCategory.OST_MechanicalEquipment)
                                    || (GetCategory.GetBuiltInCategory((con as Connector).Owner.Category) == BuiltInCategory.OST_ElectricalEquipment))
                                {
                                    if (elementIds.Contains(((con as Connector).Owner.Id))==false)
                                    {
                                        elementIds.Add((con as Connector).Owner.Id);
                                    }
                                    

                                    //
                                }


                                /*
                                if ((con as Connector).Owner.Category.BuiltInCategory == BuiltInCategory.OST_MechanicalEquipment || (con as Connector).Owner.Category.BuiltInCategory == BuiltInCategory.OST_ElectricalEquipment)
                                {
                                    elementIds.Add((con as Connector).Owner.Id);
                                }
                                */
                            }
                        }

                    }
                    //NB====================================


                    List<Duct> duct = new List<Duct>();
                    List<VertexId> vertexIdsForEdge = new List<VertexId>();
                    foreach (ElementId elId in elementIds)
                    {
                        if (doc.GetElement(elId).GetType() == typeof(Duct))
                        {
                            duct.Add(doc.GetElement(elId) as Duct);
                        }
                        else if ((doc.GetElement(elId).GetType() == typeof(FamilyInstance)) & Helpers.IsPartType((doc.GetElement(elId) as FamilyInstance), PartType.Elbow) == false)
                        {
                            foreach (var v in vertexIds)
                            {
                                if (v.Name == elId)
                                {
                                    vertexIdsForEdge.Add(v);
                                }
                            }
                        }
                    }

                    if (vertexIdsForEdge.Count == 2)
                    {
                        edges.Add(new EdgeId(vertexIdsForEdge[0], vertexIdsForEdge[1], leight, duct));
                        edges.Add(new EdgeId(vertexIdsForEdge[1], vertexIdsForEdge[0], leight, duct));
                    }
                    else { continue; }
                }
                catch { continue; }
            }

            foreach (var v in vertexIds)
            {
                foreach (var e in edges)
                {
                    if (v.Name == e.From.Name)
                    {
                        v.Edges.Add(e);
                    }
                }
            }
            graphId.Vertices.AddRange(vertexIds);
            return graphId;
        }


        //
        public GraphId GraphIdFromSystem(Document document, MEPSystem MEPSystemForGraph)
        {
            GraphId graphId = new GraphId(); //создание графа

            MEPSystem = MEPSystemForGraph;
            List<ElementId> elIds = new List<ElementId>();
            List<EdgeId> edges = new List<EdgeId>();
            var count = MEPSystemForGraph.SectionsCount;
            var mechanicalSys = MEPSystemForGraph as MechanicalSystem;
            List<VertexId> vertexIds = new List<VertexId>();
            foreach (var e in mechanicalSys.DuctNetwork.OfType<FamilyInstance>())
            {
                if (Helpers.IsPartType(e, PartType.Elbow) == false)
                {
                    VertexId vertexId = new VertexId(e.Id, e);
                    vertexIds.Add(vertexId);
                }
            }
            for (int i = 0; i < count; i++)
            {
                try
                {
                    int leight_feet = (int)MEPSystemForGraph.GetSectionByIndex(i).TotalCurveLength;
                    int leight = Convert.ToInt32(leight_feet * 304.8);
                    var elementIds = MEPSystemForGraph.GetSectionByIndex(i).GetElementIds();

                    //NB====================================

                    var elementIds_onlyDuct = new List<Duct>();
                    foreach (var duct_ID in elementIds)
                    {
                        var element_finding = document.GetElement(duct_ID);
                        
                        
                        if (GetCategory.GetBuiltInCategory(element_finding.Category) == BuiltInCategory.OST_DuctCurves)
                        {
                            elementIds_onlyDuct.Add(element_finding as Duct);
                        }

                        /*
                         * if (element_finding.Category.BuiltInCategory == BuiltInCategory.OST_DuctCurves)
                        {
                            elementIds_onlyDuct.Add(element_finding as Duct);
                        }
                         * */

                    }

                    foreach (Duct item in elementIds_onlyDuct)
                    {
                        foreach (var connectors in (item as MEPCurve).ConnectorManager.Connectors)
                        {
                            var allref = (connectors as Connector).AllRefs;


                            foreach (Connector con in allref)
                            {
                                if ((GetCategory.GetBuiltInCategory((con as Connector).Owner.Category) == BuiltInCategory.OST_MechanicalEquipment )
                                    || GetCategory.GetBuiltInCategory((con as Connector).Owner.Category) == BuiltInCategory.OST_ElectricalEquipment)
                                {
                                    elementIds.Add((con as Connector).Owner.Id);
                                }


                                /*                                
                                                                if ((con as Connector).Owner.Category.BuiltInCategory == BuiltInCategory.OST_MechanicalEquipment || (con as Connector).Owner.Category.BuiltInCategory == BuiltInCategory.OST_ElectricalEquipment)
                                                                {
                                                                    elementIds.Add((con as Connector).Owner.Id);
                                                                }
                                */
                            }
                        }

                    }
                    //NB====================================

                    List<Duct> duct = new List<Duct>();
                    List<VertexId> vertexIdsForEdge = new List<VertexId>();
                    foreach (ElementId elId in elementIds)
                    {
                        if (document.GetElement(elId).GetType() == typeof(Duct))
                        {
                            duct.Add(document.GetElement(elId) as Duct);
                        }
                        else if ((document.GetElement(elId).GetType() == typeof(FamilyInstance)) & Helpers.IsPartType((document.GetElement(elId) as FamilyInstance), PartType.Elbow) == false)
                        {
                            foreach (var v in vertexIds)
                            {
                                if (v.Name == elId)
                                {
                                    vertexIdsForEdge.Add(v);
                                }
                            }
                        }
                    }

                    if (vertexIdsForEdge.Count == 2)
                    {
                        edges.Add(new EdgeId(vertexIdsForEdge[0], vertexIdsForEdge[1], leight, duct));
                        edges.Add(new EdgeId(vertexIdsForEdge[1], vertexIdsForEdge[0], leight, duct));
                    }
                    else { continue; }
                }
                catch { continue; }
            }

            foreach (var v in vertexIds)
            {
                foreach (var e in edges)
                {
                    if (v.Name == e.From.Name)
                    {
                        v.Edges.Add(e);
                    }
                }
            }

            graphId.Vertices.AddRange(vertexIds);
            return graphId;
        }










        /// <summary>
        /// Find VertexId by Revit ElementId
        /// </summary>
        /// <param name="graph">Graph</param>
        /// <param name="elementId">ElementID by Revit</param>
        /// <returns></returns>
        static public VertexId GetVertexId(GraphId graph, ElementId elementId)
        {
            List<VertexId> list = new List<VertexId>();
            for (int i = 0; i < graph.Vertices.Count; i++)
            {
                if (graph.Vertices[i].Name.Equals(elementId))
                {
                    list.Add(graph.Vertices[i]);
                }
            }
            if (list.Count == 0)
            { return null; }
            else { return list[0]; }


        }

        
        static public List<EdgeId> GetAllEgesId(GraphId graph)
        {
            List<EdgeId> list = new List<EdgeId>();

            foreach(var item in graph.Vertices)
            {
                list.AddRange(item.Edges);
            }
            
            return [.. list.Distinct()];


        }


        /// <summary>
        /// Math sum of all Revit ducts in list
        /// </summary>
        /// <param name="lducts"></param>
        /// <returns></returns>
        public static double LenghOfDucts(List<Duct> lducts)
        {
            if (lducts.Count == 0) { return 0; }
            double sumLenght = 0;
            foreach (var item in lducts)
            {
                var el = item.Location as LocationCurve;
                var lenght = Math.Round((double)el.Curve.Length, 1);
                sumLenght += lenght;
            }
            if (1 < sumLenght & sumLenght > 0) { return 1; }
            return sumLenght;
        }



        /// <summary>
        /// <para>{NS} Dijkstra</para>
        /// Find all short ways from sorse to all vertex in graph
        /// </summary>
        /// <param name="graph">Graph</param>
        /// <param name="source">sourse VERTEX</param>
        /// <param name="dv">(LIST VERTEX) Dic all Vertex in Grahp for sourse Vertex</param>
        /// <param name="de">(LIST EDGE) Dic all Vertex in Grahp for sourse Vertex</param>
        /// <returns></returns>
        public static Dictionary<VertexId, int> Dijkstra(GraphId graph, VertexId source, out Dictionary<VertexId, List<VertexId>> dv, out Dictionary<VertexId, List<EdgeId>> de)
        {
            var distances = graph.Vertices.ToDictionary(v => v, v => int.MaxValue);
            var WaysOfVertex = graph.Vertices.ToDictionary(v => v, v => new List<VertexId>());
            var WaysOfEges = graph.Vertices.ToDictionary(v => v, v => new List<EdgeId>());
            var previous = new Dictionary<VertexId, VertexId>();
            var notVisited = new HashSet<VertexId>(graph.Vertices);
            distances[source] = 0;
            while (notVisited.Any())
            {
                var nearestVertex = notVisited.OrderBy(v => distances[v]).FirstOrDefault();
                notVisited.Remove(nearestVertex);

                foreach (var edge in nearestVertex.Edges)
                {
                    var neighbor = edge.To;

                    if (notVisited.Contains(neighbor))
                    {
                        var currentDistance = distances[nearestVertex] + edge.Length_mm;
                        List<EdgeId> currentDistanceOfEdge = new List<EdgeId>();
                        currentDistanceOfEdge.AddRange(WaysOfEges[nearestVertex]);
                        currentDistanceOfEdge.Add(edge);
                        List<VertexId> currentDistanceOfVertex = new List<VertexId>();
                        currentDistanceOfVertex.AddRange(WaysOfVertex[nearestVertex]);
                        currentDistanceOfVertex.Add(neighbor);
                        if (currentDistance < distances[neighbor])
                        {
                            distances[neighbor] = currentDistance;
                            previous[neighbor] = nearestVertex;

                            WaysOfEges[neighbor] = currentDistanceOfEdge;
                            WaysOfVertex[neighbor] = currentDistanceOfVertex;
                        }
                    }
                }
            }
            dv = WaysOfVertex;
            de = WaysOfEges;
            return distances;
        }


        ///ВТОРАЯ ПЕРЕГРУЗКА
        /// <summary>
        /// <para>{NS} Dijkstra</para>
        /// Find all short ways from sorse to all vertex in graph
        /// </summary>
        /// <param name="graph">Graph</param>
        /// <param name="source">sourse VERTEX</param>
        /// <returns></returns>
        public static Dictionary<VertexId, int> Dijkstra(GraphId graph, VertexId source)
        {
            var distances = graph.Vertices.ToDictionary(v => v, v => int.MaxValue);

            var previous = new Dictionary<VertexId, VertexId>();
            var notVisited = new HashSet<VertexId>(graph.Vertices);
            distances[source] = 0;
            while (notVisited.Any())
            {
                var nearestVertex = notVisited.OrderBy(v => distances[v]).FirstOrDefault();
                notVisited.Remove(nearestVertex);

                foreach (var edge in nearestVertex.Edges)
                {
                    var neighbor = edge.To;

                    if (notVisited.Contains(neighbor))
                    {
                        var currentDistance = distances[nearestVertex] + edge.Length_mm;
                        List<EdgeId> currentDistanceOfEdge = new List<EdgeId>();

                        currentDistanceOfEdge.Add(edge);
                        List<VertexId> currentDistanceOfVertex = new List<VertexId>();

                        currentDistanceOfVertex.Add(neighbor);
                        if (currentDistance < distances[neighbor])
                        {
                            distances[neighbor] = currentDistance;
                            previous[neighbor] = nearestVertex;

                        }
                    }
                }
            }

            return distances;
        }



        /// <summary>
        /// <para>{NS}</para>
        /// <para>All Duct from MEPSystem</para>
        /// </summary>
        /// <param name="sys"></param>
        /// <returns></returns>
        public static List<Duct> DuctFromMS(MEPSystem sys)
        {
            List<Duct> list = new List<Duct>();
            var es = sys as MechanicalSystem;
            list.AddRange(es.DuctNetwork.OfType<Duct>());
            return list;
        }


    }




    /// <summary>
    /// VertexId Class
    /// </summary>
    public class VertexId
    {
        public ElementId Name { get; set; }
        public List<EdgeId> Edges { get; }

        public List<Object> Objects { get; set; } = [];

        /// <summary>
        /// OST_MechanicalEquipment,
        /// OST_ElectricalEquipment
        /// </summary>
        public EQ eQ { get; }

        public VertexId(ElementId name, FamilyInstance fi)
        {
            Name = name;
            Edges = new List<EdgeId>();


            if (GetCategory.GetBuiltInCategory(fi.Category) == BuiltInCategory.OST_MechanicalEquipment)
            {
                eQ = new EQ(fi);

            }
            else if (GetCategory.GetBuiltInCategory(fi.Category) == BuiltInCategory.OST_ElectricalEquipment)
            {
                eQ = new EQ(fi);
                eQ.EQipment(eQ, true);
            }



            /*
            if (fi.Category.BuiltInCategory == BuiltInCategory.OST_MechanicalEquipment)
            {
                eQ = new EQ(fi);

            }
            else if (fi.Category.BuiltInCategory == BuiltInCategory.OST_ElectricalEquipment)
            {
                eQ = new EQ(fi);
                eQ.EQipment(eQ);
            }
            */

        }
        
        /// <summary>
        /// Точка вершины (начало/конец кривой) на чертежном виде
        /// </summary>
        public XYZ Xyz { get; }
        /// <summary>
        /// Для кривых
        /// </summary>
        /// <param name="xyz"></param>
        public VertexId(XYZ xyz, FamilyInstance fi)
        {
            
            Edges = new List<EdgeId>();
            Xyz = xyz;
            Name = fi.Id;

        }

        public List<Element> Names { get; set; } = [];
        public VertexId(XYZ xyz)
        {

            Edges = new List<EdgeId>();
            Xyz = xyz;

        }
    }

    /// <summary>
    /// ElementId
    /// </summary>
    public class EdgeId
    {
        public List<Duct> Duct { get; }
        public VertexId From { get; }
        public VertexId To { get; }
        //public List<Cabel> ListOfCabel { get; set; } = new List<Cabel>();//  список кабеля для него

        public int Length_mm { get; } //По ней рассчитывается ближайшая вершина



        public EdgeId(VertexId from, VertexId to, int leight, List<Duct> duct)
        {
            From = from;
            To = to;
            Length_mm = leight;
            Duct = duct;
        }


        public Curve Curve { get; }
        public Element ElementOfCurve { get; }
        public EdgeId(VertexId from, VertexId to,  Element el)
        {
            ElementOfCurve = el;
            From = from;
            To = to;
            Curve = (el.Location as LocationCurve).Curve;
            Length_mm = ((int)(Curve.Length).ToMillimeters());
        }



        public VirtualCurve VirtualCurve { get; }
        public EdgeId(VertexId from, VertexId to, VirtualCurve curve)
        {
            From = from;
            To = to;
            VirtualCurve = curve;
            Length_mm = ((int)(VirtualCurve.Length).ToMillimeters());
        }


    }
    
    /// <summary>
    /// Представление кривой в виде двух точек (отрезок)
    /// </summary>
    public class VirtualCurve
    {
        
        public XYZ Begin { get; }
        public XYZ End { get; }

        public double Length { get; }

        public VirtualCurve (XYZ begin, XYZ end)
        {
            Begin = begin;
            End = end;
            Length = Math.Sqrt(Math.Pow((Begin.X - End.X), 2) + Math.Pow((Begin.Y - End.Y), 2));//расстояние между двумя точками.
        }


    }

    
    







    /// <summary>
    /// Путь иежду двумя вершинами
    /// </summary>
    public class WayId
    {
        public VertexId Begin { get; }
        public VertexId End { get; }
        /// <summary>
        /// Список ребер в пути
        /// </summary>
        public List<EdgeId> WayEdge { get; }
        /// <summary>
        /// Список вершин в пути
        /// </summary>
        public List<VertexId> WayVertex { get; }
        public List<Duct> Ducts { get; } = new List<Duct>();
        public int Length { get; } = 0;
        public WayId(VertexId begin, VertexId end, GraphId graph)
        {
            Begin = begin;
            End = end;
            GraphId.Dijkstra(graph, Begin, out var dictv, out var dictde);
            WayEdge = dictde[End];
            WayVertex = dictv[End];
            Ducts = new List<Duct>();
            foreach (var d in WayEdge)
            {
                // Length = Length + d.Length_mm;
                Ducts.AddRange(d.Duct);
            }
            foreach (var d in Ducts)
            {

                Length = Length + Convert.ToInt32(d.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble() * 304.8);

            }

        }

    }



    /// <summary>
    /// Cabel
    /// </summary>
    public class Cabel
    {
        public WayId WayId { get; }
        public string Namber { get; set; } //Номер кабеля
        /// <summary>
        /// Округленная длина до 5 в метрах
        /// </summary>
        public int Length { get; } = 0;
        public CabelJournal cabelJournal { get; } = new CabelJournal();
        public Cabel(WayId wayId)
        {
            WayId = wayId;
            
            //Length = LenghtInt(wayId.Length,5);


            foreach (Duct du in wayId.Ducts)
            {
                /// Создаем словарь с кабеленесущими и их количеством в рамках кабеля
                /// Если способ прокладки не назначен - то группируется в словаре ключу "открыто"
                var carries = "открыто";
                try
                {
                    carries = du.LookupParameter("NS_Способ_прокладки").AsString();
                    if (System.String.IsNullOrEmpty(carries))
                    {
                        carries = "открыто";
                    }
                }
                catch { carries = "открыто"; }

                var lint = Convert.ToInt32(du.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble() * 304.8);
                try { cabelJournal.Carries.Add(carries, lint); }
                catch { cabelJournal.Carries[carries] = cabelJournal.Carries[carries] + lint; }


            }
            Length = 0;

            
            //Округляем значения до длины Length = wayId.Length
            //и Получаем значения кабелей в стрингах
            List<string> valueCarries = new List<string>();
            Length = wayId.Length;
            Length = LenghtInt(wayId.Length, 5);
            /*
            
            foreach (string i in cabelJournal.Carries.Keys)
            {
                int lengt = LenghtInt(cabelJournal.Carries[i], 5);
                valueCarries.Add(i + "-" + lengt.ToString() + "м");
                cabelJournal.Carries[i] = lengt;
                Length = Length + lengt;
            }
            */

            /*

            */
            //cabelJournal.CarriesString = System.String.Join(Environment.NewLine, valueCarries);
            cabelJournal.CarriesString = System.String.Join(" / ", valueCarries);
        }









        /// <summary>
        /// Math sum for Duct in List
        /// <para> if 1 > sumLenght and sumLenght > 0  return 1"</para>
        /// </summary>
        /// <param name="duct"></param>
        /// <returns></returns>
        static double LenghtDouble(List<Duct> duct)
        {

            if (duct.Count == 0) { return 0; }
            double sumLenght = 0;
            foreach (var item in duct)
            {
                var el = item.Location as LocationCurve;

                var lenght = Math.Round((double)el.Curve.Length, 1);
                sumLenght += lenght;

            }

            if (1 > sumLenght & sumLenght > 0) { return 1; }


            return (sumLenght * 304.8) / 1000;
        }

        /// <summary>
        /// Math sum for Duct in List
        /// <para> round must have int 0...10 (round for cabel in "cabel jurnal")</para>
        /// </summary>
        /// <param name="duct"></param>
        /// /// <param name="round">Must have 1/5/10</param>
        /// <returns></returns>
        static int LenghtDuctInt(List<Duct> duct, int round)
        {
            var integer = ((int)System.Math.Round(LenghtDouble(duct), MidpointRounding.ToEven));

            if (integer - (integer % round) == 0)
            {
                return round;
            }
            else { return integer - (integer % round); }
        }

        static int LenghtInt(int number, int round)
        {
            var integer = number / 1000;

            if (integer - (integer % round) == 0)
            {
                return round;
            }
            else { return integer - (integer % round); }
        }

    }


    public enum TypeGroupCabel { Силовой, Слаботочный, Контрольный }
    public enum TypeCarries { Лоток, ПВХтруба, ПНДтруба, МРукав, Вертикально }



    /// <summary>
    /// Кабеленесущие
    /// </summary>
    public class CabelJournal
    {
        public string Namber { get; set; } //Номер кабеля
        public string Begin { get; set; }
        public string End { get; set; }
        public Dictionary<string, int> Carries { get; set; } = new Dictionary<string, int>();

        public string CarriesString { get; set; }

        /*
        public int В_лотке { get; set; }
        public int В_трубе { get; set; }
        public int В_стояке { get; set; }
        */
        public int Сечение { get; set; } //мм кв.
        public string Наименование_краткое { get; set; }
        public string NxMxS { get; set; }
        public string Наименование_полное { get; set; }
        public string Производитель { get; set; }
        public string Марка { get; set; }
        public string Артикул { get; set; }
        public TypeGroupCabel Type { get; set; } = TypeGroupCabel.Слаботочный;




    }

}



