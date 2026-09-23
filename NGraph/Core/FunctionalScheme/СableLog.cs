namespace NGraph.Core.FunctionalScheme;


/// <summary>
/// Кабельный журнал
/// </summary>
public class CableLog
{
    public string ОбозначениеКабеля { get; set; } = string.Empty;
    public string НачалоОбозначение { get; set; } = string.Empty;
    public string НачалоОборудование { get; set; } = string.Empty;
    public string КонецОбозначение { get; set; } = string.Empty;
    public string КонецОборудование { get; set; } = string.Empty;
    public string Трасса { get; set; } = string.Empty;
    public string МаркаКабеляПоПроекту { get; set; } = string.Empty;
    public string СечениеиЖилыПоПроекту { get; set; } = string.Empty;
    public int Длина { get; set; }
    public string ПроложеноМаркаКабеляПоПроекту { get; set; } = string.Empty;
    public string ПроложеноСечениеиЖилыПоПроекту { get; set; } = string.Empty;
    public int ПроложеноДлина { get; set; }

    public bool ПерезаписьПараметров { get; set; }
    public CableLog()
    {
        
    }
    
    
}


    