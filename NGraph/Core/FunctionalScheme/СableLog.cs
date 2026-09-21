namespace NGraph.Core.FunctionalScheme;


/// <summary>
/// Кабельный журнал
/// </summary>
public class CableLog
{
    public string ОбозначениеКабеля { get; set; }
    public string НачалоОбозначение { get; set; }
    public string НачалоОборудование { get; set; }
    public string КонецОбозначение { get; set; }
    public string КонецОборудование { get; set; }
    public string Трасса { get; set; }
    public string МаркаКабеляПоПроекту { get; set; }
    public string СечениеиЖилыПоПроекту { get; set; }
    public int Длина { get; set; }
    public string ПроложеноМаркаКабеляПоПроекту { get; set; }
    public string ПроложеноСечениеиЖилыПоПроекту { get; set; }
    public int ПроложеноДлина { get; set; }

    public bool ПерезаписьПараметров { get; set; }
    public CableLog()
    {
        
    }
    
    
}


    