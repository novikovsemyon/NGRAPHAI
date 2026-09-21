namespace NGraph.Core.FunctionalScheme;


    /// <summary>
    /// Константы проекта
    /// </summary>
    static class Const

    {
        

        public const string Element_Footor_First = "FSA_CABEL_FIRST";
        public const string Element_Footor_Second = "FSA_CABEL_SECOND";
        public const string Element_Footor_Next = "FSA_CABEL_NEXT";
        public const string Element_Footor_Text = "FSA_CABEL_TEXT";

        /// <summary>
        /// Имя семейства элемента узла, определяющее оборудование на функциональной схеме
        /// </summary>
        public const string Element_Header = "Окружность 10 мм";

        /// <summary>
        /// Имя семейства элемента узла, определяющее элемент структуры на функциональной схеме
        /// </summary>
        public const string Element_Header_Users = "FAS_точка";
        
        public const string Element_Header_Users_NoTag = "FAS_точка_без маркировки";
        /// <summary>
        /// Имя семейства типовой аннотации, определяющее элемент футора на функциональной схеме
        /// </summary>
        public const string Element_Footor = "_Подвал";

        /// <summary>
        /// Имя семейства элемента узла, определяющее номер кабеля на функциональной схеме
        /// </summary>
        public const string Element_CabelForFootor = "Вертикально";

        /// <summary>
        /// Имя семейства элемента марки
        /// </summary>
        public const string Element_Tag_CabelForFootor = "BE_Марка_Элемент_узла";


        public const string Param_NS_ElementId = "NS_ElementId";


        public const string Param_NS_ElementId_guid = "1ded0a07-6d53-4277-83bd-1b7ce65336f8";



        public const string Param_ADSK_Position = "ADSK_Позиция";
        public const string Param_ADSK_Group = "ADSK_Группирование";

        //public const string Param_NS_Equpment = "FAS_Установка";
        public const string Param_NS_Equpment = "_Установка";
        
        public const string Param_NS_PanelName = "NS_Имя панели";

        /// <summary>
        /// _Установка
        /// </summary>
        public const string Param_NS_Equpment_guid = "744cc9e7-fa0b-47fb-9467-5f20f01905e9";
        


        public const string Param_NS_IsVisible = "NS_Видимость";
        public const string Param_NS_IsVisible_guid = "b714bca1-3327-434d-a639-ef8fff42a463";


       // public const string Param_NS_GOST= "ГОСТ 21.208";
        //public const string Param_NS_GOST_guid = "ed5b2b60-8aba-4fcd-bb4b-6de1a18c1710";
        public const string Param_NS_GOST = "_ГОСТ 21.208";
        public const string Param_NS_GOST_guid = "0ff8cfc0-12db-45c2-89c2-c9f4e10ad8fe";


        //public const string Param_NS_Box = "Щит_";
        public const string Param_NS_Box = "_Элемент_на_щите";
        public const string Param_NS_Box_guid = "7c233692-a955-43b1-98d1-9c1b19891825";


        //Параметры элементов футора
        public const string Param_NS_Position = "NS_Позиция";
        //public const string Param_NS_Type = "Типизация";
        
        public const string Param_NS_Type = "_Типизация";
        public const string Param_NS_Type_guid = "246edbe2-e6d2-465a-a329-0cf2cb41e864";
        //public const string Param_NS_Princip = "Принцип";

        public const string Param_NS_Princip = "_Принцип";
        public const string Param_NS_Princip_guid = "161926b2-e4ac-43f5-9440-d08abfe1cd28";
        //public const string Param_NS_Di = "DI_количество";
        public const string Param_NS_Di = "_Din";
        public const string Param_NS_Di_guid = "2b570b87-e2d0-48f3-a814-ab6fb41945be";

        //public const string Param_NS_Do = "DO_количество";
        public const string Param_NS_Do = "_Dout";
        public const string Param_NS_Do_guid = "4b5ae5d1-bb3a-45e7-ae72-adda2cc2725a";

        //public const string Param_NS_Ai = "AI_количество";
        public const string Param_NS_Ai = "_Ain";
        public const string Param_NS_Ai_guid = "50c0ad55-f781-476f-8dac-d2b9fd70188a";

        //public const string Param_NS_Ao = "AO_количество";
        public const string Param_NS_Ao = "_Aout";
        public const string Param_NS_Ao_guid = "6354e063-4dc4-40dd-a14b-8b77ca812c6a";

        //public const string Param_NS_HMI = "HMI/Сигнал-я, управл-е";
        public const string Param_NS_HMI = "_HMI";
        public const string Param_NS_HMI_guid = "305f204c-b35a-4191-a18b-564415625e87";

        //public const string Param_NS_RS = "RS485, Modbus/Ethernet, Profinet";
        public const string Param_NS_RS = "_Interface";
        public const string Param_NS_RS_guid = "d6a7c127-4cdd-4464-92a9-63a3a70f1fc1";

        public const string Param_NS_text = "Щит_текст";
        public const string Param_NS_element = "Щит_прибор";
        public const string Param_NS_net = "Щит_связь";
        public const string Param_NS_toLleft = "Воздействие_слева";
        public const string Param_NS_toLeftWithUp = "Воздействие_слева_стрелка_вверх";
        public const string Param_NS_toRightWithDown = "Воздействие_слева_стрелка_вниз";
        public const string Param_NS_toRight = "Воздействие_справа";
        public const string Param_NS_ConnectionDown = "Соединитель_снизу";
        public const string Param_NS_ConnectionDownWithPoint = "Соединитель_снизу_точка";
        public const string Param_NS_ConnectionLeft = "Соединитель_влево";
        public const string Param_NS_ConnectionRight = "Соединитель_вправо";

        //Параметры элементов футора
        /// <summary>
        /// Длина футора
        /// </summary>
        public const string Param_NS_LinghtOfFooter = "Количество_кабелей";

        public const string Param_NS_NoParametr = "Нет_параметра";


        public const string Param_CJ_Lenght = "CJ_Длина кабеля";
        public const string Param_CJ_Lenght_guid = "b2016ce4-0476-4105-8f6c-6fcadbed0297";
        
        public const string Param_CJ_Mark = "CJ_Марка кабеля";
        public const string Param_CJ_Mark_guid = "5730a9bf-adba-4f5d-b2ba-3956266c3be5";
        
        public const string Param_CJ_Nets = "CJ_Жилы и сечение";
        public const string Param_CJ_Nets_guid = "4e286dc1-2e30-48fd-a484-470784ee613d";
        
        public const string Param_CJ_Number = "CJ_Номер кабеля";
        public const string Param_CJ_Number_guid = "CJ_Номер кабеля";
        
        public const string Param_CJ_Begin = "CJ_Начало кабеля";
        public const string Param_CJ_Begin_guid = "2588f23d-62e7-4999-9085-1146ef34f6e4";
       
        public const string Param_CJ_End = "CJ_Окончание кабеля";
        public const string Param_CJ_End_guid = "f304a42d-8659-401f-973b-59a5ae1494cf";

        public const string Param_CJ_Begin_eq = "CJ_Начало кабеля_eq";
        public const string Param_CJ_End_eq = "CJ_Окончание кабеля_eq";
       
        public const string Param_CJ_Reload = "CJ_Перезапись параметров";

        public const string Param_CJ_Work = "CJ_Рабочий набор";
        public const string Param_CJ_Work_guid = "a4986405-436a-43ed-8006-51253298ee64";

        



        //public const string Param_N_number = "N_number"; be6abf4b-bdcb-48b6-b9ab-4d7695e9617b

        /// <summary>
        /// Порядковый номер
        /// </summary>
        public const string Param_N_ = "N_data";
        public const string Param_N_guid = "4aa46fac-bd1e-4ae8-b6b3-4141ffc1ef14";
        public const string Param_N_data = "data";
        public const string Param_N_data_guid = "be6abf4b-bdcb-48b6-b9ab-4d7695e9617b";
        
        
    }

    