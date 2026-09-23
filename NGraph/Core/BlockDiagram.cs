using Autodesk.Revit.UI;
namespace NGraph.Core;

 /// <summary>
 /// Символ на структурной схеме в цепи. (rev2)
 /// </summary>
 public class BlockDiagram
 {
     ViewDrafting viewDrafting { get; }

     public List<GroupSymbol> GroupSymbols { get; } = new List<GroupSymbol>();
    // public IEnumerator GetEnumerator() => new GroupSymbolEnumerator(GroupSymbols);

     public XYZ Begin { get; }
     public XYZ End { get; }

     public BlockDiagram (Document doc, ViewDrafting viewDrafting, XYZ beginPointOfMethod, TypeOfLevel Level, List<CircuitId> circuitIds, ref int indexBlockDiagram)
     {
         Begin = beginPointOfMethod;
         End = beginPointOfMethod;


         this.viewDrafting = viewDrafting;
         Helpers helpers = new Helpers();
         //XYZ deltaX = new XYZ(helpers.MillimetersToFeet(60), 0, 0);
         //XYZ deltaY = new XYZ(0, helpers.MillimetersToFeet(60), 0);

         XYZ deltaX = new XYZ(helpers.MillimetersToFeet(6), 0, 0);
         XYZ deltaY = new XYZ(0, helpers.MillimetersToFeet(6), 0);

         var selectElSysLevel = circuitIds.Where(y => y.Equipment.Level.Equals(Level)).GroupBy(y => y.Equipment.Имя_панели); //Не последовательно ЦЕПЯМ!!!!

         int indexBlockDiagramBaseEqupment = indexBlockDiagram;// Действеие определяет сквозную нумерацию
         
         foreach (var selectedCircuitIds in selectElSysLevel)
         {
             
             BlockDiagramBaseEqupment blockDiagramOfBaseEqupment =
                 new BlockDiagramBaseEqupment(doc, viewDrafting,  selectedCircuitIds.First().Equipment, beginPointOfMethod, ref indexBlockDiagramBaseEqupment);
             
             //т.к. экземпляр создан, то можно обратиться к его реальной геометрии и перезаписать приращение конечной точки
             blockDiagramOfBaseEqupment.XYZ_end = GroupSymbol.ReloadEnd(blockDiagramOfBaseEqupment.BaseGroupSymbol.Symbol_ID.FamilyInstance, viewDrafting);

             GroupSymbols.Add(blockDiagramOfBaseEqupment.BaseGroupSymbol);

             //============>
             FormatGrowing formatGrowing = blockDiagramOfBaseEqupment.EQ.FormatGrowing;//Определяем направление роста цепей относительно базового элемента
             

             //============>
             int indexBlockDiagramCircuitId = 1;
             XYZ startXYZforCircuitId = blockDiagramOfBaseEqupment.XYZ_end;
             foreach (CircuitId cid in selectedCircuitIds)
             {
                 BlockDiagramCircuitId blockDiagramCircuitId = new BlockDiagramCircuitId(doc,blockDiagramOfBaseEqupment, cid, startXYZforCircuitId, ref indexBlockDiagramCircuitId);
                 GroupSymbols.AddRange(blockDiagramCircuitId.GroupSymbols);
                 if (formatGrowing == FormatGrowing.CircuitId_UP) startXYZforCircuitId = startXYZforCircuitId + new XYZ(0, blockDiagramCircuitId.deltaXYZ.Y, 0)+deltaY;
                 else startXYZforCircuitId = startXYZforCircuitId + new XYZ(blockDiagramCircuitId.deltaXYZ.X, 0, 0)+deltaX;
                 //Добавляем цепь в базовый блок
                 blockDiagramOfBaseEqupment.BlockDiagramCircuitIds.Add(blockDiagramCircuitId);
                 //Обрисовываем каждую цепь /_/
                 //CreateLineBox(doc, blockDiagramCircuitId.XYZ_begin, blockDiagramCircuitId.XYZ_end);

             }

             //Определяем реальные координаты элементов
             SetRealXYZ_BlockDiagramBaseEqupment(blockDiagramOfBaseEqupment, viewDrafting, deltaX+deltaY);
             
             //Обрисовываем каждый BlockDiagramBaseEqupment /_/
             CreateLineBox(doc, blockDiagramOfBaseEqupment.XYZ_begin, blockDiagramOfBaseEqupment.XYZ_end);

             End = blockDiagramOfBaseEqupment.XYZ_end;

             //Рисуем линии для цепей и к базовому элементу
             CreateLineInBlockDiagramCircuitIds(doc,blockDiagramOfBaseEqupment);


             
             //Переходим на другую координату BaseEqupment, точнее перезаписываем точку входа в метод (beginPointOfMethod) в зависимости от условий FormatSxema
             
             switch (selectedCircuitIds.First().Equipment.formatSxema) //Определяем как расположены ЦЕПИ относительно базового элемента
             {
                 //Растем по X
                 case FormatSxema.HorizontalFromLeftToRight: { beginPointOfMethod = beginPointOfMethod+ new XYZ(blockDiagramOfBaseEqupment.deltaXYZ.X, 0, 0)+deltaX; break; }
                 // Растем по Y
                 case FormatSxema.VerticalFromDownToUp: { beginPointOfMethod = beginPointOfMethod+ new XYZ(0, blockDiagramOfBaseEqupment.deltaXYZ.Y, 0)+deltaY; break; }
             }
             
         }

         indexBlockDiagram = indexBlockDiagramBaseEqupment; // Действеие определяет сквозную нумерацию

        


     }



     









     void SetIndex_Marka_ID(Document doc, BlockDiagramBaseEqupment blockDiagramBaseEqupment)
     {

         
        





     }

     //Размещаем линии 
     /// <summary>
     /// Размещение линий в BlockDiagramBaseEqupment
     /// ВАЖНО!!! все марки должны быть или влево или вниз, т.к. расчет идет по минимальной их геометрической точке
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="blockDiagramBaseEqupment"></param>
     void CreateLineInBlockDiagramCircuitIds(Document doc, BlockDiagramBaseEqupment blockDiagramBaseEqupment)
     {
         //Точка, от которой чертятся все цепи (локация базового элемента)
         XYZ PoinBaseEqupment = blockDiagramBaseEqupment.BaseGroupSymbol.Symbol_ID.FamilyInstance.GetPlacementPoint();
         List <XYZ> EndPoints =  new List <XYZ>(); //Накапливаем список точек от которых будут строитcя линии до PoinBaseEqupment
         using (Transaction tr = new Transaction(doc, $"Рисуем линии"))
         {
             tr.Start();
             try
             {
                 foreach (BlockDiagramCircuitId blockDiagramCircuitId in blockDiagramBaseEqupment.BlockDiagramCircuitIds)
                 {
                     //Точка последнего элемента
                     if (blockDiagramCircuitId.GroupSymbols.Count == 0) continue;
                     XYZ PointLastEqupment = blockDiagramCircuitId.GroupSymbols.Last().Symbol_ID.FamilyInstance.GetPlacementPoint();
                     XYZ PointFirstOfMarka_ID = blockDiagramCircuitId.GroupSymbols.First().Marka_ID.FamilyInstance.get_BoundingBox(viewDrafting).Min;
                     XYZ PointLastOfMarka_ID = blockDiagramCircuitId.GroupSymbols.Last().Marka_ID.FamilyInstance.get_BoundingBox(viewDrafting).Min;


                     if (blockDiagramCircuitId.CircuitId.NSA_Цепь_звезда) //Если цепь звезда
                     {
                         
                         foreach(var groupSymbol in blockDiagramCircuitId.GroupSymbols) //Строим линии между символом и минимальной точкой марки
                         {
                             XYZ PointEqupment = groupSymbol.Symbol_ID.FamilyInstance.GetPlacementPoint();
                             XYZ PointOfMarka_ID = groupSymbol.Marka_ID.FamilyInstance.get_BoundingBox(viewDrafting).Min;
                             
                             CreateLine(doc, PointEqupment, PointOfMarka_ID);

                         }
                         //Соединяем конечные точки марки линией
                         CreateLine(doc, PointFirstOfMarka_ID, PointLastOfMarka_ID);
                         //Добавляем в список точку первой марки.
                         EndPoints.Add(PointFirstOfMarka_ID);

                     }
                     else
                     {
                         CreateLine(doc, PointLastEqupment, PointFirstOfMarka_ID);
                         EndPoints.Add(PointFirstOfMarka_ID);
                     }
                 }

                 //Достраиваем прямые.
                 
                 List<double> lDoubleX = new List<double>(); //Список всех координат X (прямые стрямятся по к базовому элементу через ось X
                 lDoubleX.Add(PoinBaseEqupment.X);
                 foreach (XYZ point in EndPoints)
                 {
                     CreateLine(doc, new XYZ(point.X, PoinBaseEqupment.Y,0), point);
                     lDoubleX.Add(point.X);
                 }
                 CreateLine(doc, new XYZ(lDoubleX.Min(), PoinBaseEqupment.Y, 0), new XYZ(lDoubleX.Max(), PoinBaseEqupment.Y, 0));



             }
             catch { TaskDialog.Show("Ошибка", "Ошибка в методе CreateLineInBlockDiagramCircuitIds "); }
             tr.Commit();
         }



     }






     /// <summary>
     /// Создание красной линии
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="XYZ_begin"></param>
     /// <param name="XYZ_end"></param>
     void CreateLine(Document doc, XYZ XYZ_begin, XYZ XYZ_end)
     {
         
         GraphicsStyle _gstyle = CreateLineStyle(doc, "blue_3_сплошная", 3, new Autodesk.Revit.DB.Color(10, 10, 250), TypeOfLine.Сплошная);
         XYZ P1 = XYZ_begin;
         XYZ P2 = XYZ_end;
         Line line1 = Line.CreateBound(P1, P2);
         DetailCurve dc1 = doc.Create.NewDetailCurve(viewDrafting, line1);
         dc1.LineStyle = _gstyle;
             
     }





     public enum TypeOfLine //Для определения базового элемента
     {
         Штрих, Сплошная

     }




     /// <summary>
     /// Создание прямоугольника
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="XYZ_begin"></param>
     /// <param name="XYZ_end"></param>
     void CreateLineBox(Document doc, XYZ XYZ_begin, XYZ XYZ_end)
     {
         using (Transaction tr = new Transaction(doc, $"Сегмент"))
         {
             tr.Start();
             try
             {
                 GraphicsStyle _gstyle = CreateLineStyle(doc, "grey_1_штрих", 1, new Autodesk.Revit.DB.Color(128, 128, 128), TypeOfLine.Штрих);
                 XYZ P1 = XYZ_begin;
                 XYZ P2 = new XYZ(XYZ_begin.X, XYZ_end.Y, 0);
                 XYZ P3 = XYZ_end;
                 XYZ P4 = new XYZ(XYZ_end.X, XYZ_begin.Y, 0);

                 Line line1 = Line.CreateBound(P1, P2);
                 Line line2 = Line.CreateBound(P2, P3);
                 Line line3 = Line.CreateBound(P3, P4);
                 Line line4 = Line.CreateBound(P4, P1);
                 DetailCurve dc1 = doc.Create.NewDetailCurve(viewDrafting, line1);
                 DetailCurve dc2 = doc.Create.NewDetailCurve(viewDrafting, line2);
                 DetailCurve dc3 = doc.Create.NewDetailCurve(viewDrafting, line3);
                 DetailCurve dc4 = doc.Create.NewDetailCurve(viewDrafting, line4);

                 dc1.LineStyle = _gstyle;
                 dc2.LineStyle = _gstyle;
                 dc3.LineStyle = _gstyle;
                 dc4.LineStyle = _gstyle;


             }
             catch { TaskDialog.Show("Ошибка", "Ошибка в методе CreateLineBox "); }
             tr.Commit();
         }
     }








     /// <summary>
     /// Создание стиля линии, если такого не существует.
     /// </summary>
     /// <param name="doc"></param>
     /// <param name="name"></param>
     /// <param name="weight"></param>
     /// <param name="color"></param>
     /// <param name="patternLineNAME"></param>
     /// <returns></returns>
     GraphicsStyle CreateLineStyle(Document doc, string name, int weight, Autodesk.Revit.DB.Color color, TypeOfLine patternLineNAME)
     {
         Categories categories = doc.Settings.Categories;
         Category lineCategories = categories.get_Item(BuiltInCategory.OST_Lines);
         CategoryNameMap lineStyleSubTypes = lineCategories.SubCategories;

         //Проверяем существуют ли линии и если нет, то создаем их
         if (lineStyleSubTypes.Contains(name))
             return lineStyleSubTypes.get_Item(name).GetGraphicsStyle(GraphicsStyleType.Projection);
         else
         {
             Category cat = categories.NewSubcategory(lineCategories, name);
             cat.SetLineWeight(weight, GraphicsStyleType.Projection);
             cat.LineColor = color;
             if (patternLineNAME == TypeOfLine.Штрих)
             {
                 var patternLine = LinePatternElement.GetLinePatternElementByName(doc, patternLineNAME.ToString()).Id;
                 cat.SetLinePatternId(patternLine, GraphicsStyleType.Projection);

             }




             return cat.GetGraphicsStyle(GraphicsStyleType.Projection);
         }
     }




     /// <summary>
     /// Записываем реальные координаты габаритов групп (после транзакции и создания экземпляра семейства).
     /// И определяем реальные координаты всей BlockDiagramBaseEqupment [begin ,end]
     /// delta - отступ габаритов с рамкой
     /// </summary>
     /// <param name="blockDiagramOfBaseEqupment"></param>
     /// <param name="view"></param>
     /// <param name="delta"></param>

     /// <returns></returns>
     void SetRealXYZ_BlockDiagramBaseEqupment(BlockDiagramBaseEqupment blockDiagramOfBaseEqupment, View view, XYZ delta)
     {
         XYZ BaseSymbolMax = blockDiagramOfBaseEqupment.BaseGroupSymbol.Symbol_ID.FamilyInstance.get_BoundingBox(view).Max;
         XYZ BaseSymbolMin = blockDiagramOfBaseEqupment.BaseGroupSymbol.Symbol_ID.FamilyInstance.get_BoundingBox(view).Min;
         List<double> com_arrayX = new List<double>();
         List<double> com_arrayY = new List<double>();
         com_arrayX.Add(BaseSymbolMax.X); com_arrayX.Add(BaseSymbolMin.X);
         com_arrayY.Add(BaseSymbolMax.Y); com_arrayY.Add(BaseSymbolMin.Y);

         
         if ( blockDiagramOfBaseEqupment.BaseGroupSymbol.Marka_ID.FamilyInstance != null)
         {
             com_arrayX.Add(blockDiagramOfBaseEqupment.BaseGroupSymbol.Marka_ID.FamilyInstance.get_BoundingBox(view).Max.X);
             com_arrayX.Add(blockDiagramOfBaseEqupment.BaseGroupSymbol.Marka_ID.FamilyInstance.get_BoundingBox(view).Min.X);
             com_arrayY.Add(blockDiagramOfBaseEqupment.BaseGroupSymbol.Marka_ID.FamilyInstance.get_BoundingBox(view).Max.Y);
             com_arrayY.Add(blockDiagramOfBaseEqupment.BaseGroupSymbol.Marka_ID.FamilyInstance.get_BoundingBox(view).Min.Y);
         }
         
         foreach (BlockDiagramCircuitId blockDiagramCircuitId in blockDiagramOfBaseEqupment.BlockDiagramCircuitIds)
         {
             XYZ sMin = blockDiagramCircuitId.XYZ_begin;
             XYZ sMax = blockDiagramCircuitId.XYZ_end;
             com_arrayX.Add(sMax.X); com_arrayX.Add(sMin.X);
             com_arrayY.Add(sMax.Y); com_arrayY.Add(sMin.Y);
         }
         blockDiagramOfBaseEqupment.XYZ_end = new XYZ(com_arrayX.Max(), com_arrayY.Max(), 0)+ delta;
         blockDiagramOfBaseEqupment.XYZ_begin = new XYZ(com_arrayX.Min(), com_arrayY.Min(), 0);
         blockDiagramOfBaseEqupment.deltaXYZ = blockDiagramOfBaseEqupment.XYZ_end - blockDiagramOfBaseEqupment.XYZ_begin;
         //XYZ[] d = [new XYZ(com_arrayX.Min(), com_arrayY.Min(), 0), new XYZ(com_arrayX.Max(), com_arrayY.Max(), 0)];

         //return d;
     }


    





 }

    