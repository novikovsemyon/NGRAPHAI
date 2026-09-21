using Autodesk.Revit.UI;
namespace NGraph.Core;

  /// <summary>
  /// Структурная схема от базового элемента (создается один базовый элемент с маркой)
  /// </summary>
  public class BlockDiagramBaseEqupment : Gabarit
  {
      public Document Doc { get; }
      public View ViewDrafting { get; }
      public EQ EQ {  get;}
      public GroupSymbol BaseGroupSymbol { get;}
      public List<BlockDiagramCircuitId> BlockDiagramCircuitIds { get; set; } = new List<BlockDiagramCircuitId>();   

      public int indexBlockDiagramBaseEqupment { get; set; }
      public BlockDiagramBaseEqupment(Document doc, View viewDrafting,  EQ EQ, XYZ XYZ_begin, ref int indexBlockDiagramBaseEqupment)
      {
          this.Doc = doc;
          this.ViewDrafting = viewDrafting;
          this.XYZ_begin = XYZ_begin;
          this.EQ = EQ;
          this.indexBlockDiagramBaseEqupment = indexBlockDiagramBaseEqupment;
          indexBlockDiagramBaseEqupment++;

          //Размещение марки для базового элемента
          int indexGroupSymbol = 1;
          if (EQ.formatSxema == FormatSxema.HorizontalFromLeftToRight)
          {
              this.BaseGroupSymbol = new GroupSymbol(new Symbol_ID(doc, viewDrafting, EQ), new Marka_ID(doc, viewDrafting, Orientation.Вертикально), ref indexGroupSymbol);
          }
          else 
          {
              this.BaseGroupSymbol = new GroupSymbol(new Symbol_ID(doc, viewDrafting, EQ), new Marka_ID(doc, viewDrafting, Orientation.Горизонтально), ref indexGroupSymbol);
          }
          indexGroupSymbol++;
          using (Transaction tr = new Transaction(doc, $"{EQ.Имя_панели}"))
          {
              tr.Start();
              try
              {
                  BaseGroupSymbol.Symbol_ID.FamilyInstance = BaseGroupSymbol.CreateFamilyInstanceSymbol_ID(doc, viewDrafting, this.XYZ_begin);
                  //BaseGroupSymbol.Marka_ID.FamilyInstance = BaseGroupSymbol.CreateFamilyInstanceMarka_ID(doc, viewDrafting, this.XYZ_begin);
                  BaseGroupSymbol.SetParametr(BaseGroupSymbol);
              }
              catch { TaskDialog.Show("Ошибка", "Ошибка в методе BlockDiagramBaseEqupment "); }
              tr.Commit();
          }



      }

      













  }
