using System.IO;
namespace NGraph.Core;

public class EQ
{
  
    public string hisNameSymbolForStruct { get; set; } = string.Empty; //This is for geting(creating) FamilySymbol
    public TypeOfLevel Level { get; set; }
    public FormatSxema formatSxema { get; set; }
    public Orientation OrientationForMarka_ID { get; set; }
    public bool HaveMark_Always { get; set; } = false;
    /// <summary>
    /// Определяет рост цепей относительно базового элемента
    /// </summary>
    public FormatGrowing FormatGrowing { get; set; }
    public string Помещение_Имя { get; }
    public string Зона_имя { get; } = "не назначено";
    public string ADSK_Позиция { get; }
    public string ADSK_Марка { get; }
    public string ADSK_Завод_изготовитель { get; }
    public string ADSK_Код_изделия { get; }
    public string ADSK_Номер_контроллера { get; }
    public string ADSK_Номер_устройства { get; set; } = string.Empty;
    public string ADSK_Принадлежность_к_разделу { get; }
    public string ADSK_Наименование { get; }
    public string ADSK_Наименование_краткое { get; }
    public string ADSK_Группирование { get; }
    public string Имя_панели { get; set; } = string.Empty;
    public List<LogicPort> Ports { get; set; } = new List<LogicPort>();
    //public VertexId Vertex { get; }
    public FamilyInstance familyInstance { get; }

    //public ElementId ElementId { get; }



    public MarkaEQ MarkaEQ { get; set; } = new();
    public int Marka1 { get; set; }
    public int Marka2 { get; set; }
    public int Marka3 { get; set; }
    public int Marka4 { get; set; }


    public EQ(FamilyInstance fi)
    {
        familyInstance = fi;
        var space = fi.Space;
        Помещение_Имя = space?.Name ?? "нет помещения";
#if REVIT2027_OR_GREATER
        // В Revit 2027 прежние HVAC-зоны заменены зонами, связанными с пространствами.
        if (space is not null)
        {
            var zoneNames = new FilteredElementCollector(fi.Document)
                .OfClass(typeof(Autodesk.Revit.DB.Analysis.GenericZone))
                .Cast<Autodesk.Revit.DB.Analysis.GenericZone>()
                .Where(zone => zone.GeometricDefinition == Autodesk.Revit.DB.Analysis.ZoneGeometricDefinition.Spaces
                    && zone.GetSpaceIds().Contains(space.Id))
                .Select(zone => zone.Name).Distinct().OrderBy(name => name).ToList();
            if (zoneNames.Count > 0) Зона_имя = string.Join(", ", zoneNames);
        }
#else
        Зона_имя = space?.Zone?.Name ?? "не назначено";
#endif

        ADSK_Позиция = GetParametrFromElementId("ADSK_Позиция");
        ADSK_Номер_контроллера = GetParametrFromElementId("ADSK_Номер контроллера");
        ADSK_Номер_устройства = GetParametrFromElementId("ADSK_Номер устройства");
        ADSK_Принадлежность_к_разделу = GetParametrFromElementId("ADSK_Принадлежность к разделу");
        ADSK_Наименование = GetParametrFromElementId("ADSK_Наименование");
        ADSK_Наименование_краткое = GetParametrFromElementId("ADSK_Наименование краткое");
        ADSK_Марка = GetParametrFromElementId("ADSK_Марка");
        ADSK_Код_изделия = GetParametrFromElementId("ADSK_Код изделия");
        ADSK_Завод_изготовитель = GetParametrFromElementId("ADSK_Завод-изготовитель");
        ADSK_Группирование = GetParametrFromElementId("ADSK_Группирование");
        //Имя_панели = GetParametrFromElementId("Имя панели");
        try
        {
            Имя_панели = familyInstance.get_Parameter(BuiltInParameter.RBS_ELEC_PANEL_NAME).AsString();
        }
        catch
        {
            Имя_панели = GetParametrFromElementId("Имя панели");
        }

    }



    /// <summary>
    /// Получить текстовый параметр по ElementId
    /// </summary>
    /// <param name="nameOfParametr"></param>
    /// <returns></returns>
    public string GetParametrFromElementId(string nameOfParametr)
    {
        string parametr = "";
        //var param = RevitAPI.Document.GetElement(ElementId);
        if (familyInstance.Symbol.LookupParameter(nameOfParametr) != null)
        {
            parametr = familyInstance.Symbol.LookupParameter(nameOfParametr).AsString();
            /*
            try { parametr = (param as FamilyInstance).Symbol.LookupParameter(nameOfParametr).AsString(); }
            catch { try { parametr = param.LookupParameter(nameOfParametr).AsString(); } catch { parametr = "нет параметра"; } }
            */
        }
        else if (familyInstance.LookupParameter(nameOfParametr) != null)
        {
            parametr = familyInstance.LookupParameter(nameOfParametr).AsString();
        }

        return parametr;
    }



        
        public void EQipment(EQ eQs, bool fromIni)
        {

            var mydocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            //string locationAddin = Document.Application.CurrentUserAddinsLocation;
            string[] allFoundFiles = Directory.GetFiles(@$"{mydocumentsPath}\REVIT\BETA-BIM\Настройки\", "*.ini", SearchOption.AllDirectories);

            //Имя файла - ADSK_Группирование
            //aSection [ADSK_Позиция]
            //
            
            foreach (string file in allFoundFiles)
            {
                INIManager readini = new INIManager(file);

                if (eQs.ADSK_Группирование == Path.GetFileNameWithoutExtension(file))
                {
                    List<string> ADSK_Pozitions = new List<string>();
                    foreach (string line in File.ReadAllLines(file))
                    {
                        if (line.StartsWith("[") & line.Trim().EndsWith("]"))
                        {
                            
                            ADSK_Pozitions.Add(line.Substring(1, line.Length - 2));
                        }
                    }
                    foreach (string aSection in ADSK_Pozitions)
                    {
                        if (eQs.ADSK_Позиция == aSection)
                        {
                            //set_Ports(eQs, 0, 0, 0, 0, 2, 1);
                            
                            eQs.hisNameSymbolForStruct = readini.GetPrivateString(aSection, "Symbol");
                            
                            switch (readini.GetPrivateString(aSection, "Level"))
                            {
                                case "0":
                                    eQs.Level = TypeOfLevel.Level0;
                                    break;
                                case "1":
                                    eQs.Level = TypeOfLevel.Level1;
                                    break;
                                case "2":
                                    eQs.Level = TypeOfLevel.Level2;
                                    break;
                                case "3":
                                    eQs.Level = TypeOfLevel.Level3;
                                    break;
                            }
                            switch (readini.GetPrivateString(aSection, "FormatSxema"))
                            {
                                case "Hor":
                                    eQs.formatSxema = FormatSxema.HorizontalFromLeftToRight;
                                    break;
                                case "Ver":
                                    eQs.formatSxema = FormatSxema.VerticalFromDownToUp;
                                    break;
                               
                            }
                            switch (readini.GetPrivateString(aSection, "OrientationMarka"))
                            {
                                case "Hor":
                                    eQs.OrientationForMarka_ID = Orientation.Горизонтально;
                                    break;
                                case "Ver":
                                    eQs.OrientationForMarka_ID = Orientation.Вертикально;
                                    break;

                            }
                            switch (readini.GetPrivateString(aSection, "FormatGrowing"))
                            {
                                case "Right":
                                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;
                                    break;
                                case "Up":
                                    eQs.FormatGrowing = FormatGrowing.CircuitId_UP ;
                                    break;

                            }

                            switch (readini.GetPrivateString(aSection, "HaveMarkAlways"))
                            {
                                case "Yes":
                                    eQs.HaveMark_Always = true;
                                    break;
                                case "No":
                                    eQs.HaveMark_Always = false;
                                    break;

                            }




                        }
                        
                    }
                }
                


            }


        }


        

        /// <summary>
        /// Назначение оборудованию выходов, коннекторов, тип
        /// </summary>
        /// <param name="eQs"></param>
        public void EQipment(EQ eQs)
        {
            if (eQs.ADSK_Группирование == "АК_Газ") //Важный параметр - по нему присваивается тип оборудования к VertexID при создании графа
            {
                if (eQs.ADSK_Позиция == "ЩА")
                {
                    set_Ports(eQs, 0, 0, 0, 0, 2, 1);
                    eQs.hisNameSymbolForStruct = "ППКОП";
                    eQs.Level = TypeOfLevel.Level2;
                    eQs.formatSxema = FormatSxema.VerticalFromDownToUp;
                    eQs.OrientationForMarka_ID = Orientation.Вертикально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;

                    //Стуктурная схема


                }

                else if (eQs.ADSK_Позиция == "ВМР8")
                {
                    set_Ports(eQs, 0, 8, 0, 0, 1, 0);
                    eQs.hisNameSymbolForStruct = "ППКОП";
                    eQs.Level = TypeOfLevel.Level1;
                    eQs.formatSxema = FormatSxema.HorizontalFromLeftToRight;
                    eQs.OrientationForMarka_ID = Orientation.Горизонтально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;
                    eQs.HaveMark_Always = true;
                }


                else if (eQs.ADSK_Позиция == "AI16")
                {
                    set_Ports(eQs, 0, 0, 16, 0, 1, 0);
                    eQs.hisNameSymbolForStruct = "ППКОП";
                    eQs.Level = TypeOfLevel.Level1;
                    eQs.formatSxema = FormatSxema.HorizontalFromLeftToRight;
                    eQs.OrientationForMarka_ID = Orientation.Горизонтально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;
                    eQs.HaveMark_Always = true;
                }

                else if (eQs.ADSK_Позиция == "YA")
                {
                    set_Ports(eQs, TypeOfLogicPort.U12_24, "Свет", TypeOfLogicPort.U12_24, "Звук");
                    eQs.hisNameSymbolForStruct = "Оповещатель";
                    eQs.Level = TypeOfLevel.Level2;
                    eQs.formatSxema = FormatSxema.VerticalFromDownToUp;
                    eQs.OrientationForMarka_ID = Orientation.Вертикально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;
                    eQs.HaveMark_Always = true;
                }


                else if (eQs.ADSK_Позиция == "QE")
                {
                    set_Ports(eQs, TypeOfLogicPort.mA4_20, "4-20mA");
                    eQs.hisNameSymbolForStruct = "Газоанализатор";
                    eQs.Level = TypeOfLevel.Level2;
                    eQs.formatSxema = FormatSxema.VerticalFromDownToUp;
                    eQs.OrientationForMarka_ID = Orientation.Горизонтально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;

                }


                else if (eQs.ADSK_Позиция == "BSU")
                {
                    eQs.hisNameSymbolForStruct = "ППКОП";
                    eQs.Level = TypeOfLevel.Level0;
                    set_Ports(eQs, 0, 0, 0, 0, 2, 1);
                    set_Ports(eQs, TypeOfLogicPort.U12_24, "=24В");
                    eQs.formatSxema = FormatSxema.HorizontalFromLeftToRight;
                    eQs.OrientationForMarka_ID = Orientation.Горизонтально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_UP;
                }
            }


            if (eQs.ADSK_Группирование == "АК_СКС")
            {
                if (eQs.ADSK_Позиция == "ЩА")
                {
                    set_Ports(eQs, 0, 0, 0, 0, 2, 1);
                    eQs.hisNameSymbolForStruct = "УГО - стойка";
                    eQs.Level = TypeOfLevel.Level0;
                    eQs.formatSxema = FormatSxema.VerticalFromDownToUp;
                    eQs.OrientationForMarka_ID = Orientation.Вертикально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;


                }


                else if (eQs.ADSK_Позиция == "VP")
                {
                    set_Ports(eQs, TypeOfLogicPort.U12_24, "Электропитание", TypeOfLogicPort.Eth, "Ethernet");
                    eQs.hisNameSymbolForStruct = "Кнопка вызова";
                    eQs.Level = TypeOfLevel.Level1;
                    eQs.formatSxema = FormatSxema.VerticalFromDownToUp;
                    eQs.OrientationForMarka_ID = Orientation.Вертикально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;
                    eQs.HaveMark_Always = true;
                }


                else if (eQs.ADSK_Позиция == "VD")
                {
                    set_Ports(eQs, TypeOfLogicPort.U12_24, "Электропитание", TypeOfLogicPort.Eth, "Ethernet");
                    eQs.hisNameSymbolForStruct = "Домофон";
                    eQs.Level = TypeOfLevel.Level1;
                    eQs.formatSxema = FormatSxema.VerticalFromDownToUp;
                    eQs.OrientationForMarka_ID = Orientation.Горизонтально;
                    eQs.FormatGrowing = FormatGrowing.CircuitId_Right;

                }


            }

        }
        
        /// <summary>
        /// Один порт
        /// </summary>
        /// <param name="eq"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        public void set_Ports(EQ eq, TypeOfLogicPort type, string name)
        {
            eq.Ports.Add(new LogicPort(type, name));

        }

        /// <summary>
        /// Два порта
        /// </summary>
        /// <param name="eq"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        public void set_Ports(EQ eq, TypeOfLogicPort type1, string name1, TypeOfLogicPort type2, string name2)
        {
            eq.Ports.Add(new LogicPort(type1, name1));
            eq.Ports.Add(new LogicPort(type2, name2));

        }


        /// <summary>
        /// ШКАФ
        /// </summary>
        /// <param name="eq"></param>
        /// <param name="nDi"></param>
        /// <param name="nDo"></param>
        /// <param name="nAi"></param>
        /// <param name="nAo"></param>
        /// <param name="nRS485"></param>
        /// <param name="nEth"></param>
        public static void set_Ports(EQ eq, int nDi, int nDo, int nAi, int nAo, int nRS485, int nEth)
        {
            for (int i = 1; i <= nDi; i++)
            {
                eq.Ports.Add(new LogicPort(TypeOfLogicPort.Din, "Di" + i.ToString()));
            }
            for (int i = 1; i <= nDo; i++)
            {
                eq.Ports.Add(new LogicPort(TypeOfLogicPort.Dout, "Do" + i.ToString()));
            }
            for (int i = 1; i <= nAi; i++)
            {
                eq.Ports.Add(new LogicPort(TypeOfLogicPort.Ain, "Ai" + i.ToString()));
            }
            for (int i = 1; i <= nAo; i++)
            {
                eq.Ports.Add(new LogicPort(TypeOfLogicPort.Aout, "Ao" + i.ToString()));
            }

            for (int i = 1; i <= nRS485; i++)
            {
                eq.Ports.Add(new LogicPort(TypeOfLogicPort.RS, "RS" + i.ToString()));
            }
            for (int i = 1; i <= nEth; i++)
            {
                eq.Ports.Add(new LogicPort(TypeOfLogicPort.Eth, "Eth" + i.ToString()));
            }

        }
}
public class LogicPort
{
    public List<Cabel> Cabel { get; set; } = new List<Cabel>();
    public bool IsConnected { get; set; }
    public TypeOfLogicPort TypeOfLogicPort { get; }
    public string Name { get; }


    public LogicPort(TypeOfLogicPort typeOfLogicPort, string name)
    {
        IsConnected = false;
        Name = name;
        TypeOfLogicPort = typeOfLogicPort;


    }


}

public enum TypeOfLogicPort
{
    Eth, SFP, RS, mA4_20, U10V, Din, Dout, Ain, Aout, U220, Namur, PGU, TL, U12_24, Solid, Resistance, USB
}
// <summary>
/// Задание вида марки
/// (часть1)(часть2)(часть3)(часть4)(часть5)(часть6)(часть7)
/// </summary>
public class MarkaEQ
{

    public string part1 { get; set; } = string.Empty;
    public string part2 { get; set; } = string.Empty;
    public string part3 { get; set; } = string.Empty;
    public string part4 { get; set; } = string.Empty;
    public string part5 { get; set; } = string.Empty;
    public string part6 { get; set; } = string.Empty;
    public string part7 { get; set; } = string.Empty;


    public int Marka1 { get; set; }
    public int Marka2 { get; set; }
    public int Marka3 { get; set; }
    public int Marka4 { get; set; }

    public MarkaEQ()
    {

    }
    

}

public enum TypeOfLevel  //Для определения базового элемента
{
    Ring, Level0, Level1, Level2, Level3

}

/// <summary>
/// Определяет чередование этого элемента внутри цепи
/// </summary>
public enum FormatSxema
{
    HorizontalFromLeftToRight, VerticalFromDownToUp
}

/// <summary>
/// Определяет ориентацию марки относительно символа
/// </summary>
public enum Orientation { Вертикально, Горизонтально }

/// <summary>
/// Определяет рост цепей относительно этого элемента
/// 
/// </summary>
public enum FormatGrowing
{
    CircuitId_UP, CircuitId_Right
}