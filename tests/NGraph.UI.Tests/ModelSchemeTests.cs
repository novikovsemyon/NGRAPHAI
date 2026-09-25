using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NGraph.Core.ModelSchemes;
using NGraph.ViewModels;
using NGraph.Views;

internal static class ModelSchemeTests
{
    public static void Run()
    {
        SchemeSourceElement Equipment(string id, string floor = "2", string section = "1", string number = "101",
            string group = "Щитовая", Func<SchemeSpace?>? space = null, Func<SchemeLevel?>? level = null) =>
            new(id, "Щит автоматики " + id, "АК_Газ", "QE", "ЩР-" + id, id, "Щит", new Dictionary<string, string> {
                ["group"] = group, ["floor"] = floor, ["section"] = section, ["number"] = number,
                ["filter"] = id == "4" ? "Резерв" : "Работа", ["type:group"] = "Группа из типа" },
                space ?? (() => throw new Exception("Parameter mode read a Space")),
                level ?? (() => throw new Exception("Parameter level read a model Level")));
        SchemeOptions ParameterOptions() => new() { Mode = SchemeGroupingMode.Parameters, LevelSource = SchemeLevelSource.Parameter,
            GroupNameParameterKey = "group", GroupNumberParameterKey = "number", SectionParameterKey = "section", LevelParameterKey = "floor" };
        var elements = new[] { Equipment("1"), Equipment("2", "10"), Equipment("3", section: "2"), Equipment("4", number: "102"), Equipment("5") };
        var options = ParameterOptions();
        var plan = SchemePlanner.Build(elements, options);
        Check(plan.CanBuild && plan.Elements.Count == 5 && plan.Groups.Count == 4, "Parameter grouping independent of Space and Level");
        Check(plan.Groups[0].Count == 2 && plan.Groups[0].LevelName == "2" && plan.Groups[2].LevelName == "10", "Natural floors and composite group identity");
        Check(plan.Elements.All(e => e.ElevationMillimeters == null), "No physical elevation in parameter mode");
        options.FilterParameterKey = "filter"; options.FilterValue = "Резерв";
        Check(SchemePlanner.Build(elements, options).Elements.Single().Id == "4", "Parameter value filter");
        options.FilterParameterKey = "floor"; options.FilterValue = "10";
        Check(SchemePlanner.Build(elements, options).Elements.Single().Id == "2", "Numeric display value filter");
        options = ParameterOptions(); options.GroupNameParameterKey = "type:group"; options.GroupNumberParameterKey = "";
        Check(SchemePlanner.Build(elements, options).Groups.Count == 3, "Type source remains distinct from instance source");
        options = ParameterOptions(); options.GroupNameParameterKey = "";
        Check(!SchemePlanner.Build(elements, options).CanBuild, "Require group source");
        options = ParameterOptions(); options.LevelParameterKey = "";
        Check(!SchemePlanner.Build(elements, options).CanBuild, "Require parameter level source");

        var incomplete = new[] { Equipment("1"), Equipment("2", floor: "", group: "") };
        options = ParameterOptions(); plan = SchemePlanner.Build(incomplete, options);
        Check(plan.CanBuild && plan.Elements.Count == 2 && plan.Issues.Count == 1, "Keep empty values in explicit groups");
        options.MissingValues = MissingSchemeValuePolicy.Exclude; plan = SchemePlanner.Build(incomplete, options);
        Check(plan.CanBuild && plan.Elements.Count == 1 && plan.SkippedCount == 1, "Exclude only incomplete elements");
        options.MissingValues = MissingSchemeValuePolicy.Stop;
        Check(!SchemePlanner.Build(incomplete, options).CanBuild, "Strict empty-value policy");
        options = ParameterOptions(); options.FilterParameterKey = "floor"; options.FilterValue = "";
        Check(SchemePlanner.Build(incomplete, options).Elements.Single().Id == "2", "Empty filter is distinct from all values");
        options.FilterValue = null;
        Check(SchemePlanner.Build(incomplete, options).Elements.Count == 2, "All-value filter");

        var physical = new[] {
            Equipment("1", space: () => new SchemeSpace("S1", "Комната", "101"), level: () => new SchemeLevel("L1", "Нижний", -3000)),
            Equipment("2", space: () => new SchemeSpace("S2", "Комната", "101"), level: () => new SchemeLevel("L2", "Верхний", 3000)),
            Equipment("3", space: () => new SchemeSpace("S3", "Комната", "101"), level: () => new SchemeLevel("L1", "Нижний", -3000)) };
        options = new SchemeOptions { SectionParameterKey = "section" };
        plan = SchemePlanner.Build(physical, options);
        Check(plan.Groups.Count == 3 && plan.Groups[0].First.ElevationMillimeters == -3000, "Spatial IDs and actual elevation preserved");
        options = ParameterOptions(); options.LevelSource = SchemeLevelSource.Model;
        var native = new[] { Equipment("1", level: () => new SchemeLevel("L", "Этаж 1", 0)) };
        Check(SchemePlanner.Build(native, options).Elements.Single().LevelName == "Этаж 1", "Parameter groups may use native level without Space");
        var missingNative = new[] { Equipment("1", level: () => null) };
        Check(SchemePlanner.Build(missingNative, options).Issues.Count == 1, "Missing native level is explicit");
        var comparer = SchemeValueComparer.Instance;
        var ordered = new[] { "10", "2", "-2", "-10", "Этаж 10", "Этаж 2" }.OrderBy(x => x, comparer).ToArray();
        Check(ordered.SequenceEqual(new[] { "-10", "-2", "2", "10", "Этаж 2", "Этаж 10" }), "Numeric and natural sorting");
        var labels = new[] { "-2", "-10", "-9a", "2", "01", "1", "Этаж 10", "Этаж 2" };
        foreach (var a in labels) foreach (var b in labels) foreach (var c in labels)
            if (comparer.Compare(a, b) <= 0 && comparer.Compare(b, c) <= 0)
                Check(comparer.Compare(a, c) <= 0, "Comparer is transitive");

        CheckIni();
        var parameters = new[] {
            new SchemeParameterChoice("group", "ADSK_Наименование помещения", false, 5, 5),
            new SchemeParameterChoice("number", "ADSK_Номер помещения", false, 5, 5),
            new SchemeParameterChoice("floor", "ADSK_Этаж", false, 5, 5),
            new SchemeParameterChoice("section", "ADSK_Номер секции", false, 5, 5),
            new SchemeParameterChoice("filter", "Комментарии", false, 5, 5) };
        var vm = new NGraphCreateSxemaByModelViewModel(elements, parameters, @"C:\Users\User\AppData\Roaming\NGraph\Settings", SchemeGroupingMode.Parameters);
        vm.UseParameterLevel = true; vm.LevelParameter = parameters[2]; vm.GroupNumberParameter = parameters[1]; vm.GroupNameParameter = parameters[0];
        var window = new NGraphCreateSxemaByModelView(vm); Show(window);
        var grid = (DataGrid)window.FindName("GroupsPreview");
        var confirm = (Button)window.FindName("Confirm");
        Check(grid.Items.Count == 4 && confirm.IsEnabled, "WPF preview and confirmation reflect parameter plan");
        Check(((ComboBox)window.FindName("LevelParameter")).IsEnabled, "Parameter level picker enabled");
        SavePreview(window, "model-scheme-parameters.png");
        vm.FilterParameter = parameters[4]; vm.FilterValue = vm.FilterValues.Single(v => v.Value == "Резерв");
        Pump(window); Check(grid.Items.Count == 1 && vm.Plan.Elements.Single().Id == "4", "WPF filter refresh");
        ((TextBox)window.FindName("ViewName")).Text = " "; Pump(window);
        Check(!confirm.IsEnabled, "Empty view name blocks submission");
        ((TextBox)window.FindName("ViewName")).Text = "Схема по параметрам"; Pump(window);
        window.Hide(); window.Dispatcher.BeginInvoke(new Action(() => confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
        Check(window.ShowDialog() == true && vm.GetOptions().LevelSource == SchemeLevelSource.Parameter, "Modal result retains exact source choices");
        var spatialVm = new NGraphCreateSxemaByModelViewModel(physical, parameters, "INI");
        var spatialWindow = new NGraphCreateSxemaByModelView(spatialVm); Show(spatialWindow);
        Check(!((ComboBox)spatialWindow.FindName("GroupNameParameter")).IsEnabled && spatialVm.Plan.Groups.Count == 3, "Spatial mode disables replacement fields");
        SavePreview(spatialWindow, "model-scheme-spaces.png"); spatialWindow.Close();
        Console.WriteLine("PASS: model scheme parameter/space modes, optional native levels, filters, empty-value policies, grouping identity, sorting, INI validation and actual WPF preview.");
    }

    private static void CheckIni()
    {
        var root = Path.Combine(Path.GetTempPath(), "ngraph-scheme-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "АК_Газ.ini"), "[QE]\r\nSymbol=Датчик газа\r\n", Encoding.Unicode);
            Check(new SchemeIniCatalog(root).Resolve("АК_Газ", "QE") == "Датчик газа", "Unicode INI symbol mapping");
            void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Invalid INI accepted"); }
            Reject(() => new SchemeIniCatalog(root).Resolve("АК_Газ", "Missing"));
            Reject(() => new SchemeIniCatalog(root).Resolve("АК_Нет", "QE"));
            var sub = Path.Combine(root, "duplicate"); Directory.CreateDirectory(sub);
            File.Copy(Path.Combine(root, "АК_Газ.ini"), Path.Combine(sub, "АК_Газ.ini"));
            Reject(() => new SchemeIniCatalog(root).Resolve("АК_Газ", "QE"));
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Show(Window window) { window.Show(); Pump(window); }
    private static void Pump(Window window) { window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
    private static void SavePreview(Window window, string filename)
    {
        var directory = Environment.GetEnvironmentVariable("NGRAPH_UI_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(directory, filename)); encoder.Save(stream);
    }
}
