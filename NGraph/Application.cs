using Nice3point.Revit.Toolkit.External;
using NGraph.Commands;
using Nice3point.Revit.Extensions.UI;

namespace NGraph;

/// <summary>
///     Application entry point
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        CreateRibbon();
        
        
    }

    private void CreateRibbon()
    {
        
        
        
        const string nGraph = "NGraph";
        Application.CreateRibbonTab(nGraph);

        
        #region Общее
        var panelCom = Application.CreateRibbonPanel(nGraph, "Общее");
        
        var buttonInfo = panelCom.AddPushButton<InfoCommand>("Инфо");
        buttonInfo.SetImage("/NGraph;component/Resources/Icons/NG_info_16.png");
        buttonInfo.SetLargeImage("/NGraph;component/Resources/Icons/NG_info_32.png");
        
#if REVIT2019_OR_GREATER
        var buttonSetting = panelCom.AddPushButton<NGraphSettings>("Настройки");
        buttonSetting.SetImage("/NGraph;component/Resources/Icons/NG_settings_16.png");
        buttonSetting.SetLargeImage("/NGraph;component/Resources/Icons/NG_settings_32.png");
#endif
        #endregion
#if REVIT2019_OR_GREATER
        #region Листы
        var panelSheets = Application.CreateRibbonPanel(nGraph, "Листы");
        var buttonPrint = panelSheets.AddPushButton<PrintTo>("Экспорт");
        buttonPrint.SetImage("/NGraph;component/Resources/Icons/NG_printPDF_DWG_16.png");
        buttonPrint.SetLargeImage("/NGraph;component/Resources/Icons/NG_printPDF_DWG_32.png");
        buttonPrint.LongDescription = "Печать работает при установленном принтере PDF-XChange Standard." +
                                      "Должен быть настроен хотя бы один набор печати в Revit." +
                                      "В настройках принтера в вкладке \"Cохранить\" должен быть отключен флаг\n" +
                                      "\"Cохранить как...\" и /если файл есть, то \"добавлять к существующему\"." +
                                      "Имя файла %[DocName]" +
                                      "Путь сохранения Документы/Экспорт из Revit";
        
        var buttonSheetsNumbering = panelSheets.AddPushButton<NumberingView>("Нумератор\nлистов");
        buttonSheetsNumbering.SetImage("/NGraph;component/Resources/Icons/NG_sheetsNumbering_16.png");
        buttonSheetsNumbering.SetLargeImage("/NGraph;component/Resources/Icons/NG_sheetsNumbering_32.png");
        
        var buttonSheetsFromDrafting = panelSheets.AddPushButton<CreateViewSheetsByDraftingViewes>("Создать листы\nпо чертежам");
        buttonSheetsFromDrafting.SetImage("/NGraph;component/Resources/Icons/NG_sheetsFromDrafting_16.png");
        buttonSheetsFromDrafting.SetLargeImage("/NGraph;component/Resources/Icons/NG_sheetsFromDrafting_32.png");

        
        #endregion
        #region  Нумераторы
        var panelNunbering = Application.CreateRibbonPanel(nGraph, "Нумераторы");
        var buttonNumberingByLine = panelNunbering

            .AddPushButton<CrossLineWithElementsInViewByGraph>("Нумератор\nпо линии");
        buttonNumberingByLine.SetImage("/NGraph;component/Resources/Icons/NG_numberByline_16.png");
        buttonNumberingByLine.SetLargeImage("/NGraph;component/Resources/Icons/NG_numberByline_32.png");
        buttonNumberingByLine.LongDescription = "Последовательность действий:\n1. Выбрать элементы рамкой\n2. Подтвердить действие нажав: <Готово>\n3. Указать отрезок - начало линии";


       
        #endregion
        #region Структурные схемы по пространствам и параметрам"
        var panelStruct = Application.CreateRibbonPanel(nGraph, "Структурные схемы по пространствам и параметрам");
        
        var buttonCreateSxemaByModel = panelStruct.AddPushButton<CreateSxemaByModel>("Менеджер\nсхем");
        buttonCreateSxemaByModel.SetImage("/NGraph;component/Resources/Icons/NG_structura_16.png");
        buttonCreateSxemaByModel.SetLargeImage("/NGraph;component/Resources/Icons/NG_structura_32.png");
        
        var buttonCreateConnection = panelStruct.AddPushButton<CreateСonnectionsLineOnViewDrafting>("Расставить\nкабель");
        buttonCreateConnection.SetImage("/NGraph;component/Resources/Icons/NG_cabel_16.png");
        buttonCreateConnection.SetLargeImage("/NGraph;component/Resources/Icons/NG_cabel_32.png");
        
        var buttonCutLines = panelStruct.AddPushButton<CutLineOnViewDrafting>("Вырезать\nлинии");
        buttonCutLines.SetImage("/NGraph;component/Resources/Icons/NG_cut_16.png");
        buttonCutLines.SetLargeImage("/NGraph;component/Resources/Icons/NG_cut_32.png");
        
        var buttonCopyLinesToView = panelStruct.AddPushButton<CopyLinesToView>("Копировать\nкабель");
        buttonCopyLinesToView.SetImage("/NGraph;component/Resources/Icons/NG_copyCabel_16.png");
        buttonCopyLinesToView.SetLargeImage("/NGraph;component/Resources/Icons/NG_copyCabel_32.png");
        
        
        var buttonSelectInModelByElementId = panelStruct.AddPushButton<SelectInModelByElementId>("Найти в\nмодели");
        buttonSelectInModelByElementId.SetImage("/NGraph;component/Resources/Icons/NG_find_16.png");
        buttonSelectInModelByElementId.SetLargeImage("/NGraph;component/Resources/Icons/NG_find_32.png");
        
       
        
        #endregion
#endif
        #region  ФСА
        var pdnelFsa = Application.CreateRibbonPanel(nGraph, "Схемы автоматизации");
        var buttonCreateFsa = pdnelFsa.AddPushButton<AlgoritmCreateFootor>("ФСА\nпо группам");
        buttonCreateFsa.SetImage("/NGraph;component/Resources/Icons/NG_FSA_16.png");
        buttonCreateFsa.SetLargeImage("/NGraph;component/Resources/Icons/NG_FSA_32.png");
        buttonCreateFsa.LongDescription = "Построение сигнальных линий\nдля шкафа автоматизации\nпо технологическому процессу";
        
        var buttonCreateFsaWithEq = pdnelFsa.AddPushButton<AlgoritmCreateFootorWithEquipment>("ФСА по\nоборудованию");
        buttonCreateFsaWithEq.SetImage("/NGraph;component/Resources/Icons/NG_FSA_eq_16.png");
        buttonCreateFsaWithEq.SetLargeImage("/NGraph;component/Resources/Icons/NG_FSA_eq_32.png");
        buttonCreateFsaWithEq.LongDescription = "Построение сигнальных линий\nдля шкафа автоматизации\nна схеме должно быть оборудование";
        
        var buttonCreateFsaFromBd = pdnelFsa.AddPushButton<AlgoritmCreateFootorFromBd>("База\nданных");
        buttonCreateFsaFromBd.SetImage("/NGraph;component/Resources/Icons/NG_FSA_BD_16.png");
        buttonCreateFsaFromBd.SetLargeImage("/NGraph;component/Resources/Icons/NG_FSA_BD_32.png");
        buttonCreateFsaFromBd.LongDescription = "Выбор заготовки\nдля технологического процесса\nили элемента функциональной схемы";
        
        var buttonCreateFsaFromBDelement = pdnelFsa.AddPushButton<AlgoritmAddOnViewElementsFromBd>("База\nэлементов");
        buttonCreateFsaFromBDelement.SetImage("/NGraph;component/Resources/Icons/NG_BD_element_16.png");
        buttonCreateFsaFromBDelement.SetLargeImage("/NGraph;component/Resources/Icons/NG_BD_element_32.png");
        buttonCreateFsaFromBDelement.LongDescription = "Выбор элемента для функциональной схемы";

       
        #endregion
        
#if REVIT2019_OR_GREATER 
        #region  Схемы по воздуховодам
        var panelDuctSystems = Application.CreateRibbonPanel(nGraph, "Схемы по воздуховодам и цепям");
        var buttomCreateStructShemaByMechanicalDuctSystemsAndCircuit =
            panelDuctSystems.AddPushButton<CreateStructShemaByMechanicalDuctSystemsAndCircuit>("Построить\nсхему");
        buttomCreateStructShemaByMechanicalDuctSystemsAndCircuit.SetImage("/NGraph;component/Resources/Icons/NG_duct_createShema_16.png");
        buttomCreateStructShemaByMechanicalDuctSystemsAndCircuit.SetLargeImage("/NGraph;component/Resources/Icons/NG_duct_createShema_32.png");
        buttomCreateStructShemaByMechanicalDuctSystemsAndCircuit.LongDescription = "Создание структурной схемы\nпо модели электрооборудования,\nсвязанного системой воздузоводов и цепями";
            
        var buttomDuctSystemDestroy = panelDuctSystems
            .AddPushButton<DuctSystemDestroy>("Сломать\nсистему");
        buttomDuctSystemDestroy.SetImage("/NGraph;component/Resources/Icons/NG_duct_MSdestroy_16.png");
        buttomDuctSystemDestroy.SetLargeImage("/NGraph;component/Resources/Icons/NG_duct_MSdestroy_32.png");
        buttomDuctSystemDestroy.LongDescription = "Сломать систему воздуховодов\nдля удобства работы\nс цепями";
            
        var buttomDuctSystemConnect = panelDuctSystems
            .AddPushButton<DuctSystemConnect>("Собрать\nсистему");
        buttomDuctSystemConnect.SetImage("/NGraph;component/Resources/Icons/NG_duct_MScombine_16.png");
        buttomDuctSystemConnect.SetLargeImage("/NGraph;component/Resources/Icons/NG_duct_MScombine_32.png");
        buttomDuctSystemConnect.LongDescription = "Собрать систему воздуховодов\nдля дальнейшего построения\nструктурной схемы";
            

        #endregion
        
#endif   
    
    }
}