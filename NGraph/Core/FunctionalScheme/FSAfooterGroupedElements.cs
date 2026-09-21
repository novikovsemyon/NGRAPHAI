namespace NGraph.Core.FunctionalScheme;

 /// <summary>
 /// Сгруппированные элементы футора по структуре
 /// </summary>
 public class FSAfooterGroupedElements
 {
     /// <summary>
     /// Элемент структурной схемы
     /// </summary>
     
     
     FSAheader Header { get; }
     /// <summary>
     /// Список элементов, соответствующий структурной схеме
     /// </summary>
     internal List<FSAfooterElement> Elements { get;} = new List<FSAfooterElement>();
     
     /// <summary>
     /// Футер, на котором расположена группа с элементами
     /// </summary>
     FSAfooter Footer { get; }

     /// <summary>
     /// По этому имени определяем, какой элемент на структурной схеме
     /// </summary>
     public string Name { get;}

     int indexGroup { get; set; } = 1;

     public FSAfooterGroupedElements(Document doc, ViewDrafting viewDrafting, FSAheader header, FSAfooter footer, ref int  index)
     {
         indexGroup = indexGroup++;
         // ViewDrafting activeView = doc.ActiveView as ViewDrafting;
         Header = header;
         Footer = footer;
         Name = Header.Name;

         
         foreach (DataDescription dd in DefineDataDescriptionMethodsByName(doc))
         {
             XYZ step = footer.StepBehindElementsInFooter;
             //Позиция экземпляра семейства
             XYZ xyz = footer.XYZ + new XYZ(0.1, 0, 0)+step*index;

             //Создаем экземпляр в футоре, каждый раз сдвигая на footer.StepBehindElementsInFooter;
             var fiElement = doc.Create.NewFamilyInstance(xyz,
               new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).First(q => q.Name == dd.NameTypeElement) as FamilySymbol,
               viewDrafting);

             FSAfooterElement fSAfooterElenent = new FSAfooterElement(fiElement, dd);

             //

             if (dd.NameParamHeader != Const.Param_NS_NoParametr & dd.NameTypeElement == Const.Element_Footor_First)
             {
                 //Создаем марку (Элемент узла) и пишем в ее комментарий номер от параметра элемента на структурной схеме
                 FamilyInstance fiMark = doc.Create.NewFamilyInstance
                     (xyz+ new XYZ(0, 152 / 304.8, 0),
                     new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).First(q => q.Name == Const.Element_CabelForFootor) as FamilySymbol, viewDrafting);

                 FamilyInstance fiHeader = (doc.GetElement(Header.ID) as FamilyInstance);
                 fiMark.LookupParameter(Const.Param_CJ_Number).Set(fiHeader.LookupParameter(dd.NameParamHeader).AsString());
                 //fiMark.LookupParameter(Const.Param_CJ_Number).Set(fiHeader.LookupParameter(dd.NameParamHeader).AsString() + "-" + Footer.Group);



                 //fiMark.LookupParameter("NS_ElementId").Set(FAS_fi.Id.ToString() + " " + FamilyInstanceFAS.Id.ToString());
                 //Первый айди - элемент структурной схемы
                 //Второй айди - элемент подвала FSA_CABEL_FIRST

                 //fiMark.LookupParameter("NS_Позиция").Set(iterator_fi + 1);


             }








             Elements.Add(fSAfooterElenent);


             index++;

         }

         FSAfooterElement.Set(doc, Elements);


     }



   




     /// <summary>
     /// Определяем как будет выглядеть элемент на футоре 
     /// <br/> Узнаем список DataDescription
     /// </summary>
     /// <param name="doc"></param>
     /// <returns></returns>
     List<DataDescription> DefineDataDescriptionMethodsByName(Document doc)
     {
         switch (Name)
         {
                                
             case Const.Element_Header_Users:
                 return DataDescription.FooterElementUser(doc, Header.ID, false);
             case Const.Element_Header_Users_NoTag:
                 return DataDescription.FooterElementUser(doc, Header.ID, true);




             case "Водяной нагреватель":
                 return DataDescription.HVAC_heater_Water();
             case "Вентилятор вытяжной":
                 return DataDescription.HVAC_Motor_Out();
             case "Вентилятор приточный":
                 return DataDescription.HVAC_Motor_IN();
             case "Фильтр воздушный":
                 return DataDescription.HVAC_Filter();
             case "Рекуператор пластинчатый":
                 return DataDescription.HVAC_PlateHeatRecovery();
             case "Рекуператор роторный":
                 return DataDescription.HVAC_RotaryHeatRecovery();





             default:
                 return DataDescription.FooterElementUserUnknown(doc, Header.ID);

         }


     }







     








 }
