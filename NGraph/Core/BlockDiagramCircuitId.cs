using System.Collections;
using Autodesk.Revit.UI;
namespace NGraph.Core;

/// <summary>
/// Структура представляет CircuitId
/// </summary>
public class BlockDiagramCircuitId : Gabarit, IEnumerable
{

    
    public CircuitId CircuitId { get; }
    public List<GroupSymbol> GroupSymbols { get;} = new List<GroupSymbol>();
    
    
    public IEnumerator GetEnumerator() => GroupSymbols.GetEnumerator();

    public int indexBlockDiagramCircuitId { get; set; }
    public BlockDiagramCircuitId(Document activeDocument, BlockDiagramBaseEqupment parent, CircuitId circuitId, XYZ beginXYZ , ref int indexBlockDiagramCircuitId)
    {

        Helpers helpers = new Helpers();
        Document doc = parent.Doc;
        View viewDrafting = parent.ViewDrafting;
        this.CircuitId = circuitId;
        this.indexBlockDiagramCircuitId = indexBlockDiagramCircuitId;
        indexBlockDiagramCircuitId++;
        

        using (Transaction tr = new Transaction(activeDocument, $" - {circuitId.Номер_цепи}"))
        {
            tr.Start();
            try
            {
                
                XYZ LocationXYZ = beginXYZ;//Координаты начала расстановки groupSymbol

                //Обращаемся к оборудованию в цепи
                int indexGroupSymbol = 1;
                foreach (VertexId vId in CircuitId.VerticesFromBaseEquipment)
                {
                    
                    //Определяем группу из символа и добавляем в список
                    GroupSymbol groupSymbol = new GroupSymbol(new Symbol_ID(doc, viewDrafting, vId.eQ), new Marka_ID(doc, viewDrafting, vId.eQ.OrientationForMarka_ID), ref indexGroupSymbol);
                    

                    this.GroupSymbols.Add(groupSymbol);
                    groupSymbol.Marka_ID.IsSet = SetMarkaIsSet(groupSymbol, CircuitId); //Определяем наличие марки
                    groupSymbol.deltaXYZ = GetDeltaGabaritGroupSymbol(groupSymbol);

                    //Определяем координаты установки элемента (для марки и для символа)
                    XYZ delta = GetDelta(groupSymbol, CircuitId.NSA_Цепь_звезда);
                    LocationXYZ = LocationXYZ + delta;
                    //Габариты BlockDiagramCircuitId
                    


                    groupSymbol.Symbol_ID.FamilyInstance = groupSymbol.CreateFamilyInstanceSymbol_ID(doc, viewDrafting, LocationXYZ);
                    
                    if (groupSymbol.Marka_ID.IsSet)//Если есть флаг, то добавляем марку
                    groupSymbol.Marka_ID.FamilyInstance = groupSymbol.CreateFamilyInstanceMarka_ID(doc, viewDrafting, LocationXYZ);
                    groupSymbol.SetParametr(groupSymbol);

                    //Записываем в марку индесы (Номер кабеля)
                    string НомерКабеля =
                        parent.indexBlockDiagramBaseEqupment
                        + "-"
                        + this.indexBlockDiagramCircuitId
                        + "."
                        + groupSymbol.indexGroupSymbol.ToString();

                    
                    //Определение принадлежности кабеля к вершине, какой кабель из списка принадлежит этой верщине (оборудованию)
                    foreach (Cabel cab in CircuitId.Cabels)
                    {
                        if (cab.WayId.End.Equals(vId))
                        {
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("NS_ElementId").Set(CircuitId.Element.Id.ToString());
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Длина кабеля").Set(cab.Length);
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Марка кабеля").Set(CircuitId.NSA_Кабель_марка);
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Жилы и сечение").Set(CircuitId.NSA_Кабель_жилы_сечение);
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Номер кабеля").Set(НомерКабеля);
                            groupSymbol.НомерКабеля = НомерКабеля;
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Начало кабеля").Set(cab.WayId.Begin.eQ.Имя_панели);
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Окончание кабеля").Set(cab.WayId.End.eQ.Имя_панели);
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Начало кабеля_eq").Set(cab.WayId.Begin.eQ.ADSK_Наименование_краткое);
                            groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Окончание кабеля_eq").Set(cab.WayId.End.eQ.ADSK_Наименование_краткое);

                            try
                            {
                                groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Рабочий набор").Set(groupSymbol.Marka_ID.FamilyInstance.LookupParameter("Рабочий набор").AsValueString());
                            }
                            catch //Если нет рабочего набора у элемента (файл без совместного доступа)
                            {
                                groupSymbol.Marka_ID.FamilyInstance.LookupParameter("CJ_Рабочий набор").Set((doc.GetElement(groupSymbol.Marka_ID.FamilyInstance.OwnerViewId) as ViewDrafting).Name);
                            }


                        }
                    }

                    
                    


                }
                
            }
            catch { TaskDialog.Show("Ошибка", "Ошибка в классе BlockDiagramCircuitId "); }
            tr.Commit();


            //===>
            //Получаем кабель
            //Записываем Номер кабеля если это то обрудование
            foreach (Cabel cab in CircuitId.Cabels)
            {
                foreach (GroupSymbol groupSymbol in GroupSymbols)
                {
                    if (cab.WayId.End.eQ == groupSymbol.Symbol_ID.EQ)
                    {
                        cab.Namber = groupSymbol.НомерКабеля;
                    }

                }
                
            }
            
            //===>




        }


        //Записываем реальные координаты габаритов групп (после транзакции и создания экземпляра семейства).
        // И определяем реальные координаты всей цепочки [begin ,end]
        XYZ[] begin_end = SetRealXYZ_GroupSymbol(GroupSymbols, viewDrafting);
        XYZ_begin = begin_end[0];
        XYZ_end = begin_end[1];
        deltaXYZ = XYZ_end - XYZ_begin;


        //Размещаем линии


        








    }




    /// <summary>
    /// Габариты группы
    /// </summary>
    /// <param name="groupSymbol"></param>
    /// <returns></returns>
    XYZ GetDeltaGabaritGroupSymbol(GroupSymbol groupSymbol)
    {
        XYZ delta = new XYZ();
        if (groupSymbol.Marka_ID.IsSet)
        delta = groupSymbol.Symbol_ID.deltaXYZ+groupSymbol.Marka_ID.deltaXYZ;
        else 
        delta = groupSymbol.Symbol_ID.deltaXYZ;
        return delta;
    }


    /// <summary>
    /// Смещение в зависимости от FormatSxema
    /// </summary>
    /// <param name="groupSymbol"></param>
    /// <returns></returns>
    XYZ GetDelta(GroupSymbol groupSymbol, bool NSA_звезда)
    {
        XYZ delta = new XYZ();
        if (NSA_звезда)
        {
            delta = new XYZ(0, groupSymbol.deltaXYZ.Y, 0);
            return delta;
        }
        else
        {
            if (groupSymbol.Symbol_ID.EQ.formatSxema == FormatSxema.HorizontalFromLeftToRight) //Если элементы в цепи расположены горизонтально
                delta = new XYZ(groupSymbol.deltaXYZ.X, 0, 0);
            else
                delta = new XYZ(0, groupSymbol.deltaXYZ.Y, 0);

            return delta;
        }
        
    }



    /// <summary>
    /// Определяем присутствие марки
    /// </summary>
    /// <param name="groupSymbol"></param>
    /// <param name="index"></param>
    /// <param name="vId"></param>
    /// <param name="circuitId"></param>
    /// <returns></returns>
    bool SetMarkaIsSet(GroupSymbol groupSymbol, CircuitId circuitId)
    {
        //===========
        // Если это единственный элемент в цепи
        // Создаем экземпляр семйства
        // Ориентация марки определяется описанием EQ
        // 
        bool isSet = false;

        if (CircuitId.VerticesFromBaseEquipment.Count == 1)
        {
            isSet = true;
        }

        else
        {
            // Если в цепи более 2-х элементов
            // Определяем, является ли цепь последовательной или звездой
            // Если цепь звезда - то марка назначается каждому элементу
            if (circuitId.NSA_Цепь_звезда == true)
            {
                isSet = true;
            }
            else
            {
                if (groupSymbol.indexGroupSymbol == 1)
                {
                    isSet = true;
                }
                else if (groupSymbol.Symbol_ID.EQ.HaveMark_Always == true) //определено поьзователем
                {
                    isSet = true;
                }
                
            }
        }
        return isSet;


    }


    /// <summary>
    /// Записываем реальные координаты габаритов групп (после транзакции и создания экземпляра семейства).
    /// И определяем реальные координаты всей BlockDiagramCircuitId [begin ,end]
    /// </summary>
    /// <param name="groupSymbol"></param>
    /// <returns></returns>
    XYZ[] SetRealXYZ_GroupSymbol(List<GroupSymbol> groupSymbols, View view)
    {
        List<double> com_arrayX = new List<double>();
        List<double> com_arrayY = new List<double>();
        foreach (GroupSymbol groupSymbol in groupSymbols)
        {
            List<double> arrayX = new List<double>();
            List<double> arrayY = new List<double>();

            XYZ sMin = new XYZ(), sMax = new XYZ(), mMin = new XYZ(), mMax = new XYZ();

            sMax = groupSymbol.Symbol_ID.FamilyInstance.get_BoundingBox(view).Max;

            sMin = groupSymbol.Symbol_ID.FamilyInstance.get_BoundingBox(view).Min;

            if (groupSymbol.Marka_ID.FamilyInstance != null)
            {
                mMax = groupSymbol.Marka_ID.FamilyInstance.get_BoundingBox(view).Max;
                mMin = groupSymbol.Marka_ID.FamilyInstance.get_BoundingBox(view).Min;

                arrayX.Add(sMax.X); arrayX.Add(sMin.X); arrayX.Add(mMax.X); arrayX.Add(mMin.X);

                arrayY.Add(sMax.Y); arrayY.Add(sMin.Y); arrayY.Add(mMax.Y); arrayY.Add(mMin.Y);
            }
            else
            {
                arrayX.Add(sMax.X); arrayX.Add(sMin.X);

                arrayY.Add(sMax.Y); arrayY.Add(sMin.Y);
            }
           

            groupSymbol.XYZ_end = new XYZ(arrayX.Max(), arrayY.Max(), 0);
            groupSymbol.XYZ_begin = new XYZ(arrayX.Min(), arrayY.Min(), 0);

            com_arrayX.AddRange(arrayX);
            com_arrayY.AddRange(arrayY);
        }
        Helpers helpers = new Helpers();
        //XYZ delta = new XYZ(helpers.MillimetersToFeet(10), helpers.MillimetersToFeet(10),0);
        XYZ delta = new XYZ(helpers.MillimetersToFeet(1), helpers.MillimetersToFeet(1), 0);

        XYZ[] d = [new XYZ(com_arrayX.Min(), com_arrayY.Min(), 0)- delta, new XYZ(com_arrayX.Max(), com_arrayY.Max(), 0)+ delta];


        return d;
    }


    





}



