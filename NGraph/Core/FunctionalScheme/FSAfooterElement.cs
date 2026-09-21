namespace NGraph.Core.FunctionalScheme;

/// <summary>
/// Модель элемента на футоре
/// </summary>
public class FSAfooterElement : FSAentity
{
    public FSAfooterElement(FamilyInstance fi , DataDescription dataDescription) : base(fi)
    {
        DataDescription = dataDescription;
    }

    public DataDescription DataDescription { get;}




    /// <summary>
    /// Запись параметров в экземпляр семейства из DataDescription
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="list"></param>
    public static void Set(Document doc, List<FSAfooterElement> list)
    {
        
        foreach (var e in list)
        {
            var fi = doc.GetElement(e.ID);
            var de = e.DataDescription;

            fi.LookupParameter(Const.Param_NS_Position).Set(de.Iterator);
            fi.LookupParameter(Const.Param_NS_Type).Set(de.Type);
            fi.LookupParameter(Const.Param_NS_Princip).Set(de.Description);
            fi.LookupParameter(Const.Param_NS_Di).Set(de.Di);
            fi.LookupParameter(Const.Param_NS_Do).Set(de.Do);
            fi.LookupParameter(Const.Param_NS_Ai).Set(de.Ai);
            fi.LookupParameter(Const.Param_NS_Ao).Set(de.Ao);
            fi.LookupParameter(Const.Param_NS_HMI).Set(de.HMIpanel);
            fi.LookupParameter(Const.Param_NS_RS).Set(de.Interface);
            fi.LookupParameter(Const.Param_NS_text).Set(de.TextLabel);
            fi.LookupParameter(Const.Param_NS_element).Set(de.IsBox);
            fi.LookupParameter(Const.Param_NS_net).Set(de.IsConnection);
            fi.LookupParameter(Const.Param_NS_toLleft).Set(de.IsActionLeft);
            fi.LookupParameter(Const.Param_NS_toLeftWithUp).Set(de.IsActionLeftWithCursorUp);
            fi.LookupParameter(Const.Param_NS_toRightWithDown).Set(de.IsActionLeftWithCursorDown);
            fi.LookupParameter(Const.Param_NS_toRight).Set(de.IsActionRight);
            fi.LookupParameter(Const.Param_NS_ConnectionDown).Set(de.IsConnectionDown);
            fi.LookupParameter(Const.Param_NS_ConnectionDownWithPoint).Set(de.IsConnectionDownPoint);
            fi.LookupParameter(Const.Param_NS_ConnectionLeft).Set(de.DimConnectionLeft / 304.8);
            fi.LookupParameter(Const.Param_NS_ConnectionRight).Set(de.DimConnectionRight / 304.8);
        }
    }


}

    