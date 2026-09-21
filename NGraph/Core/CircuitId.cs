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
    public List<VertexId> VerticesFromBaseEquipment { get; set; } = new List<VertexId>();
    public List<EQ> EQFromBaseEquipment { get; set; } = new List<EQ>();

    public string NSA_Цепь_комментарий { get; set; } = string.Empty;
    public string NSA_Цепь_назначение { get; set; } = string.Empty;
    public bool NSA_Цепь_звезда { get; set; }

    //REVIT PARAMS
    public string Номер_слота { get; } = string.Empty;
    public string Номер_цепи { get; } = string.Empty;
    public string Имя_нагрузки { get; set; } = string.Empty;
    public string Имя_панели { get; } = string.Empty;
    public string Тип_системы { get; } = string.Empty;
    public double Длина { get; }
    public int Количество_элементов { get; }
    public string Комментарии { get; set; } = string.Empty;
    public ElectricalSystemType Тип { get; }

    //NSA PARAMS
    public string NSA_Кабель_артикул { get; } = string.Empty;
    public string NSA_Кабель_производитель { get; } = string.Empty;
    public string NSA_Кабель_жилы_сечение { get; } = string.Empty;
    public string NSA_Кабель_комментарий { get; } = string.Empty;
    public string NSA_Кабель_марка { get; } = string.Empty;
    public string NSA_Кабель_наименование { get; } = string.Empty;
    public int NSA_Цепь_внешн_диам { get; }



    public CircuitId(Element element, Document _doc)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (_doc is null) throw new ArgumentNullException(nameof(_doc));
        if (element is not ElectricalSystem electricalSystem)
            throw new ArgumentException("Элемент должен быть электрической цепью.", nameof(element));

        Element = element;
        FI_BaseEquipment = electricalSystem.BaseEquipment
            ?? throw new InvalidOperationException($"У электрической цепи {element.Id} не назначено базовое оборудование.");

        foreach (FamilyInstance familyInstance in electricalSystem.Elements.OfType<FamilyInstance>())
            FI_FromBaseEquipment.Add(familyInstance);

        NSA_Цепь_комментарий = ReadStringParameter(element, "NS_Цепь_комментарий");

        var purposeParameter = element.LookupParameter("NS_Цепь_назначение");
        var purposeElement = purposeParameter is null ? null : _doc.GetElement(purposeParameter.AsElementId());
        NSA_Цепь_назначение = purposeElement?.Name ?? "!NO TYPE";

        var starParameter = element.LookupParameter("NS_Цепь_звезда");
        if (starParameter is null)
        {
            NSA_Цепь_звезда = false;
            TaskDialog.Show("Ошибка параметра", "Параметр NS_Цепь_звезда отсутствует. Использовано значение «Нет».");
        }
        else
        {
            NSA_Цепь_звезда = starParameter.AsInteger() != 0;
        }

        Номер_цепи = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER)?.AsString() ?? string.Empty;
        Имя_панели = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_PANEL_PARAM)?.AsString() ?? string.Empty;
        Имя_нагрузки = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NAME)?.AsString() ?? string.Empty;
        Тип_системы = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_TYPE)?.AsValueString() ?? string.Empty;
        Длина = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_LENGTH_PARAM)?.AsDouble() ?? 0d;
        Количество_элементов = element.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER_OF_ELEMENTS_PARAM)?.AsInteger() ?? 0;
        Комментарии = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? string.Empty;
        Тип = electricalSystem.SystemType;

        ReloadNS_parametrs_fromR2019_to2021(element, _doc);

        NSA_Кабель_артикул = ReadStringParameter(element, "NS_Кабель_артикул");
        NSA_Кабель_производитель = ReadStringParameter(element, "NS_Кабель_производитель");
        NSA_Кабель_жилы_сечение = ReadStringParameter(element, "NS_Кабель_жилы_сечение");
        NSA_Кабель_комментарий = ReadStringParameter(element, "NS_Кабель_комментарий");
        NSA_Кабель_марка = ReadStringParameter(element, "NS_Кабель_марка");
        NSA_Кабель_наименование = ReadStringParameter(element, "NS_Кабель_наименование");
        NSA_Цепь_внешн_диам = element.LookupParameter("NS_Цепь_внешн.диам.мм.")?.AsInteger() ?? 0;
    }

    private static string ReadStringParameter(Element element, string parameterName)
    {
        return element.LookupParameter(parameterName)?.AsString() ?? string.Empty;
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
            if (item is not ElectricalSystem electricalSystem || electricalSystem.BaseEquipment is null)
                continue;

            var vertexId = GraphId.GetVertexId(graphId, electricalSystem.BaseEquipment.Id);
            if (vertexId is null)
                continue;

            CircuitId circuit = new CircuitId(item, _doc);
            List<VertexId> listVertxId = new List<VertexId>();
            List<EQ> listEQ = new List<EQ>();

            foreach (FamilyInstance familyInstance in electricalSystem.Elements.OfType<FamilyInstance>())
            {
                var connectedVertex = GraphId.GetVertexId(graphId, familyInstance.Id);
                if (connectedVertex is null)
                    continue;

                listVertxId.Add(connectedVertex);
                listEQ.Add(connectedVertex.eQ);
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
        Transaction? transaction = null;
        try
        {
            if (!_doc.IsModifiable)
            {
                transaction = new Transaction(_doc, "Перезапись параметра по цепи");
                transaction.Start();
            }

            CopyStringParameter(element, "ПП_NS_Кабель_артикул", "NS_Кабель_артикул");
            CopyStringParameter(element, "ПП_NS_Кабель_производитель", "NS_Кабель_производитель");
            CopyStringParameter(element, "ПП_NS_Кабель_комментарий", "NS_Кабель_комментарий");
            CopyStringParameter(element, "ПП_NS_Кабель_марка", "NS_Кабель_марка");
            CopyStringParameter(element, "ПП_NS_Кабель_наименование", "NS_Кабель_наименование");
            CopyStringParameter(element, "ПП_NS_Кабель_жилы_сечение", "NS_Кабель_жилы_сечение");

            var diameterTarget = element.LookupParameter("NS_Цепь_внешн.диам.мм.");
            var diameterSource = element.LookupParameter("ПП_NS_Цепь_внешн.диам.мм");
            if (diameterTarget is not null && !diameterTarget.IsReadOnly && diameterSource is not null)
            {
                int.TryParse(diameterSource.AsString(), out var diameter);
                diameterTarget.Set(diameter);
            }

            if (transaction?.GetStatus() == TransactionStatus.Started)
                transaction.Commit();
        }
        catch (Exception ex)
        {
            if (transaction?.GetStatus() == TransactionStatus.Started)
                transaction.RollBack();

            TaskDialog.Show("Ошибка", "Не удалось перезаписать параметры цепи.\n" + ex.Message);
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private static void CopyStringParameter(Element element, string sourceName, string targetName)
    {
        var source = element.LookupParameter(sourceName);
        var target = element.LookupParameter(targetName);
        if (source is null || target is null || target.IsReadOnly)
            return;

        target.Set(source.AsString() ?? string.Empty);
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
