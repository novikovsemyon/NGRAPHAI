using ClosedXML.Excel;
using HOVS.Model;
using HOVS.Plugin;
var root = Path.Combine(Path.GetTempPath(), "NGraph-hovs-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root); HovsEnvironment.Root = root;
void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
try
{
    var source = Path.Combine(root,"test.xlsx");
    using (var workbook = new XLWorkbook())
    {
        workbook.AddWorksheet("Титул").Cell(1,1).Value = "Титульный лист";
        var ws = workbook.AddWorksheet("ХОВС");
        string[] headers = { "Обозначение", "L, м3/ч", "L расчётный, м3/ч", "Помещение", "Фильтр" };
        for (int i=0;i<headers.Length;i++) ws.Cell(1,i+1).Value = headers[i];
        ws.Cell(2,1).Value="П1";ws.Cell(2,2).Value=1200;ws.Cell(2,4).Value="101";ws.Cell(2,5).Value="G4";
        ws.Cell(3,1).Value="В1";ws.Cell(3,3).Value=900;ws.Cell(3,4).Value="101";
        ws.Cell(4,1).Value="П2";ws.Cell(4,2).Value=0;
        workbook.SaveAs(source);
    }
    var schema = new HovsSchema { WorksheetName="ХОВС", HeaderRow=1, LastHeaderRow=1, FirstDataRow=2 };
    schema.Columns.Add(new ColumnSemantic {ColumnNumber=1,Kind=ColumnSemanticKind.InstallationName});
    schema.Columns.Add(new ColumnSemantic {ColumnNumber=2,Kind=ColumnSemanticKind.AirFlow,Header="L, м3/ч"});
    schema.Columns.Add(new ColumnSemantic {ColumnNumber=3,Kind=ColumnSemanticKind.AirFlow,Header="L расчётный, м3/ч"});
    schema.Columns.Add(new ColumnSemantic {ColumnNumber=4,Kind=ColumnSemanticKind.Room});
    schema.Columns.Add(new ColumnSemantic {ColumnNumber=5,Kind=ColumnSemanticKind.Filter,Slot=1});
    var model = new ExcelImporter().Load(source,(_,_)=>schema);
    Check(model.Equipment.Any(x=>x.Id=="П1") && model.Equipment.Any(x=>x.Id=="В1"),"Positive and fallback airflow must import");
    Check(model.Equipment.All(x=>x.Id!="П2" || EquipmentFeatureAnalyzer.AnalyzeDetailed(x).InstallationConfidence < .1),"Zero airflow cannot confirm installation");
    var p = model.Equipment.First(x=>x.Id=="П1");
    TrainingStore.SaveExplicitCorrection(p,new InstallationFeatures {InstallationType="ПВУ"});
    Check(EquipmentFeatureAnalyzer.Analyze(p).InstallationType=="ПВУ","Explicit learning must affect next analysis");
    var noFlow=new Equipment("П3","П3","П","",new Dictionary<string,string>());
    TrainingStore.SaveExplicitCorrection(noFlow,new InstallationFeatures {InstallationType="П"});
    Check(EquipmentFeatureAnalyzer.AnalyzeDetailed(noFlow).InstallationConfidence < .1,"Training cannot bypass airflow rule");
    Equipment Candidate(string id, string type, string room, bool flow) => new Equipment(id,id,type,room,
        flow ? new Dictionary<string,string>{{"__Installation.Airflow","100"}} : new Dictionary<string,string>());
    var supply = Candidate("П10","П","Комната",true);
    var exhaust = Candidate("В20","В","Комната",true);
    Check(ConnectionRules.Find(new[]{supply,exhaust},Array.Empty<EquipmentComponent>()).Any(x=>x.Kind==RelationKind.SameRoom),"Same-room candidate");
    Check(ConnectionRules.Find(new[]{Candidate("П10","П","Комната",false),exhaust},Array.Empty<EquipmentComponent>()).Count==0,"No-L excluded from links");
    Check(ConnectionRules.Find(new[]{Candidate("ПД10","Другая","Комната",true),exhaust},Array.Empty<EquipmentComponent>()).Count==0,"Smoke control excluded from links");
    ProjectDataOverrides.Save(supply,new InstallationFeatures {InstallationType="П",Recirculation="Да"},true);
    Check(ConnectionRules.Find(new[]{supply,exhaust},Array.Empty<EquipmentComponent>()).Any(x=>x.Kind==RelationKind.Recirculation),"Edited recirculation affects inference");
    var repo = new HovsRepository(root); var project=repo.Create("Объект & тест");
    var revision=repo.Save(project,model,source,"Первый импорт");
    ProjectDataOverrides.Save(p, new InstallationFeatures {InstallationType="ПВУ",HeatExchangers="Нагрев: Водяной ×1; Охлаждение: DX ×1"},false);
    repo.Save(project,model,source,"Исправления");
    Check(repo.Revisions(project).Count==2,"Revision history must be preserved");
    Check(!repo.Load(revision).Equipment.First(x=>x.Id=="П1").Attributes.ContainsKey(ProjectDataOverrides.Prefix+"Selected"),"Older revision must remain unchanged");
    repo.Rename(project, "Переименованный & объект");
    Check(repo.Projects().Single(x => x.Id == project.Id).Name == project.Name && repo.Revisions(project).Count == 2, "Rename preserves identity and revisions");
    var foreign = repo.Create("Другой объект");
    bool rejected = false;
    try { repo.DeleteRevision(foreign, revision); } catch (ArgumentException) { rejected = true; }
    Check(rejected && File.Exists(revision.SourcePath), "Cannot delete another object's revision");
    rejected = false;
    try { repo.DeleteProject(new HovsProject { Id = ".." }); } catch (ArgumentException) { rejected = true; }
    Check(rejected && Directory.Exists(root), "Cannot delete outside repository");
    var outside = new HovsRevision { Id = revision.Id, DirectoryPath = Path.Combine(root, "outside", project.Id, revision.Id) };
    rejected = false;
    try { repo.Load(outside); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "Cannot load a forged revision path");
    var disposable = repo.Save(foreign, model, source, "Удаляемая");
    repo.DeleteRevision(foreign, disposable);
    Check(repo.Revisions(foreign).Count == 0 && repo.Revisions(project).Count == 2, "Revision deletion is isolated");
    repo.DeleteProject(foreign);
    Check(repo.Projects().Count == 1 && repo.Revisions(project).Count == 2, "Project deletion is isolated");
    Check(File.Exists(Path.Combine(root, "training_corrections_v2.tsv")), "Deletion preserves shared training");

    HovsModel Snapshot(string flow, string room, bool extra = false, bool missing = false, string filter = "G4", bool links = false)
    {
        var items = new List<Equipment>();
        if (!missing) items.Add(new Equipment("П1", "П1", "П", room, new Dictionary<string,string> { ["__Installation.Airflow"] = flow, [ProjectDataOverrides.Prefix + "FilterType"] = filter }));
        if (extra) items.Add(new Equipment("В2", "В2", "В", room, new Dictionary<string,string>()));
        return new HovsModel(items, Array.Empty<EquipmentComponent>(), links ? new[] { new Relation("П1", "В2", RelationKind.SameRoom, .7, "test") } : Array.Empty<Relation>());
    }
    var first = new RevisionSnapshot("R1", Snapshot("1 200,0", "101"));
    var same = new RevisionSnapshot("R2", Snapshot("1200", " 101 "));
    Check(RevisionComparison.Compare(new[]{first,same}).Single().Status == "Без изменений", "Numeric airflow and whitespace normalization");
    var changed = new RevisionSnapshot("R2", Snapshot("1500", "102", true, filter:"F7", links:true));
    var diff = RevisionComparison.Compare(new[]{first,changed});
    var item = diff.Single(x=>x.Designation == "П1");
    Check(item.Status == "Изменена" && item.Details.Contains("101 → 102") && item.Details.Contains("G4 → F7") && item.Details.Contains("Связи:"), "Concrete room, saved override and relation differences");
    Check(diff.Single(x=>x.Designation == "В2").Status == "Добавлена", "Added installation");
    Check(RevisionComparison.Compare(new[]{changed,first}).Single(x=>x.Designation=="В2").Status == "Удалена", "Removed installation");
    var history = RevisionComparison.Compare(new[]{first,changed,new RevisionSnapshot("R3",first.Model)});
    Check(history.Single(x=>x.Designation == "П1").Details.Contains("R1 → R2") && history.Single(x=>x.Designation == "П1").Details.Contains("R2 → R3"), "Intermediate changes must not disappear when endpoints match");
    var absent = new RevisionSnapshot("R2", Snapshot("", "", missing:true));
    Check(RevisionComparison.Compare(new[]{first,absent,same}).Single().Status == "Изменена", "Intermediate absence is a change");
    var stable = RevisionComparison.Compare(new[]{first,changed}).Single(x=>x.Designation=="П1").Details;
    TrainingStore.SaveExplicitCorrection(first.Model.Equipment[0], new InstallationFeatures { FilterType = "F9" });
    Check(RevisionComparison.Compare(new[]{first,changed}).Single(x=>x.Designation=="П1").Details == stable, "New training must not rewrite revision comparison");
    File.Delete(source);
    Check(File.Exists(revision.SourcePath) && repo.Load(revision).Equipment.Count==model.Equipment.Count,"Saved revision must not depend on original XLSX path");
    Console.WriteLine("PASS: multi-sheet XLSX, positive/fallback/zero L, learning, revisions, independent source snapshot, rename/delete isolation, multi-revision comparison.");
}
finally {Directory.Delete(root,true);}
