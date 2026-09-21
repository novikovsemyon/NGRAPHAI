using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
namespace NGraph.Core;

public class CircuitId
{
        public List<Cabel> Cabels { get; set; } = new List<Cabel>();
    public Element Element { get; }
    public FamilyInstance FI_BaseEquipment { get; }
    public List<FamilyInstance> FI_FromBaseEquipment { get; } = new List<FamilyInstance>();
    public VertexId BaseEquipment { get; set; }

    /// <summary>
    /// Оборудование, подключенное к базовому элементу
    /// </summary>
    public List<VertexId> VerticesFromBaseEquipment { get; set; }
    public List<EQ> EQFromBaseEquipment { get; set; }

    public string NSA_Цепь_комментарий { get; set; }
    public string NSA_Цепь_назначение { get; set; }
    public bool NSA_Цепь_звезда { get; set; }

    //REVIT PARAMS
    public string Номер_слота { get; }
    public string Номер_цепи { get; }
    public string Имя_нагрузки { get; set; }
    public string Имя_панели { get; }
    public string Тип_системы { get; }
    public double Длина { get; }
    public int Количество_элементов { get; }
    public string Комментарии { get; set; }
    public ElectricalSystemType Тип { get; }

    //NSA PARAMS
    public string NSA_Кабель_артикул { get; }
    public string NSA_Кабель_производитель { get; }
    public string NSA_Кабель_жилы_сечение { get; }
    public string NSA_Кабель_комментарий { get; }
    public string NSA_Кабель_марка { get; }
    public string NSA_Кабель_наименование { get; }
    public int NSA_Цепь_внешн_диам { get; }



    public CircuitId(Element element, Document _doc)
    {

        Element = element;
        FamilyInstance BE = (element as MEPSystem).BaseEquipment;
        FI_BaseEquipment = BE;
        ElementSet elementSet = new ElementSet();
        elementSet = (element as MEPSystem).Elements;
        foreach (var fi in elementSet)
        {
            FI_FromBaseEquipment.Add(fi as FamilyInstance);
        }

        ElementId elId = BE.Id;

        



        try { NSA_Цепь_комментарий = element.LookupParameter("NS_Цепь_комментарий").AsString(); }
        catch { TaskDialog.Show("Ошибка параметра", "NS_Цепь_комментарий"); }
        NSA_Цепь_назначение = "";
        try { NSA_Цепь_назначение = _doc.GetElement(element.LookupParameter("NS_Цепь_назначение").AsElementId()).Name; }
        catch { NSA_Цепь_назначение = "!NO TYPE"; }
        try { NSA_Цепь_звезда = Convert.ToBoolean(element.LookupParameter("NS_Цепь_звезда").AsInteger()); }
        catch
        {
            TaskDialog.Show("Ошибка параметра", "Проверьте NS_Цепь_звезда");
            _doc.Close();
        }



        //Номер_слота = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_SLOT_INDEX).AsString();
        Номер_цепи = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER).AsString();
        Имя_панели = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_PANEL_PARAM).AsString();
        Имя_нагрузки = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NAME).AsString();
        Тип_системы = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_TYPE).AsValueString();
        Длина = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_LENGTH_PARAM).AsDouble();
        Количество_элементов = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER_OF_ELEMENTS_PARAM).AsInteger();
        Комментарии = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
        Тип = (element as ElectricalSystem).SystemType;

        ReloadNS_parametrs_fromR2019_to2021(element, _doc);



        NSA_Кабель_артикул = element.LookupParameter("NS_Кабель_артикул").AsString();
        NSA_Кабель_производитель = element.LookupParameter("NS_Кабель_производитель").AsString();
        NSA_Кабель_жилы_сечение = element.LookupParameter("NS_Кабель_жилы_сечение").AsString();
        NSA_Кабель_комментарий = element.LookupParameter("NS_Кабель_комментарий").AsString();
        NSA_Кабель_марка = element.LookupParameter("NS_Кабель_марка").AsString();

        NSA_Кабель_наименование = element.LookupParameter("NS_Кабель_наименование").AsString();
        NSA_Цепь_внешн_диам = element.LookupParameter("NS_Цепь_внешн.диам.мм.").AsInteger();






        /*
        var selectFireAlarm = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.FireAlarm); //Пожарка
        var selectNurseCall = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.NurseCall); //Вызов
        var selectPowerBalanced = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.PowerBalanced); //Сбалансированная нагрузка
        var selectPowerCircuit = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.PowerCircuit); //Мощность
        var selectPowerUnBalanced = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.PowerUnBalanced); //Несбалансированная нагрузка
        var selectSecurity = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.Security); //Безопасность
        var selectTelephone = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.Telephone); //Телефон
        var selectUndefinedSystemType = lElectricalCircuit.Where(x => (x as ElectricalSystem).SystemType == ElectricalSystemType.UndefinedSystemType); //нет системы (сломана не подключена)
        */


    }

    /// <summary>
    /// Создание списка цепей
    /// </summary>
    /// <param name="_doc"></param>
    /// <param name="builtInCategory"></param>
    /// <returns></returns>
    public static List<CircuitId> CreateCircuitIds(Document _doc, BuiltInCategory builtInCategory, GraphId graphId)
    {
        List<CircuitId> circuitIds = new();
        Helpers helpers = new Helpers();
        var lElectricalCircuit = helpers.AllElementsOfCategory(_doc, builtInCategory);
        foreach (var item in lElectricalCircuit)
        {
            CircuitId circuit = new CircuitId(item, _doc);
            FamilyInstance BaseEquipment = (item as MEPSystem).BaseEquipment;
            ElementId elId = BaseEquipment.Id;
            if (GraphId.GetVertexId(graphId, elId) == null) { continue; }
            VertexId vertexId = GraphId.GetVertexId(graphId, elId);
            List<ElementId> listElId = new List<ElementId>();
            List<VertexId> listVertxId = new List<VertexId>();
            List<EQ> listEQ = new List<EQ>();
            foreach (var l in (item as MEPSystem).Elements)
            {
                listElId.Add((l as FamilyInstance).Id);
                var vertexid = GraphId.GetVertexId(graphId, (l as FamilyInstance).Id);
                listVertxId.Add(vertexid);
                listEQ.Add(vertexid.eQ);
            }
            circuit.BaseEquipment = vertexId;
            circuit.VerticesFromBaseEquipment = listVertxId;
            circuit.EQFromBaseEquipment = listEQ;
            circuitIds.Add(circuit);
        }

        return circuitIds;

    }

    /// <summary>
    /// Перезапись параметров из ПП_NS 
    /// </summary>
    void ReloadNS_parametrs_fromR2019_to2021(Element element, Document _doc)
    {

        using (Transaction tr = new Transaction(_doc, "Перезапись параметра по цепи"))
        {
            tr.Start();
            try
            {
                if (element.LookupParameter("NS_Кабель_артикул").IsReadOnly == false)
                    element.LookupParameter("NS_Кабель_артикул").Set(element.LookupParameter("ПП_NS_Кабель_артикул").AsString());
                if (element.LookupParameter("NS_Кабель_производитель").IsReadOnly == false)
                    element.LookupParameter("NS_Кабель_производитель").Set(element.LookupParameter("ПП_NS_Кабель_производитель").AsString());
                if (element.LookupParameter("NS_Кабель_комментарий").IsReadOnly == false)
                    element.LookupParameter("NS_Кабель_комментарий").Set(element.LookupParameter("ПП_NS_Кабель_комментарий").AsString());
                if (element.LookupParameter("NS_Кабель_марка").IsReadOnly == false)
                    element.LookupParameter("NS_Кабель_марка").Set(element.LookupParameter("ПП_NS_Кабель_марка").AsString());
                if (element.LookupParameter("NS_Кабель_наименование").IsReadOnly == false)
                    element.LookupParameter("NS_Кабель_наименование").Set(element.LookupParameter("ПП_NS_Кабель_наименование").AsString());
                
                if (element.LookupParameter("NS_Цепь_внешн.диам.мм.").IsReadOnly == false)
                {
                    string s = element.LookupParameter("ПП_NS_Цепь_внешн.диам.мм").AsString();
                    int result = 0;
                    bool res = int.TryParse(s, out result);
                    if (res) { element.LookupParameter("NS_Цепь_внешн.диам.мм.").Set(result); }
                    else { element.LookupParameter("NS_Цепь_внешн.диам.мм.").Set(0); }
                   
                }
                    
                
                if (element.LookupParameter("NS_Кабель_жилы_сечение").IsReadOnly == false)
                    element.LookupParameter("NS_Кабель_жилы_сечение").Set(element.LookupParameter("ПП_NS_Кабель_жилы_сечение").AsString());


            }
            catch { TaskDialog.Show("Ошибка", "Перезапись параметра по цепи (ReloadNS_parametrs_fromR2019_to2021)"); }
            tr.Commit();
        }






    }


     /// <summary>
     /// Группировка и нумератор должны работать, когда определена связь цепей с графом и определена ПОСЛЕДОВАТЕЛЬНОСТЬ подключений.
     /// Последовательность подключений определяется по чаредованию воздуховодов (дэйкстра, комивояжер)
     /// !!! перезаписываем имена панелей (через транзакцию)
     /// Надо отсортировать с учетом названия оборудования, и последовательности его соединения по графу
     /// </summary>
     /// <param name="circuitIds"></param>
     /// <param name="eq"></param>
     public static void CircuitGroup(List<CircuitId> circuitIds, List<EQ> eq)
     {

         //Выбираем цепи по уровням.
         var selCircuitIds_Level0 = from CircuitId in circuitIds where CircuitId.BaseEquipment.eQ.Level == TypeOfLevel.Level0 select CircuitId;
         var selCircuitIds_Level1 = from CircuitId in circuitIds where CircuitId.BaseEquipment.eQ.Level == TypeOfLevel.Level1 select CircuitId;
         var selCircuitIds_Level2 = from CircuitId in circuitIds where CircuitId.BaseEquipment.eQ.Level == TypeOfLevel.Level2 select CircuitId;
         var selCircuitIds_Level3 = from CircuitId in circuitIds where CircuitId.BaseEquipment.eQ.Level == TypeOfLevel.Level3 select CircuitId;

         //Группируем цепи по EQ
         var group_selCircuitIds_Level0 = selCircuitIds_Level0.GroupBy(p => p.BaseEquipment.eQ);
         var group_selCircuitIds_Level1 = selCircuitIds_Level1.GroupBy(p => p.BaseEquipment.eQ);
         var group_selCircuitIds_Level2 = selCircuitIds_Level2.GroupBy(p => p.BaseEquipment.eQ);
         var group_selCircuitIds_Level3 = selCircuitIds_Level3.GroupBy(p => p.BaseEquipment.eQ);


         //Выбираем оборудование по уровням.
         var selectEQ_Level0 = from EQ in eq where EQ.Level == TypeOfLevel.Level0 select EQ;
         var selectEQ_Level1 = from EQ in eq where EQ.Level == TypeOfLevel.Level1 select EQ;
         var selectEQ_Level2 = from EQ in eq where EQ.Level == TypeOfLevel.Level2 select EQ;
         var selectEQ_Level3 = from EQ in eq where EQ.Level == TypeOfLevel.Level3 select EQ;



         //----------Уровень 0


         foreach (var group0ByPoz in group_selCircuitIds_Level0.GroupBy(poz => poz.Key.ADSK_Позиция))
         {
             int index_baseEq_POZ = 1;
             foreach (var group0 in group0ByPoz) //Сгруппировано по позиции
             {
                 EQ eQ = group0.Key; //Базовый элемент
                 eQ.Имя_панели = eQ.ADSK_Позиция + "-" + index_baseEq_POZ.ToString(); //Запись имени панели в EQ
                 eQ.familyInstance.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME).Set(eQ.Имя_панели); //Запись имени панели в модель
                 eQ.MarkaEQ = SetMarkaEQ(eQ, index_baseEq_POZ, 0, 0, 0);
                 //Запись имени 
                 int index_CircuitId_nextLEVEL = 1;
                 foreach (CircuitId circuit_fromBaseEQ in group0)
                 {
                     foreach (var group0ByPoz_next in circuit_fromBaseEQ.EQFromBaseEquipment.GroupBy(p => p.ADSK_Позиция))
                     {
                         int index_POZ_nextLEVEL = 1;
                         foreach (EQ group_next in group0ByPoz_next)
                         {
                             group_next.Имя_панели =
                                 index_baseEq_POZ.ToString()
                                 + "." + group_next.ADSK_Позиция

                                 + "-" + index_CircuitId_nextLEVEL.ToString() //!!!!!!!!! или так _______+ "." + circuit_fromBaseEQ.Номер_цепи.ToString();
                                 + "." + index_POZ_nextLEVEL.ToString(); //Запись имени панели в EQ

                             group_next.familyInstance.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME).Set(group_next.Имя_панели); //Запись имени панели в модель
                             group_next.MarkaEQ = SetMarkaEQ(group_next, index_baseEq_POZ, index_CircuitId_nextLEVEL, index_POZ_nextLEVEL, 0);
                             index_POZ_nextLEVEL++;
                         }
                     }
                     index_CircuitId_nextLEVEL++;
                 }
                 index_baseEq_POZ++;
             }
         }


         //----------Уровень 1


         foreach (var group0ByPoz in group_selCircuitIds_Level1.GroupBy(poz => poz.Key.ADSK_Позиция))
         {
             int index_baseEq_POZ = 1;
             foreach (var group0 in group0ByPoz) //Сгруппировано по позиции
             {
                 EQ eQ = group0.Key; //Базовый элемент
                 //eQ.Имя_панели = eQ.ADSK_Позиция + "-" + index_baseEq_POZ.ToString(); //Запись имени панели в EQ
                 //eQ.familyInstance.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME).Set(eQ.Имя_панели); //Запись имени панели в модель
                 //SetMarkaEQ(eQ, index_baseEq_POZ, 0, 0, 0);
                 //Запись имени 
                 int index_CircuitId_nextLEVEL = 1;
                 foreach (CircuitId circuit_fromBaseEQ in group0)
                 {
                     foreach (var group0ByPoz_next in circuit_fromBaseEQ.EQFromBaseEquipment.GroupBy(p => p.ADSK_Позиция))
                     {
                         int index_POZ_nextLEVEL = 1;
                         foreach (EQ group_next in group0ByPoz_next)
                         {
                             group_next.Имя_панели =
                                 eQ.MarkaEQ.Marka1.ToString()
                                 + "." + group_next.ADSK_Позиция
                                 + "-" + eQ.MarkaEQ.Marka2.ToString()
                                 + "." + eQ.MarkaEQ.Marka3.ToString() //!!!!!!!!! или так _______+ "." + circuit_fromBaseEQ.Номер_цепи.ToString();
                                 + "-" + index_CircuitId_nextLEVEL.ToString()
                                 + "/" + index_POZ_nextLEVEL.ToString(); //Запись имени панели в EQ

                             group_next.familyInstance.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME).Set(group_next.Имя_панели); //Запись имени панели в модель
                             group_next.MarkaEQ = SetMarkaEQ(group_next, index_baseEq_POZ, index_CircuitId_nextLEVEL, index_POZ_nextLEVEL, 0);
                             index_POZ_nextLEVEL++;
                         }
                     }
                     index_CircuitId_nextLEVEL++;
                 }
                 index_baseEq_POZ++;
             }
         }














         MarkaEQ SetMarkaEQ(EQ eq, int m1, int m2, int m3, int m4)
         {
             MarkaEQ markaEQ = new MarkaEQ();
             markaEQ.Marka1 = m1;
             markaEQ.Marka2 = m2;
             markaEQ.Marka3 = m3;
             markaEQ.Marka4 = m4;

             return markaEQ;
         }



         // begin R2021
         /*

         List<EQ> eqList = new List<EQ>();

         foreach (var i in eq)
         {
             
             var aElSys = i.familyInstance.MEPModel.GetAssignedElectricalSystems();
             var ElSys = i.familyInstance.MEPModel.GetElectricalSystems();
         }


         */










     }





}
