using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using NGraph.Core;
using Nice3point.Revit.Extensions.Runtime;
using Nice3point.Revit.Toolkit.External;



namespace NGraph.Commands;

/// <summary>
///     Info for plugin NGraph
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AlgoritmCreateSpecification : ExternalCommand
{


    public override void Execute()
    {
        // Спецификация формируется по активному виду; отдельная база оборудования не нужна.
        var greenPoints = Application.ActiveUIDocument.Document.CollectElements(Application.ActiveUIDocument.ActiveView.Id).ToList().Where(x => x.GetType() == typeof(FamilyInstance))
            .ToList()
            .Where(i => NgContext._FindParameter(i, "Тип")!.AsValueString().Contains("FAS_точка")).ToList();

        var greenPointsGrouped =
            greenPoints.GroupBy(i => i.LookupParameter("NS_ElementId").AsString(),
                i => i.LookupParameter("Комментарии").AsString());

        var spec = new Specification();
        foreach (var e in greenPointsGrouped)
        {
            var gr = e.GroupBy(i => i);
            if (e.Key.IsNullOrWhiteSpace()) continue;
            var elementSpecification = new ElementSpecification(Application.ActiveUIDocument.Document, e.Key)
            {
                Количество = gr.Count()
            };
            spec.ElementSpecifications.Add(elementSpecification);

        }

        spec.ElementSpecifications.Count();


        using var tr = new Transaction(Application.ActiveUIDocument.Document, $"Размещение спецификации");
        tr.Start();
        try
        {
            XYZ xyz = new XYZ(280/304.8,-120/304.8,0);
            XYZ step = new XYZ();
            foreach (var sp in spec.ElementSpecifications) //Точки и марки
            {
                
                FamilyInstance fi = Application.ActiveUIDocument.Document.Create.NewFamilyInstance(xyz+step, sp.Fs, Application.ActiveUIDocument.ActiveView);

                fi.LookupParameter("_Установка").Set(sp.Установка);
                fi.LookupParameter("ADSK_Наименование").Set(sp.Наименование);
                fi.LookupParameter("ADSK_Наименование краткое").Set(sp.НаименованиеКраткое);
                fi.LookupParameter("ADSK_Количество").Set(sp.Количество);
                fi.LookupParameter("ADSK_Позиция").Set(sp.Позиция);

                step = step + new XYZ(0, -10/304.8, 0);


            }
        }
        catch
        {
            // ignored
        }

        tr.Commit();




    }

    public class Specification
    {

        public List<ElementSpecification> ElementSpecifications { get; set; } = [];

        public Specification()
        {

        }
    }

    public class ElementSpecification
    {
        public FamilySymbol? Fs { get; }
        private Element El { get; }
        public string? Позиция { get; }
        public string? Наименование { get; }
        public string? НаименованиеКраткое { get; }
        public string? Артикул { get; }
        public string? ЕИ { get; }
        public string? Установка { get; }
        public int Количество { get; set; }
        //string Примечание{ get; set; } 


        public ElementSpecification(Document doc, string id)
        {
            El = doc.GetElement(RevitElementAccess.CreateId(long.Parse(id)));
            Fs = (El as AnnotationSymbol)?.Symbol;
            Наименование = NgContext._FindParameter(El, "ADSK_Наименование")?.AsString();
            НаименованиеКраткое = NgContext._FindParameter(El, "ADSK_Наименование краткое")?.AsString();
            Позиция = NgContext._FindParameter(El, "ADSK_Позиция")?.AsString();
            Установка = NgContext._FindParameter(El, "_Установка")?.AsString();


        }
    }
}


