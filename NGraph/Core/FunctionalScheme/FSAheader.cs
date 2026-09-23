namespace NGraph.Core.FunctionalScheme;

/// <summary>
/// Элемент структурной схемы, по которому строится Footor
/// </summary>
public class FSAheader: FSAentity, IComparable<FSAheader>
{
    public int  index { get; set; }
    public ElementHeader? ElementHeader { get; set; }
    /// <summary>
    /// Пересекающиеся точки включая исходную или другой элемент узла представляющий прибор
    /// </summary>
    public List<FSAheader> Crossing { get; set; } = [];

    internal bool IsVisible { get;}

    /// <summary>
    /// Подлежит сквозной маркировке
    /// </summary>
    internal bool isNumbering { get; }
    public string Gost { get; }
    /// <summary>
    /// Порядковый номер
    /// </summary>
    public Parameter Parameter_position_number { get; }
    public string Parameter_position_number_set { get; set; } = string.Empty;

    /// <summary>
    /// Номер элемента с позицией
    /// </summary>
    public Parameter Parameter_name_element { get; }
    public string Parameter_name_element_set { get; set; } = string.Empty;

    public Group? GroupRevit { get; set; }
    
    public CableLog CableLog { get; }
                
    public FSAheader(FamilyInstance fi) : base(fi)
    {

        Group = fi.get_Parameter(new Guid(Const.Param_NS_Equpment_guid)).AsString();
        IsVisible = fi.get_Parameter(new Guid(Const.Param_NS_IsVisible_guid)).AsBool();
            
        if (fi.Name == Const.Element_Header_Users) isNumbering = true;
        else if (fi.Name == Const.Element_Header_Users_NoTag) isNumbering = false;
        else isNumbering = false;


        //rev1 без типовых аннотаций
        Parameter_position_number = fi.get_Parameter(new Guid(Const.Param_N_guid));//Порядковый номер
        Parameter_name_element = fi.get_Parameter(new Guid(Const.Param_N_data_guid)); //Номер элемента с позицией
        Gost = fi.get_Parameter(new Guid(Const.Param_NS_GOST_guid)).AsString();

        CableLog = new CableLog();


    }

    public int CompareTo(FSAheader? obj)
    {
        return obj is null ? 1 : int.Parse(Parameter_position_number.AsString()).CompareTo(int.Parse(obj.Parameter_position_number.AsString()));

    }
}

