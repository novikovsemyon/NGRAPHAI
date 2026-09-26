using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NGraph.Core;
using NGraph.Core.ModelSchemes;
using NGraph.ViewModels;
using NGraph.Views;

internal static class SchemeSymbolTests
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "ngraph-symbols-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "АК_Газ.ini");
            const string original = "; Комментарий инженера\r\n[QE]\r\nSymbol=Датчик газа\r\nPort_Di=2\r\nCustom=не менять\r\n[Spare]\r\nOther=17\r\n";
            File.WriteAllText(path, original, new UTF8Encoding(false)); var bytes = File.ReadAllBytes(path);
            var file = SchemeIniFile.Open(path);
            Check(file.Text == original && file.ReadSection("qe")["symbol"] == "Датчик газа", "UTF-8 and case-insensitive INI keys");
            file.Save(original.Replace("Датчик газа", "Щит автоматики"));
            Check(File.ReadAllBytes(path + ".bak").SequenceEqual(bytes), "Exact backup of original file");
            Check(File.ReadAllBytes(path).Take(2).SequenceEqual(new byte[] { 255, 254 }), "Save in Revit-compatible UTF-16");
            Check(new INIManager(path).GetPrivateString("QE", "Symbol") == "Щит автоматики", "Legacy WinAPI reader can read editor output");
            Check(SchemeIniFile.Open(path).Text.Contains("Custom=не менять") && SchemeIniFile.Open(path).ReadSection("Spare")["Other"] == "17", "Unrelated content preserved");
            var stale = SchemeIniFile.Open(path); File.AppendAllText(path, "; external\r\n", Encoding.Unicode);
            Expect<IOException>(() => stale.Save("stale text"));
            Check(SchemeIniFile.Open(path).Text.Contains("; external"), "External edits preserved");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            File.WriteAllBytes(path, Encoding.GetEncoding(1251).GetBytes(original));
            Check(new SchemeIniCatalog(root).Resolve("АК_Газ", "QE") == "Датчик газа", "Windows-1251 input");
            File.WriteAllText(path, "[QE]\nSymbol=A\nSymbol=B", Encoding.Unicode);
            Expect<InvalidOperationException>(() => new SchemeIniCatalog(root).Resolve("АК_Газ", "QE"));
            File.WriteAllText(path, original, Encoding.Unicode);

            var eq = new SchemeSourceElement("1", "Датчик модели", "АК_Газ", "QE", "ЩР-1", "1", "Датчик", new Dictionary<string,string> {
                ["zone"] = "Зона 1", ["floor"] = "2", ["comments"] = "Система газа", ["type-comments"] = "Не использовать" },
                () => new SchemeLevel("L2", "Уровень 2", 3000));
            var parameters = new[] {
                new SchemeParameterChoice("type-comments", "Комментарии", true, 1, 1),
                new SchemeParameterChoice("comments", "Комментарии", false, 1, 1),
                new SchemeParameterChoice("zone", "ADSK_Зона", false, 1, 1),
                new SchemeParameterChoice("floor", "ADSK_Этаж", false, 1, 1) };
            IReadOnlyList<SchemeSymbolRow> Load(IReadOnlyList<SchemeSourceElement> elements)
            {
                var catalog = new SchemeIniCatalog(root);
                return elements.Select(e => {
                    var row = new SchemeSymbolRow(e) { IniPath = catalog.FindFile(e.IniGroup) };
                    try {
                        row.Symbol = catalog.Resolve(e.IniGroup, e.Position); row.FamilyName = "Тестовое семейство";
                        row.IniValues = string.Join("\n", catalog.ReadSection(e.IniGroup, e.Position).Select(v => v.Key + " = " + v.Value));
                    } catch (InvalidOperationException ex) { row.MappingError = ex.Message; }
                    return row;
                }).ToList();
            }
            var vm = new ParameterSchemeViewModel(new[] { eq }, parameters, root, Load);
            Check(vm.FilterParameter == parameters[1] && vm.GroupNameParameter == parameters[2] && vm.UseModelLevel && vm.LevelParameter == parameters[3], "Requested defaults prefer instance comments, zone and floor");
            vm.UseParameterLevel = true;
            Check(vm.GetOptions().LevelParameterKey == "floor" && vm.Plan.Groups.Single().LevelName == "2", "Switching to parameter level uses ADSK_Этаж");
            vm.LevelParameter = parameters[2]; vm.UseModelLevel = true; vm.UseParameterLevel = true;
            Check(vm.LevelParameter == parameters[2], "Toggling source keeps the user's parameter selection");
            vm.LevelParameter = parameters[3];
            var missing = new ParameterSchemeViewModel(new[] { eq }, parameters.Take(1).ToArray(), root);
            Check(missing.FilterParameter?.Key == "" && missing.GroupNameParameter == null && missing.LevelParameter == null, "Missing defaults do not select type comments or arbitrary parameters");

            int renders = 0;
            var window = new ParameterSchemeView(vm, (row, viewName) => {
                renders++;
                var image = new DrawingImage(new GeometryDrawing(Brushes.White, new Pen(Brushes.DarkBlue, 2), new RectangleGeometry(new Rect(2, 2, 100, 42))));
                image.Freeze(); row.SetPreview(image, "Тест отображения WPF: " + viewName);
            });
            window.Show(); ((TabItem)window.FindName("SymbolsTab")).IsSelected = true; Pump(window);
            var grid = (DataGrid)window.FindName("EquipmentPreview"); grid.SelectedIndex = 0; Pump(window); DrainPreviews(window);
            Check(grid.Items.Count == 1 && vm.Symbols[0].Symbol == "Датчик газа" && vm.Symbols[0].Preview != null && renders > 0, "INI values and image appear in equipment rows");
            SavePreview(window, "scheme-ini-table.png");
            // Открываем именно встроенный редактор выбранной строки, сохраняем и проверяем перечитывание таблицы.
            window.Dispatcher.BeginInvoke(new Action(() => {
                var editor = Application.Current.Windows.OfType<SchemeIniEditorView>().Single();
                var text = (TextBox)editor.FindName("Editor"); text.Text = text.Text.Replace("Датчик газа", "Щит автоматики");
                SavePreview(editor, "scheme-ini-editor.png");
                ((Button)editor.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }), DispatcherPriority.ApplicationIdle);
            ((Button)window.FindName("EditIni")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump(window); DrainPreviews(window);
            Check(vm.Symbols.Single().Symbol == "Щит автоматики" && vm.CanBuild, "Editor save reloads INI mapping and permits build");
            Check(new INIManager(path).GetPrivateString("QE", "Symbol") == "Щит автоматики", "Saved mapping reaches the file");
            File.WriteAllText(path, "[QE]\r\nCustom=5", Encoding.Unicode);
            ((Button)window.FindName("ReloadIni")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(window);
            Check(!vm.CanBuild && vm.Symbols.Single().MappingError.Length > 0, "Invalid refreshed mapping blocks construction");
            window.Close();
            Console.WriteLine("PASS: requested scheme defaults, instance/type distinction, INI encodings, backup, external-change protection, WPF INI editor, mapping reload and preview image binding.");
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Expect<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    private static void Pump(Window window) { window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
    private static void DrainPreviews(Window window)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); Pump(window);
    }
    private static void SavePreview(Window window, string name)
    {
        var folder = Environment.GetEnvironmentVariable("NGRAPH_UI_PREVIEW_DIR"); if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder); window.UpdateLayout();
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(Path.Combine(folder, name)); encoder.Save(output);
    }
}
