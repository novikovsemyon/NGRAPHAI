using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NGraph.Core.CableRouting;
using NGraph.ViewModels;
using NGraph.Views;

internal static class DuctCableTests
{
    public static void Run()
    {
        TestPaths(); TestDiagram(); TestStarGeometry(); TestWritesAndWindow(); TestPreferences();
        Console.WriteLine("PASS: duct cable shortest paths, fractional lengths, cycles, disconnected systems, stars, duplicate endpoints, write scopes, rounding, per-view persistence and real WPF dialog.");
    }

    private static void TestPaths()
    {
        var graph = new CableRouteGraph();
        foreach (var pair in new[] { ("A", 3000000001L), ("B", 2L), ("C", 3L), ("D", 4L), ("isolated", 5L) }) graph.AddPort(pair.Item1, pair.Item2);
        graph.Connect("A", "B", 0.123456, 101, "Трасса 1"); graph.Connect("B", "D", 0.456789, 102, "Трасса 1");
        graph.Connect("A", "C", 0.4, 103, "Трасса 1"); graph.Connect("C", "D", 0.4, 104, "Трасса 1");
        graph.Connect("B", "C", 2, 105, "Трасса 1");
        var path = graph.Find(3000000001L, 4);
        Check(Math.Abs(path.Meters-0.580245) < 1e-10 && path.DuctIds.SequenceEqual(new long[] { 101, 102 }), "Weighted shortest route with fractional meters and 64-bit IDs");
        Check(graph.Find(4, 3000000001L).DuctIds.SequenceEqual(new long[] { 102, 101 }), "Bidirectional route order");
        Expect(() => graph.Find(3000000001L, 5)); Expect(() => graph.Find(3000000001L, 3000000001L));
        graph.AddPort("second-system", 3000000001L); graph.AddPort("second-end", 6); graph.Connect("second-system", "second-end", 1, 106, "Трасса 2");
        Check(graph.Find(3000000001L, 6).Systems.Single() == "Трасса 2", "Endpoint can have ports in separate systems");
        Expect(() => graph.Find(4, 6)); // одинаковый владелец портов не склеивает разные системы
        graph.AddPort("zero-joint", 7); graph.Connect("B", "zero-joint", 0); graph.Connect("zero-joint", "C", 0);
        Check(graph.Find(3000000001L, 4).Meters < 0.580245, "Zero-cost fittings and cache invalidation");
        Check(CableWritePlanner.RoundedMeters(5, 5) == 5 && CableWritePlanner.RoundedMeters(5.000001, 5) == 10 &&
              CableWritePlanner.RoundedMeters(0.580245, 1) == 1 && CableWritePlanner.RoundedMeters(4.999999999999, 1) == 5, "Rounding only after summing the route");
    }

    private static void TestDiagram()
    {
        var equipment = new List<DiagramEquipment> { Eq(1, "ЩА-1", 0, 0), Eq(2, "Д1", 50, 100), Eq(3, "Д2", 100, 100) };
        var lines = new List<DiagramLine> { Line(0,0,100,0,true), Line(50,0,50,100), Line(100,0,100,100) };
        var cables = new List<DiagramCable> { Cable(11,"1.1","ЩА-1","Д1",50,100), Cable(12,"1.2","ЩА-1","Д2",100,100) };
        var connections = CableDiagram.Resolve(equipment, lines, cables);
        Check(connections.All(c => c.Error == "" && c.IsStar && c.BeginId == 1) && connections.Select(c => c.EndId).SequenceEqual(new long[] { 2,3 }), "Pinned bus yields independent star routes from common beginning");
        equipment.Add(Eq(4,"Д1",300,100)); // такое же имя в ДРУГОЙ сети допустимо
        Check(CableDiagram.Resolve(equipment,lines,cables).All(c => c.Error == ""), "Names resolved within the line component");
        equipment.Add(Eq(5,"Д1",100,0));
        Check(CableDiagram.Resolve(equipment,lines,cables)[0].Error.Contains("одно начало"), "Two different model elements on the pinned bus blocked");
        equipment.RemoveAt(equipment.Count-1);
        cables[1].Number="1.1";
        Check(CableDiagram.Resolve(equipment,lines,cables).All(c => c.Error.Contains("повторяется")), "Duplicate cable numbers blocked");
        cables[1].Number="1.2"; cables[0].Point=new DiagramPoint(900,900);
        Check(CableDiagram.Resolve(equipment,lines,cables)[0].Error.Length > 0, "Moved cable point not silently routed");
        var crossing = new[] { Line(-100,0,100,0), Line(0,-100,0,100) };
        var crossEquipment = new[] { Eq(1,"A",-100,0), Eq(2,"B",100,0), Eq(3,"A",0,-100), Eq(4,"B",0,100) };
        Check(CableDiagram.Resolve(crossEquipment,crossing,new[] { Cable(1,"1","A","B",100,0) })[0].Error == "", "Crossing lines without endpoint stay separate");
        Check(CableDiagram.Resolve(new[] {Eq(1,"A",0,0),Eq(2,"B",100,0),Eq(3,"B",50,0)},
            new[] {Line(0,0,50,0),Line(50,0,100,0)},new[] {Cable(1,"1","A","B",100,0)})[0].Error.Contains("Неоднозначное"),
            "Ordinary unpinned connections retain their name-based checks");
    }

    private static void TestStarGeometry()
    {
        // Один и тот же текст у всех панелей. Старые CJ-поля намеренно пустые/устаревшие:
        // реальную связь задают закрепление линий, положение зелёной точки и NS_ElementId.
        var equipment = new List<DiagramEquipment> {Eq(1,"Панель",0,0),Eq(2,"Панель",30,100),Eq(3,"Панель",70,100)};
        var lines = new List<DiagramLine> {Line(0,0,0,50,true),Line(100,50,0,50,true),
            Line(30,50,30,70),Line(30,100,30,70),Line(70,100,70,50)};
        var cables = new[] {Cable(11,"1.1","старое начало","Панель",30,100),Cable(12,"1.2","","",70,100)};
        var connections = CableDiagram.Resolve(equipment,lines,cables);
        Check(connections.All(c=>c.Error=="" && c.IsStar && c.BeginId==1) &&
            connections.Select(c=>c.EndId).SequenceEqual(new long[]{2,3}), "Star with duplicate panel names resolves each unpinned branch geometrically");
        Check(connections.All(c=>c.BeginLabel=="Панель" && c.EndLabel=="Панель") && cables[0].Begin=="старое начало" && cables[1].End=="",
            "Preview uses actual endpoint captions without rewriting source cable fields");
        var reversed = CableDiagram.Resolve(equipment.AsEnumerable().Reverse().ToList(),
            lines.AsEnumerable().Reverse().Select(l=>new DiagramLine{A=l.B,B=l.A,Pinned=l.Pinned}).ToList(),cables);
        Check(reversed.Select(c=>c.BeginId+":"+c.EndId).SequenceEqual(connections.Select(c=>c.BeginId+":"+c.EndId)),
            "Star orientation does not depend on line direction or collector order");

        var graph=new CableRouteGraph();graph.AddPort("root",1);graph.AddPort("fork",9);graph.AddPort("end1",2);graph.AddPort("end2",3);
        graph.Connect("root","fork",4,101,"АОВ");graph.Connect("fork","end1",6,102,"АОВ");graph.Connect("fork","end2",16,103,"АОВ");
        var routes=connections.Select(c=>new CableRouteRow(c,graph.Find(c.BeginId,c.EndId),"0","")).ToList();
        Check(routes[0].Path!.Meters==10 && routes[1].Path!.Meters==20,"Identical captions route to different actual model endpoints");
        var writes=CableWritePlanner.Plan(routes,new[]{Duct(101,"s",""),Duct(102,"s",""),Duct(103,"s","")},"text",false,false);
        Check(writes.Single(w=>w.Id==101).NewValue=="1.1"+Environment.NewLine+"1.2" &&
            writes.Single(w=>w.Id==102).NewValue=="1.1" && writes.Single(w=>w.Id==103).NewValue=="1.2", "Branch cable numbers reach the correct duct routes");

        equipment[2].Panel="";
        Check(CableDiagram.Resolve(equipment,lines,cables)[1].EndLabel=="Элемент 3","Blank panel caption is optional for geometric star");
        equipment[2].ModelId=0;
        var invalid=CableDiagram.Resolve(equipment,lines,cables);
        Check(invalid[0].Error=="" && invalid[1].IsStar && invalid[1].Error.Contains("NS_ElementId"),"Missing model ID blocks only the affected branch");
        equipment[2].ModelId=3;
        equipment.Add(Eq(4,"Другой элемент",30,100));
        Check(CableDiagram.Resolve(equipment,lines,cables)[0].Error.Contains("совмещены"),"Truly overlapping endpoints remain an explicit geometry error");
        equipment.RemoveAt(equipment.Count-1);
        cables[0].Point=new DiagramPoint(30,50);
        Check(CableDiagram.Resolve(equipment,lines,cables)[0].Error.Contains("окончания"),"Cable point at the bus junction is not mistaken for equipment");
        cables[0].Point=new DiagramPoint(30,100);
        lines.Add(Line(200,50,200,0,true));lines.Add(Line(100,50,200,50));equipment.Add(Eq(5,"Панель",200,0));
        Check(CableDiagram.Resolve(equipment,lines,cables).All(c=>c.Error=="" && c.BeginId==1),"A second bus connected by another unpinned branch does not replace the local star root");
        lines.Add(Line(70,70,200,50));
        Check(CableDiagram.Resolve(equipment,lines,cables)[1].Error.Contains("одной закреплённой"),"An actual branch joined to two pinned buses is rejected without guessing");
        Console.WriteLine("PASS: star geometry resolves equal/blank/stale names, pinned polylines, unpinned branches, NS_ElementId routes, multiple buses and real geometric ambiguity.");
    }

    private static void TestWritesAndWindow()
    {
        var rows = new[] {
            Row(11,"1.1",1,2,12.25, new long[] {101,102}), Row(12,"1.2",1,3,22.75,new long[] {101,103}),
            new CableRouteRow(new CableConnection(Cable(13,"2","ЩА-2","Д3",0,0),4,5,false),null,"5","Нет физического пути") };
        var ducts = new[] { Duct(101,"s1","старое"), Duct(102,"s1",""), Duct(103,"s1",""), Duct(104,"s1","старый маршрут"), Duct(201,"s2","другая система") };
        var parameter = new CableChoice("text","NS_Кабель_АОВ");
        var vm = new DuctCableViewModel("АОВ · Структурная схема",rows,ducts,new[] {parameter,new CableChoice("missing","Другой параметр")},
            new[] {new CableChoice("view","План уровня 1")},new[] {new CableChoice("tag","Марка АОВ")},"");
        Check(!vm.CanWrite, "No arbitrary default parameter");
        rows[2].Included=false; vm.Parameter=parameter;
        Check(vm.CanWrite && vm.Writes.Count==3 && vm.Writes.Single(w=>w.Id==101).NewValue == "1.1"+Environment.NewLine+"1.2", "All cables accumulated on shared duct");
        Check(rows[0].NewLength=="13" && rows[1].NewLength=="23", "Whole-meter length preview");
        vm.Append=true;
        Check(vm.Writes.Single(w=>w.Id==101).NewValue.StartsWith("старое"), "Append preserves other labels");
        vm.ClearUnused=true; Check(!vm.CanWrite,"Cleanup blocked for partial or append mode");
        vm.ClearUnused=false;vm.Append=false;vm.Rounding="5";Check(rows[0].NewLength=="15" && rows[1].NewLength=="25", "Rounding step binding");
        vm.Parameter=vm.Parameters[1];Check(!vm.CanWrite && vm.Writes.All(w=>w.Error.Length>0), "Missing parameter coverage blocks partial writes");
        vm.Parameter=parameter; vm.CreateTags=true;Check(!vm.CanWrite,"Tag options required");vm.TagView=vm.TagViews[0];vm.TagType=vm.TagTypes[0];Check(vm.CanWrite,"Tag options selected");vm.CreateTags=false;
        var complete = new DuctCableViewModel("Схема", rows.Take(2).ToArray(), ducts,new[] {parameter},Array.Empty<CableChoice>(),Array.Empty<CableChoice>(),"text");
        complete.ClearUnused=true;
        Check(complete.CanWrite && complete.Writes.Single(w=>w.Id==104).NewValue=="" && complete.Writes.All(w=>w.Id!=201), "Cleanup constrained to used physical systems");
        var repeated = vm.Writes.Select(w=>Duct(w.Id,"s1",w.NewValue)).ToArray();vm.Append=true;
        Check(CableWritePlanner.Plan(rows,repeated,"text",true,false).Single(w=>w.Id==101).NewValue=="1.1"+Environment.NewLine+"1.2", "Repeat append does not duplicate labels"); vm.Append=false;

        var window=new DuctCableView(vm);window.Show();Pump(window);
        Check(((DataGrid)window.FindName("CableTable")).Items.Count==3 && ((Button)window.FindName("Confirm")).IsEnabled,"Real WPF binding and confirmation state");
        SavePreview(window,"duct-cable-routes.png");
        ((TabItem)window.FindName("WritesTab")).IsSelected=true;Pump(window);SavePreview(window,"duct-cable-writes.png");
        ((TextBox)window.FindName("RoundingInput")).Text="bad";Pump(window);
        Check(!((Button)window.FindName("Confirm")).IsEnabled,"Invalid text blocks UI write");
        window.Close();vm.Rounding="1";
        var modal=new DuctCableView(vm);
        modal.Dispatcher.BeginInvoke(new Action(()=>((Button)modal.FindName("Confirm")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent))),DispatcherPriority.ApplicationIdle);
        Check(modal.ShowDialog()==true,"WPF modal confirmation");
    }

    private static void TestPreferences()
    {
        var folder=Path.Combine(Path.GetTempPath(),"duct-cable-test-"+Guid.NewGuid().ToString("N"));var file=Path.Combine(folder,"views.xml");
        try
        {
            Check(CableRoutingPreferences.Read(file,"A")=="","Absent preferences");
            CableRoutingPreferences.Save(file,"A","11");CableRoutingPreferences.Save(file,"B","22");CableRoutingPreferences.Save(file,"A","33");
            Check(CableRoutingPreferences.Read(file,"A")=="33" && CableRoutingPreferences.Read(file,"B")=="22","Independent per-view parameter persistence");
        }
        finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }

    private static CableRouteRow Row(long id,string number,long a,long b,double meters,long[] ducts) => new(new CableConnection(Cable(id,number,"ЩА-1","Д"+b,0,0),a,b,true),new CablePath(meters,ducts,new[]{"Трасса АОВ"}),"5","");
    private static CableDuctSnapshot Duct(long id,string system,string value) {var d=new CableDuctSnapshot{Id=id,SystemId=system,System="Трасса "+system};d.Values["text"]=value;return d;}
    private static DiagramEquipment Eq(long id,string panel,double x,double y)=>new(){Id=id,ModelId=id,Panel=panel,Min=new DiagramPoint(x-3,y-3),Max=new DiagramPoint(x+3,y+3)};
    private static DiagramLine Line(double x,double y,double u,double v,bool pinned=false)=>new(){A=new DiagramPoint(x,y),B=new DiagramPoint(u,v),Pinned=pinned};
    private static DiagramCable Cable(long id,string n,string a,string b,double x,double y)=>new(){Id=id,Number=n,Begin=a,End=b,Point=new DiagramPoint(x,y)};
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Expect(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("Expected route error");}
    private static void Pump(Window window){window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();}
    private static void SavePreview(Window window,string name)
    {
        var folder=Environment.GetEnvironmentVariable("NGRAPH_UI_PREVIEW_DIR");if(string.IsNullOrEmpty(folder))return;
        Directory.CreateDirectory(folder);window.UpdateLayout();
        var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var output=File.Create(Path.Combine(folder,name));encoder.Save(output);
    }
}
