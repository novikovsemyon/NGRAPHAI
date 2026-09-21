using Nice3point.Revit.Extensions.Runtime;

namespace NGraph.Core.FunctionalScheme;


    public class DataDescription
    {
        public string NameParamHeader { get; set; } = Const.Param_NS_NoParametr;
        
        /// <summary>
        /// Имя семейства, размещаемое на футоре
        /// </summary>
        public string NameTypeElement { get; set; } = Const.Element_Footor_First;
        //public bool HasTag { get; set; } = true;

        public int Iterator { get; set; }
        /// <summary>
        /// Описание, типизация
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// Принцип работы
        /// </summary>
        public string Description { get; set; }

        public int Di { get; set; } = 0;
        public int Do { get; set; } = 0;
        public int Ai { get; set; } = 0;
        public int Ao { get; set; } = 0;
        public bool HMIpanel { get; set; } = false;
        public bool Interface { get; set; } = false;

        /// <summary>
        /// Щит текст (текст внизу)
        /// </summary>
        public string TextLabel { get; set; } = "";
        /// <summary>
        /// Щит_прибор (окружность внизу)
        /// </summary>
        public bool IsBox { get; set; } = false;
        /// <summary>
        /// Щит_связь линия до отщиты до верхв
        /// </summary>
        public bool IsConnection { get; set; } = false;
        public bool IsActionLeft { get; set; } = false;
        public bool IsActionLeftWithCursorUp { get; set; } = false;
        public bool IsActionLeftWithCursorDown { get; set; } = false;
        public bool IsActionRight { get; set; } = false;
        public bool IsConnectionDown { get; set; } = false;
        public bool IsConnectionDownPoint { get; set; } = false;
        public double DimConnectionLeft { get; set; } = 1;
        public double DimConnectionRight { get; set; } = 1;

        DataDescription(string type, string description)
        {
            Type = type;
            Description = description;

        }






        public static List<DataDescription> FooterElementUser(Document doc,  ElementId elid, bool hastag)
        {
            FamilyInstance HeadeFI = doc.GetElement(elid) as FamilyInstance;
            DataDescription data = new DataDescription(
                HeadeFI.LookupParameter(Const.Param_NS_Type).AsString(),
                HeadeFI.LookupParameter(Const.Param_NS_Princip).AsString()
                
                );
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            string textBox = HeadeFI.LookupParameter(Const.Param_NS_Box).AsString();
            if (textBox.IsNullOrEmpty() == false)
            {
                data.IsBox = true;
                data.TextLabel = textBox;
                data.IsConnection = true;
            }

            if (hastag) { data.NameTypeElement = Const.Element_Footor_Text; }
            
            data.NameParamHeader = Const.Param_N_;
            data.Di = HeadeFI.LookupParameter(Const.Param_NS_Di).AsInteger();
            data.Do = HeadeFI.LookupParameter(Const.Param_NS_Do).AsInteger();
            data.Ai = HeadeFI.LookupParameter(Const.Param_NS_Ai).AsInteger();
            data.Ao = HeadeFI.LookupParameter(Const.Param_NS_Ao).AsInteger();
            //data.HMIpanel = Convert.ToBoolean(HeadeFI.LookupParameter(Const.Param_NS_HMI).AsInteger());
            //data.Interface = Convert.ToBoolean(HeadeFI.LookupParameter(Const.Param_NS_RS).AsInteger());

            data.HMIpanel = HeadeFI.LookupParameter(Const.Param_NS_HMI).AsBool();
            data.Interface = HeadeFI.LookupParameter(Const.Param_NS_RS).AsBool();

            dataDescriptions.Add(data);
            return dataDescriptions;
        }


        public static List<DataDescription> FooterElementUserUnknown(Document doc, ElementId elid)
        {
            FamilyInstance HeadeFI = doc.GetElement(elid) as FamilyInstance;
            DataDescription data = new DataDescription(
                $"НЕТ В БАЗЕ   /{HeadeFI.Name}/",
                $"{HeadeFI.Id.ToString()}"

                );
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            dataDescriptions.Add(data);
            return dataDescriptions;
        }



        /// <summary>
        /// Водяной нагреватель
        /// </summary>
        /// <returns></returns>
        public static List<DataDescription> HVAC_heater_Water()
        {
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            //Температура обратной воды
            DataDescription TEoutWater = new DataDescription("Температура обратной воды т/носителя", "Авария если < 10 С")
            { NameParamHeader = "N_TE", Ai = 1, HMIpanel = true };
            
            //Циркуляционный насос
            DataDescription Motor = new DataDescription("Управление насосом", "ПУСК/СТОП")
            { NameParamHeader = "N_Hu" };
            DataDescription MotorText = new DataDescription("Управление / статус контактора", "ПУСК/СТОП")
            {NameTypeElement = Const.Element_Footor_Text, Di = 1, Do = 1, TextLabel = "NS", IsBox = true, IsConnection = true,
            IsActionLeft = true, IsActionLeftWithCursorUp = true, IsActionRight = true, IsConnectionDown = true, DimConnectionRight = 20  };
            DataDescription MotorTS = new DataDescription("Тепловая защита насоса", "Авария/норма")
            { NameParamHeader = "N_Hts", Di=1};
            DataDescription MotorWork = new DataDescription("Режим работы насоса", "Авто-0-Ручное")
            { NameTypeElement = Const.Element_Footor_Text,Di = 1,TextLabel = "HS",IsBox = true,IsConnection = true,IsConnectionDown = true};
            
            //Клапан 3-х ходовой
            DataDescription Valve3 = new DataDescription("Управление / положение клапана т/носителя", "0...100%")
            { NameParamHeader = "N_MY", Ai = 1, Ao=1 };

            //Термостат защиты от обмерзания
            DataDescription TS = new DataDescription("\"Угроза замораживания калорифера", "7°С")
            { NameParamHeader = "N_TS", Di = 1};

            dataDescriptions.AddRange([TEoutWater,Motor, MotorText, MotorTS, MotorWork, Valve3, TS]);
            return dataDescriptions;
        }

        /// <summary>
        /// Приточный вентилятор
        /// </summary>
        /// <returns></returns>
        public static List<DataDescription> HVAC_Motor_IN()
        {
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            //Приточный вентилятор
            DataDescription OnOff = new DataDescription("Пуск/стоп приточного вентилятора", "Пуск/стоп")
            { NameParamHeader = "N_SY", Do = 1, HMIpanel = true };

            DataDescription Motor = new DataDescription("Задание частоты приточного вентилятора", "0...10В")
            { Ao=1, NameTypeElement = Const.Element_Footor_Second };

            DataDescription NS = new DataDescription("Авария ПЧ приточного вентилятора", "Норма/Авария")
            { Di = 1, NameTypeElement = Const.Element_Footor_Next, TextLabel = "NS", IsBox = true, IsConnection = true, };
            //PDS
            DataDescription PDS = new DataDescription("Контроль работы приточного вентилятора", "0...500 Па")
            { NameParamHeader = "N_PDS", Do = 1, HMIpanel = true };

            dataDescriptions.AddRange([OnOff, Motor, NS, PDS]);
            return dataDescriptions;
        }

        /// <summary>
        /// Вытяжной вентилятор
        /// </summary>
        /// <returns></returns>
        public static List<DataDescription> HVAC_Motor_Out()
        {
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            //Вытяжной вентилятор
            DataDescription OnOff = new DataDescription("Пуск/стоп вытяжного вентилятора", "Пуск/стоп")
            { NameParamHeader = "N_SY", Do = 1, HMIpanel = true };

            DataDescription Motor = new DataDescription("Задание частоты вытяжного вентилятора", "0...10В")
            { Ao = 1, NameTypeElement = Const.Element_Footor_Second };

            DataDescription NS = new DataDescription("Авария ПЧ вытяжного вентилятора", "Норма/Авария")
            { Di = 1, NameTypeElement = Const.Element_Footor_Next, TextLabel = "NS", IsBox = true, IsConnection = true};
            //PDS
            DataDescription PDS = new DataDescription("Контроль работы вытяжного вентилятора", "0...500 Па")
            { NameParamHeader = "N_PDS", Do = 1, HMIpanel = true };

            dataDescriptions.AddRange([OnOff, Motor, NS, PDS]);
            return dataDescriptions;
        }

        /// <summary>
        /// Фильтр воздушный
        /// </summary>
        /// <returns></returns>
        public static List<DataDescription> HVAC_Filter()
        {
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            //Фильтр
            DataDescription OnOff = new DataDescription("Котроль засоренности воздушного фильтра", "50...500 Па")
            { NameParamHeader = "N_PDS", Di = 1, HMIpanel = true };
            dataDescriptions.AddRange([OnOff]);
            return dataDescriptions;
        }



        /// <summary>
        /// Пластинчатый рекуператор
        /// </summary>
        /// <returns></returns>
        public static List<DataDescription> HVAC_PlateHeatRecovery()
        {
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            DataDescription OnOff = new DataDescription("Котроль угрозы обмерзания рекуператора", "50...500 Па")
            { NameParamHeader = "N_PDS", Di = 1, HMIpanel = true };
            
            DataDescription Te = new DataDescription("Температура вытяжки после рекуператора", "2...5 С")
            { NameParamHeader = "N_TE", Ai = 1, HMIpanel = true };

            dataDescriptions.AddRange([OnOff,Te]);
            return dataDescriptions;
        }



        /// <summary>
        /// Роторный рекуператор
        /// </summary>
        /// <returns></returns>
        public static List<DataDescription> HVAC_RotaryHeatRecovery()
        {
            List<DataDescription> dataDescriptions = new List<DataDescription>();

            DataDescription OnOff = new DataDescription("Пуск/стоп рекуператора", "Пуск/стоп")
            { NameParamHeader = "N_SY", Do = 1, HMIpanel = true };

            DataDescription F = new DataDescription("Задание частоты вращения рекуператора", "0...10В")
            { NameTypeElement = Const.Element_Footor_Second, NameParamHeader = "N_SY", Ao = 1, HMIpanel = true };

            DataDescription Al = new DataDescription("Авария ПЧ рекуператора", "Норма/Авария")
            { NameTypeElement = Const.Element_Footor_Next, Di = 1, HMIpanel = true, TextLabel = "HA", IsBox = true, IsConnection = true };

            DataDescription Te = new DataDescription("Температура вытяжки после рекуператора", "2...5 С")
            { NameParamHeader = "N_TE", Ai = 1, HMIpanel = true };

            dataDescriptions.AddRange([OnOff, F, Al, Te]);
            return dataDescriptions;
        }































        /*
        List<DataDescription> HVAC_heater_Woter(Document doc)
        {
            List<DataDescription> dataDescriptions = new List<DataDescription>();
            var str1 = "Температура обратной воды т/носителя";
            var str2 = "Авария если < 10 С";
            DataDescription data = new DataDescription(str1, str2);
            data.NameTypeElement = Const.Element_Footor_First;
            data.NameParamHeader = "N_TE";

            data.Di = 0;
            data.Do = 0;
            data.Ai = 1;
            data.Ao = 0;
            data.HMIpanel = true;
            data.Inreface = true;
            data.TextLabel = "" //Щит текст
            data.IsBox = false; //прибор
            data.IsConnection = false; //связь
            data.IsActionLeft = false; //Воздействие слева
            data.IsActionLeftWithCursorUp = false; //стрелка вверх
            data.IsActionLeftWithCursorDown = false; //стрелка вниз
            data.IsActionRight = false; //Воздействие справа
            data.IsConnectionDown = false; // Соединитель снизу
            data.IsConnectionDownPoint = false; //Точка соединителя
            data.ConnectionLeft = 1; //Линия влево
            data.ConnectionRight = 1; //Линия вправо
            dataDescriptions.Add(data);
            return dataDescriptions;
        }
        */
    }

