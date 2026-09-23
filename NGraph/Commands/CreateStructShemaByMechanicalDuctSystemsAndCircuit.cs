using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using NGraph.Core;
using Nice3point.Revit.Toolkit.External;

namespace NGraph.Commands;

/// <summary>
///     Info for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CreateStructShemaByMechanicalDuctSystemsAndCircuit : ExternalCommand
{
    
    public override void Execute()
    {
       
        var element = Helpers.SelectElementId(BuiltInCategory.OST_DuctCurves, Application.ActiveUIDocument, Application.ActiveUIDocument.Document);


        CreateBlockDiagram(Application.ActiveUIDocument.Document, element.Id, element);

    }
    
    
        void CreateBlockDiagram(Document _doc, ElementId elementId, Element element)
        {
            if (element is not Duct duct || duct.MEPSystem is not MechanicalSystem system)
            {
                TaskDialog.Show("NGraph", "Выберите воздуховод, принадлежащий механической системе.");
                return;
            }
            Helpers helpers = new Helpers();


            #region //=========== 1 =========== grahp
            GraphId gr = new GraphId();
            GraphId graph = gr.GraphIdFromSystem(system , Application.ActiveUIDocument.Document);
            #endregion


            #region // =========== 2 =========== circuitIds

            List<CircuitId> circuitIds = CircuitId.CreateCircuitIds(_doc, BuiltInCategory.OST_ElectricalCircuit, graph);

            /*  Удаляем
            
            List<CircuitId> circuitIds = new();
            var lElectricalCircuit = helpers.AllElementsOfCategory(_doc, BuiltInCategory.OST_ElectricalCircuit);
            foreach (var item in lElectricalCircuit)
            {
                CircuitId circuit = new CircuitId(item, _doc);
                FamilyInstance BaseEquipment = (item as MEPSystem).BaseEquipment;
                ElementId elId = BaseEquipment.Id;
                if (GraphId.GetVertexId(graph, elId) == null) { continue; }
                VertexId vertexId = GraphId.GetVertexId(graph, elId);
                List<ElementId> listElId = new List<ElementId>();
                List<VertexId> listVertxId = new List<VertexId>();
                foreach (var l in (item as MEPSystem).Elements)
                {
                    listElId.Add((l as FamilyInstance).Id);
                    listVertxId.Add(GraphId.GetVertexId(graph, (l as FamilyInstance).Id));
                }
                circuit.BaseEquipment = vertexId;
                circuit.VerticesFromBaseEquipment = listVertxId;
                circuitIds.Add(circuit);

            }

            */

            #endregion


            #region //=========== 3 =========== circuitIds + graph
            foreach (CircuitId circuitId in circuitIds)
            {
                if (circuitId.NSA_Цепь_звезда)
                {

                    //Для звезды (алгоритм Дэйктры)
                    foreach (VertexId vertexId in circuitId.VerticesFromBaseEquipment)
                    {
                        circuitId.Cabels.Add(new Cabel(new WayId(circuitId.BaseEquipment, vertexId, graph)));
                    }

                }
                else
                {
                    //circuitId.VerticesFromBaseEquipment
                    //circuitId.BaseEquipment
                    List<VertexId> new_vertexIds = new List<VertexId>();
                    VertexId start_Vertex = circuitId.BaseEquipment;
                    while (circuitId.VerticesFromBaseEquipment.Count > 0)
                    {
                        //фильтруем словарь Вершин (оставляем расчет алгоритма только для circuitId.VerticesFromBaseEquipmen)
                        Dictionary<VertexId, int> Dic = new Dictionary<VertexId, int>(GraphId.Dijkstra(graph, start_Vertex)
                            .Where(i => circuitId.VerticesFromBaseEquipment.Contains(i.Key)).ToDictionary(i => i.Key, i => i.Value));

                        //MinBy и MaxBy нет в старших версиях NET6
                        /*
                         * 
                         * 
                        VertexId minVertex = Dic.MinBy(k => k.Value).Key;
                        
                        //OrderBy(k => k.Value).First(). === MinBy
                        //OrderByDescending(k => k.Value).First(). === MaxBy

                        */
                        VertexId minVertex = Dic.OrderBy(k => k.Value).First().Key;

                        //Удаляем найденую вершину и делаем ее стартовой


                        //Сразу создаем кабель
                        circuitId.Cabels.Add(new Cabel(new WayId(start_Vertex, minVertex, graph)));

                        start_Vertex = minVertex;
                        circuitId.VerticesFromBaseEquipment.Remove(minVertex);
                        //Добавляем ее в список
                        new_vertexIds.Add(minVertex);
                    }
                    //Готовый список по порядку подключения от ближнеего к дальнему. ((Надо создавать метод комивояжера))!!!!!!!!!! т.к не учитывается минимальная сумма построенного пути.
                    //Для последовательного соединения (алгоритм Комивояжера.Circle)
                    circuitId.VerticesFromBaseEquipment = new_vertexIds;
                    List<EQ> listEQ = new List<EQ>();

                    ///Переделать!!!!!!!!!!!!
                    foreach (var eq in circuitId.VerticesFromBaseEquipment)
                    {
                        listEQ.Add(eq.eQ);
                    }
                    circuitId.EQFromBaseEquipment = listEQ;
                    ///переделать !!!!!!!!!!!!! 
                }





            }

            #endregion



            //ОТЛАДКА******************************


            using (Transaction tr = new Transaction(_doc, "Запись нумератора стуктурной схемы"))
            {
                tr.Start();
                try
                {

                    CircuitId.CircuitGroup(circuitIds, helpers.EQipmentFromGraph(graph)); //Группировка и нумерация

                }
                catch { }
                tr.Commit();
            }



            ViewDrafting view = helpers.CreateViewDrafting(Application.ActiveUIDocument.Document, "Структурная схема", true, Application.ActiveUIDocument);

            //Расстановка структуры по уровням
            /*
            int indexEndToEnd = 1; //Сквозная нумерация в структурной схеме.

            sructSxema.CreateStructFor_Level_WithMarkaSS(_doc, view, new XYZ(0, 0, 0), TypeOfLevel.Level0, circuitIds, ref indexEndToEnd);

            sructSxema.CreateStructFor_Level_WithMarkaSS(_doc, view, new XYZ(0, 3, 0), TypeOfLevel.Level1, circuitIds , ref indexEndToEnd);
            */

            //sructSxema.CreateReferencePlane(_doc, view, FormatGost.A2_h);

           


            
            //Словарь цепей с ключами типов цепей
            Dictionary<Autodesk.Revit.DB.Electrical.ElectricalSystemType, List<CircuitId>> dic_circuitIds
                = circuitIds.GroupBy(i => i.Тип).ToDictionary(a=> a.Key, a=> a.ToList());

            XYZ begin = new XYZ(0, 0, 0);
            XYZ step = new XYZ(0, helpers.MillimetersToFeet(50), 0);
            int indexBlockDiagramId = 1; //Сквозная нумерация в структурной схеме.
            foreach (var circuitId in dic_circuitIds)
            {

                BlockDiagram blockDiagram_level0 = new BlockDiagram(_doc, view, begin, TypeOfLevel.Level0, circuitId.Value, ref indexBlockDiagramId);

                if (blockDiagram_level0.GroupSymbols.Count != 0)
                    begin = new XYZ(begin.X, begin.Y + blockDiagram_level0.End.Y, 0) + step;
                
                
                BlockDiagram blockDiagram_level1 = new BlockDiagram(_doc, view, begin, TypeOfLevel.Level1, circuitId.Value, ref indexBlockDiagramId);

                if (blockDiagram_level1.GroupSymbols.Count != 0)
                    begin = new XYZ(begin.X, begin.Y + blockDiagram_level1.End.Y, 0) + step;


                BlockDiagram blockDiagram_level2 = new BlockDiagram(_doc, view, begin, TypeOfLevel.Level2, circuitId.Value, ref indexBlockDiagramId);
                if (blockDiagram_level2.GroupSymbols.Count != 0)
                    begin = new XYZ(begin.X, begin.Y + blockDiagram_level2.End.Y, 0) + step;


            }



            foreach (CircuitId circuitId in circuitIds)
            {

                foreach (Cabel cab in circuitId.Cabels)
                {
                    cab.cabelJournal.NxMxS = circuitId.NSA_Кабель_жилы_сечение;
                    cab.cabelJournal.Наименование_краткое = circuitId.NSA_Кабель_наименование;
                    cab.cabelJournal.Наименование_полное = circuitId.NSA_Кабель_наименование;
                    cab.cabelJournal.Производитель = circuitId.NSA_Кабель_производитель;
                    cab.cabelJournal.Марка = circuitId.NSA_Кабель_марка;
                    cab.cabelJournal.Артикул = circuitId.NSA_Кабель_артикул;
                    cab.cabelJournal.Сечение = circuitId.NSA_Цепь_внешн_диам * circuitId.NSA_Цепь_внешн_диам; //Занимаемое сечение рассчитывается изходя из площади сечения кабеля принятого как квадрат со стороной диаметра.


                }


            }


                /*
                int iindexGroupSymbol = 0;

                foreach (GroupSymbol item in blockDiagram_level1)
                {
                    iindexGroupSymbol++;
                }
                TaskDialog.Show($"GroupSymbol", $"Всего {iindexGroupSymbol} GroupSymbol  ");
                */







                /*


                #region //=========== 4 =========== Назнечение марки кабеля ElectricalSystem - тип кабеля, DUCT - способ прокладки, CircuitId и EQ - начало, конец, номер передаем в Cabel

                foreach (CircuitId circuitId in circuitIds)
                {

                    foreach (Cabel cab in circuitId.Cabels)
                    {
                        cab.cabelJournal.NxMxS = circuitId.NSA_Кабель_жилы_сечение;
                        cab.cabelJournal.Наименование_краткое = circuitId.NSA_Кабель_наименование;
                        cab.cabelJournal.Наименование_полное = circuitId.NSA_Кабель_наименование;
                        cab.cabelJournal.Производитель = circuitId.NSA_Кабель_производитель;
                        cab.cabelJournal.Марка = circuitId.NSA_Кабель_марка;
                        cab.cabelJournal.Артикул = circuitId.NSA_Кабель_артикул;
                        cab.cabelJournal.Сечение = circuitId.NSA_Цепь_внешн_диам*circuitId.NSA_Цепь_внешн_диам; //Занимаемое сечение рассчитывается изходя из площади сечения кабеля принятого как квадрат со стороной диаметра.


                    }

                        //Тип марки соединения звезды 1 вариант
                        if (circuitId.Количество_элементов == 1)
                    {
                        foreach (var cab in circuitId.Cabels)
                        {

                            cab.cabelJournal.Begin = cab.WayId.Begin.eQ.Имя_панели;
                            cab.cabelJournal.End = circuitId.NSA_Цепь_комментарий;
                            var marka = "("+circuitId.Номер_слота+")"+ cab.cabelJournal.Begin + "--"+ cab.cabelJournal.End;
                            cab.Namber = marka;
                            cab.cabelJournal.Namber = marka;

                            //var marka = circuitId.Номер_цепи;

                        }
                    }
                    //Тип марки соединения звезды 2 вариант
                    else if (circuitId.NSA_Цепь_звезда && circuitId.Количество_элементов > 1)
                    {
                        for (int i = 0; i < circuitId.Cabels.Count; i++)
                        {
                            var istring = (i+1).ToString();
                            circuitId.Cabels[i].cabelJournal.Begin = "(" + circuitId.Номер_слота + $".{istring})" +circuitId.BaseEquipment.eQ.Имя_панели;
                            circuitId.Cabels[i].cabelJournal.End = circuitId.Cabels[i].WayId.End.eQ.Имя_панели;
                            var marka = circuitId.Cabels[i].cabelJournal.Begin + "--" + circuitId.Cabels[i].cabelJournal.End;
                            circuitId.Cabels[i].Namber = marka;
                            circuitId.Cabels[i].cabelJournal.Namber = marka;
                        }

                    }

                    //Тип марки для последовательного соединения
                    else
                    {
                        for (int i = 0; i < circuitId.Cabels.Count; i++)
                        {
                            circuitId.Cabels[i].cabelJournal.Begin = circuitId.Cabels[i].WayId.Begin.eQ.Имя_панели;
                            circuitId.Cabels[i].cabelJournal.End = circuitId.Cabels[i].WayId.End.eQ.Имя_панели;
                            var marka = "(" + circuitId.Номер_слота + ")" + circuitId.Cabels[i].cabelJournal.Begin + "--" + circuitId.Cabels[i].cabelJournal.End;
                            circuitId.Cabels[i].Namber = marka;
                            circuitId.Cabels[i].cabelJournal.Namber = marka;
                        }
                    }
                }




                #endregion
                */


                using (Transaction tr = new Transaction(_doc, "Марки на структуре"))
            {
                tr.Start();
                try
                {

                    ///Маркируем "Элементы узлов" (SymbolId_MarkaCabel)
                    helpers.CreateIndependentTag(_doc, view);

                }
                catch { TaskDialog.Show("Ошибка", "Марки на структуре отсутствуют"); }
                tr.Commit();
            }






            #region //=========== 5 =========== DUCT + LIST<CABEL>

            Dictionary<Duct, List<Cabel>> dDuctCabel = new Dictionary<Duct, List<Cabel>>();


            //Номер слота отстутствует в R2021
            /*
            var sortedCircuitIds = circuitIds.OrderBy(circuit => circuit.BaseEquipment.Name.ToString())
                .ThenBy(cab => int.Parse(cab.Номер_слота));
            */
            var sortedCircuitIds = circuitIds.OrderBy(circuit => circuit.BaseEquipment.Name.ToString());
              //  .ThenBy(cab => int.Parse(cab.Номер_слота));
            foreach (CircuitId circuit in sortedCircuitIds)
            {
                foreach (var cab in circuit.Cabels)
                {
                    if (cab != null /*&&  == РазделПроекта*/)
                    {
                        foreach (var v in cab.WayId.Ducts)
                        {

                            if (dDuctCabel.ContainsKey(v) == false)
                            {
                                dDuctCabel.Add(v, new List<Cabel>());
                            }
                        }
                    }
                }


                foreach (var cab in circuit.Cabels)
                {
                    if (cab != null /*&&  == РазделПроекта*/)
                    {
                        foreach (var v in cab.WayId.Ducts)
                        {
                            dDuctCabel[v].Add(cab);
                        }
                    }
                }
            }


            // Создаем словарь воздуховод - номер цепи
            Dictionary<Duct, List<string>> dicDuctsstring = dDuctCabel.ToDictionary(k => k.Key, k => k.Value.ConvertAll(l => l.Namber));

            //Создаем словарь воздуховод - занимаемое сечение
            Dictionary<Duct, List<int>> dicDuctintSquare = dDuctCabel.ToDictionary(k => k.Key, k => k.Value.ConvertAll(l => l.cabelJournal.Сечение));

            #endregion




















            //ExelExport.Exel(circuitIds);



            #region transaction Revit

            using (Transaction tr = new Transaction(_doc, "Запись марки кабеля для ребра графа"))
            {
                tr.Start();
                try
                {
                    foreach (var item in GraphId.DuctFromMS(system))
                    {
                        item.LookupParameter("Комментарии").Set("");
                        item.LookupParameter("NS_Сечение_мм.кв.").Set(0);

                    }

                    foreach (var item in dicDuctintSquare)
                    {
                        item.Key.LookupParameter("NS_Сечение_мм.кв.").Set(item.Value.Sum());
                    }

                    foreach (var item in dicDuctsstring)
                    {
                        item.Key.LookupParameter("Комментарии").Set(System.String.Join(Environment.NewLine, item.Value));
                    }


                }
                
                catch { TaskDialog.Show("Ошибка", "Запись марки кабеля для ребра графа"); }
                tr.Commit();
            }


            #endregion






        }


    
    
}