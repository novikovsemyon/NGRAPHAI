using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClosedXML.Excel;
using HOVS.Model;
using HOVS.Plugin;
using NGraph.Core;
using NGraph.Views;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "NGraph-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previous = File.Exists(UserSettings.FilePath) ? File.ReadAllBytes(UserSettings.FilePath) : null;
        try
        {
            new UserSettings { HovsFolder = root }.Save();
            HovsEnvironment.Root = root;
            var repository = new HovsRepository(root); var project = repository.Create("Проверка интерфейса ХОВС");
            var source = Path.Combine(root, "source.xlsx");
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("ХОВС"); sheet.Cell(1, 1).Value = "Обозначение"; sheet.Cell(2, 1).Value = "П1"; workbook.SaveAs(source);
            var model = new HovsModel(new[] { new Equipment("П1", "П1", "П", "101", new Dictionary<string, string> { ["__Installation.Airflow"] = "1200" }) }, Array.Empty<EquipmentComponent>(), Array.Empty<Relation>());
            repository.Save(project, model, source, "Исходная"); repository.Save(project, model, source, "Уточнённая");
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            LineNumberingTests.Run();
            ModelSchemeTests.Run();
            SchemeSymbolTests.Run();
            var workspace = new HovsWorkspaceWindow(); Show(workspace);
            var projects = (ListBox)workspace.FindName("Projects");
            if (projects.Items.Count != 1) throw new Exception("Object list did not load");
            var container = (ListBoxItem)projects.ItemContainerGenerator.ContainerFromIndex(0);
            if (container.ContextMenu?.Items.Count != 4) throw new Exception("Object context menu missing");
            var compare = new HovsRevisionCompareWindow(repository, project); Show(compare);
            var grid = (DataGrid)compare.FindName("Comparison");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            var frame = new DispatcherFrame(); var deadline = DateTime.UtcNow.AddSeconds(15);
            timer.Tick += (_, _) => { if (grid.Columns.Count == 5 || DateTime.UtcNow > deadline) frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
            if (grid.Columns.Count != 5) throw new Exception("Comparison did not populate revision columns");
            ((CheckBox)compare.FindName("OnlyChanges")).IsChecked = false;
            if (grid.Items.Count != 1) throw new Exception("Unchanged row filter failed");
            ((TextBox)compare.FindName("Search")).Text = "Нет такой установки";
            if (grid.Items.Count != 0) throw new Exception("Comparison search failed");
            compare.Close();
            var rename = new HovsProjectNameWindow(project.Name); Show(rename); rename.Close();
            var wizard = new HovsSchemaWizardWindow(workbook, new HovsSchema { WorksheetName = "ХОВС", HeaderRow = 1, LastHeaderRow = 1, FirstDataRow = 2 }, source); Show(wizard); wizard.Close();
            var help = new NGraphView(new NGraph.ViewModels.NGraphViewModel { RevitVersion = "2027 (UI test)" }); Show(help);
            if (((ListBox)help.FindName("Topics")).Items.Count != 24) throw new Exception("Help topics missing");
            ((TextBox)help.FindName("Search")).Text = "ОкруглениеКабеля";
            if (((ListBox)help.FindName("Topics")).Items.Count == 0) throw new Exception("Help full-text search failed");
            help.Close(); workspace.Close();
            using var pdf = typeof(HelpContent).Assembly.GetManifestResourceStream("NGraph.Help.UserGuide.pdf");
            if (pdf == null || pdf.ReadByte() != '%') throw new Exception("Bundled PDF missing");
            Console.WriteLine("PASS: real WPF windows, merged styles, context menu, async revision comparison, filters, schema wizard, help search and embedded PDF.");
        }
        finally
        {
            if (previous == null) File.Delete(UserSettings.FilePath); else File.WriteAllBytes(UserSettings.FilePath, previous);
            Directory.Delete(root, true);
        }
    }
    private static void Show(Window window)
    {
        window.Show(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout();
    }
}
