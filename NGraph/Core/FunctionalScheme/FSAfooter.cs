namespace NGraph.Core.FunctionalScheme;

/// <summary>
/// Элемент Footor
/// </summary>
public class FSAfooter: FSAentity
{
    public List<FSAheader> FSAheaders { get; set; } = [];
    public List<FSAfooterGroupedElements> FSAfooterGroupedElements { get; set; } = [];
    public FSAfooter(FamilyInstance fi) : base(fi)
    {

    }

    public int Di { get; set; } = 0;
    public int Do { get; set; } = 0;
    public int Ai { get; set; } = 0;
    public int Ao { get; set; } = 0;


    /// <summary>
    /// Длина футора
    /// </summary>
    public int Count_lenght { get; set; }
        
    /// <summary>
    /// Шаг между элементами в футоре
    /// </summary>
    public XYZ StepBehindElementsInFooter { get; set; } = new XYZ(10 / 304.8, 0, 0);


    public void Set_di_do_ai_ao_group(Document doc)
    {
            
        foreach (var grouped in FSAfooterGroupedElements)
        {
            foreach(var elenent in grouped.Elements)
            {
                Di = elenent.DataDescription.Di + Di;
                Do = elenent.DataDescription.Do + Do;
                Ai = elenent.DataDescription.Ai + Ai;
                Ao = elenent.DataDescription.Ao + Ao;
            }

        }

        var fi = doc.GetElement(ID);

        fi.get_Parameter(new Guid(Const.Param_NS_Di_guid)).Set(Di);
        fi.get_Parameter(new Guid(Const.Param_NS_Do_guid)).Set(Do);
        fi.get_Parameter(new Guid(Const.Param_NS_Ai_guid)).Set(Ai);
        fi.get_Parameter(new Guid(Const.Param_NS_Ao_guid)).Set(Ao);
        fi.LookupParameter(Const.Param_NS_Equpment).Set(Group);

    }



}

