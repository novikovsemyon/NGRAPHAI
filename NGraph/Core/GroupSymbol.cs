namespace NGraph.Core;

  /// <summary>
  /// Группа символа
  /// </summary>
  public class GroupSymbol: Gabarit
  {
      public Symbol_ID Symbol_ID { get;}
      public Marka_ID Marka_ID { get;}
      public string НомерКабеля { get; set;} = string.Empty;
      public int indexGroupSymbol {get;}


      public GroupSymbol(Symbol_ID Symbol_ID, Marka_ID Marka_ID, ref int indexGroupSymbol)
      {
          this.Symbol_ID = Symbol_ID;
          
          this.Marka_ID = Marka_ID;
          this.indexGroupSymbol = indexGroupSymbol;
          indexGroupSymbol++;
      }


      public static XYZ ReloadEnd(FamilyInstance familyInstance, View ViewDrafting)
      {
          XYZ max = familyInstance.get_BoundingBox(ViewDrafting).Max;
          return max;
      }

     
      


      public FamilyInstance CreateFamilyInstanceSymbol_ID(Document doc, View viewDrafting, XYZ XYZ)
      {
          FamilyInstance familyInstance = doc.Create.NewFamilyInstance(XYZ, Symbol_ID.FamilySymbol, viewDrafting);

          //XYZ_begin = familyInstance.get_BoundingBox(viewDrafting).Min;
          //XYZ_begin = familyInstance.get_BoundingBox(viewDrafting).Max;

          return familyInstance;
      }

      public FamilyInstance CreateFamilyInstanceMarka_ID(Document doc, View viewDrafting, XYZ XYZ)
      {
          FamilyInstance familyInstance = doc.Create.NewFamilyInstance(XYZ, Marka_ID.FamilySymbol, viewDrafting);

          //XYZ_begin = familyInstance.get_BoundingBox(viewDrafting).Min;
          //XYZ_begin = familyInstance.get_BoundingBox(viewDrafting).Max;

          return familyInstance;
      }


      /// <summary>
      /// Запись параметров Текст УГО, Имя панели, ElementId
      /// </summary>
      /// <param name="groupSymbol"></param>
      public void SetParametr(GroupSymbol groupSymbol)
      {
          foreach (Parameter p in groupSymbol.Symbol_ID.FamilyInstance.Parameters)
          {
              switch (p.Definition.Name)
              {
                  case "NS_Текст УГО": p.Set($"{groupSymbol.Symbol_ID.EQ.ADSK_Позиция}"); break;
                  case "NS_Имя панели": p.Set($"{groupSymbol.Symbol_ID.EQ.Имя_панели}"); break;
                  case "NS_ElementId": p.Set($"{groupSymbol.Symbol_ID.EQ.familyInstance.Id.ToString()}"); break;
              }
          }
      }



      








  }


