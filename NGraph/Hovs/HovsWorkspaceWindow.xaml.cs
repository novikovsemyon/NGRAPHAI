using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClosedXML.Excel;
using HOVS.Model;
using HOVS.Plugin;
using Microsoft.Win32;

namespace NGraph.Views;

/// <summary>Окно управляет внешними объектами. Фоновые операции работают только с XLSX и файлами, не с Revit API.</summary>
public partial class HovsWorkspaceWindow
{
    private readonly HovsRepository _repository;
    private List<HovsRow> _rows = new();
    private HovsModel? _model;
    private HovsProject? _project;
    private HovsRevision? _revision;
    private string _source = "";
    private readonly List<string> _temporarySources = new();
    private bool _busy;
    private string _savedState = "";

    public HovsWorkspaceWindow()
    {
        DialogTheme.Prepare(this); InitializeComponent();
        HovsEnvironment.Root = Core.UserSettings.Load().HovsFolder;
        _repository = new HovsRepository(HovsEnvironment.Root);
        StoragePath.Text = HovsEnvironment.Root;
        TypeColumn.ItemsSource = InstallationTypeClassifier.AllTypes;
        Projects.ItemsSource = _repository.Projects();
        if (Projects.Items.Count > 0) Projects.SelectedIndex = 0;
        RefreshTraining();
    }
    private void RefreshTraining()
    {
        var path = Path.Combine(HovsEnvironment.Root, "training_corrections_v2.tsv");
        TrainingStatus.Text = "Сохранено записей исправлений: " + (File.Exists(path) ? File.ReadLines(path).Count() : 0);
    }
    private void CommitGrid()
    {
        Installations.CommitEdit(DataGridEditingUnit.Cell, true);
        Installations.CommitEdit(DataGridEditingUnit.Row, true);
    }
    private string State() => string.Join("\n", _rows.Select(r => r.Selected + "|" + string.Join("|",
        typeof(InstallationFeatures).GetProperties().Select(p => p.GetValue(r.Features)))));
    private bool CanReplace()
    {
        CommitGrid();
        return _model == null || State() == _savedState || MessageBox.Show(this,
            "Есть несохранённые данные. Продолжить без сохранения?", "NGraph", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
    private void CreateProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var p = _repository.Create(ProjectName.Text);
            Projects.ItemsSource = _repository.Projects();
            Projects.SelectedItem = Projects.Items.Cast<HovsProject>().First(x => x.Id == p.Id);
            ProjectName.Clear(); Status.Text = "Объект создан.";
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Project_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_repository == null) return;
        try { Revisions.ItemsSource = Projects.SelectedItem is HovsProject p ? _repository.Revisions(p) : null; }
        catch (Exception ex) { Error(ex); }
    }
    private void Project_RightClick(object sender, MouseButtonEventArgs e) => ((ListBoxItem)sender).IsSelected = true;
    private void Revision_RightClick(object sender, MouseButtonEventArgs e) => ((DataGridRow)sender).IsSelected = true;
    private void RenameProject_Click(object sender, RoutedEventArgs e)
    {
        if (Projects.SelectedItem is not HovsProject project) return;
        var prompt = new HovsProjectNameWindow(project.Name) { Owner = this };
        if (prompt.ShowDialog() != true) return;
        try
        {
            _repository.Rename(project, prompt.ProjectName);
            if (_project?.Id == project.Id) { _project.Name = project.Name; CurrentContext.Text = "Объект: " + project.Name + (_revision == null ? " · новый импорт" : " · " + _revision.Name); }
            Projects.Items.Refresh(); Status.Text = "Объект переименован.";
        }
        catch (Exception ex) { Error(ex); }
    }
    private void ClearModel()
    {
        _model = null; _project = null; _revision = null; _source = ""; _savedState = "";
        _rows.Clear(); Installations.ItemsSource = null; Relations.ItemsSource = null;
        SourceText.Clear(); CurrentContext.Text = "";
    }
    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (Projects.SelectedItem is not HovsProject project) return;
        if (_project?.Id == project.Id && !CanReplace()) return;
        if (MessageBox.Show(this, "Удалить объект «" + project.Name + "» со всеми ревизиями и копиями XLSX? Общее обучение сохранится.", "Удаление объекта", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            _repository.DeleteProject(project);
            if (_project?.Id == project.Id) ClearModel();
            Projects.ItemsSource = _repository.Projects();
            if (Projects.Items.Count > 0) Projects.SelectedIndex = 0;
            Status.Text = "Объект и его ревизии удалены. Обучение сохранено.";
        }
        catch (Exception ex) { Error(ex); }
    }
    private void DeleteRevision_Click(object sender, RoutedEventArgs e)
    {
        if (Projects.SelectedItem is not HovsProject project || Revisions.SelectedItem is not HovsRevision revision) return;
        if (_revision?.Id == revision.Id && !CanReplace()) return;
        if (MessageBox.Show(this, "Удалить ревизию «" + revision.Name + "» и её копию XLSX? Остальные ревизии сохранятся.", "Удаление ревизии", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            _repository.DeleteRevision(project, revision);
            if (_revision?.Id == revision.Id) ClearModel();
            Revisions.ItemsSource = _repository.Revisions(project);
            Status.Text = "Ревизия удалена.";
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (Projects.SelectedItem is not HovsProject project) { Status.Text = "Выберите объект."; return; }
        try { new HovsRevisionCompareWindow(_repository, project) { Owner = this }.ShowDialog(); }
        catch (Exception ex) { Error(ex); }
    }
    private HovsSchema ResolveSchema(XLWorkbook workbook, string path)
    {
        var analyses = workbook.Worksheets.Select(HovsSchemaAnalyzer.AnalyzeSheet).Where(x => x.LastColumn > 0)
            .OrderByDescending(x => x.Score).ToList();
        if (analyses.Count == 0) throw new InvalidOperationException("В книге нет непустых листов.");
        HovsSchema? matched; double score;
        if (ForceSchema.IsChecked != true && HovsSchemaProfileStore.TryResolve(workbook, analyses, out matched, out score)
            && matched != null && score >= .90) { matched.FromProfile = true; matched.ProfileConfidence = score; return matched; }
        var wizard = new HovsSchemaWizardWindow(workbook, analyses[0].Schema, path) { Owner = this };
        if (wizard.ShowDialog() != true || wizard.ResultSchema == null) throw new OperationCanceledException();
        HovsSchemaProfileStore.Save(wizard.ResultSchema, path);
        return wizard.ResultSchema;
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || Projects.SelectedItem is not HovsProject project) { Status.Text = "Выберите объект."; return; }
        if (!CanReplace()) return;
        var dialog = new OpenFileDialog { Filter = "ХОВС Excel (*.xlsx)|*.xlsx", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            // Снимок исключает изменение файла между разметкой, анализом и сохранением ревизии.
            var snapshot = Path.Combine(Path.GetTempPath(), "NGraph-" + Guid.NewGuid().ToString("N") + ".xlsx");
            File.Copy(dialog.FileName, snapshot); _temporarySources.Add(snapshot);
            HovsSchema schema;
            using (var workbook = new XLWorkbook(snapshot)) schema = ResolveSchema(workbook, dialog.FileName);
            SetBusy(true); Status.Text = "Анализ XLSX…";
            var importer = new ExcelImporter();
            var model = await Task.Run(() => importer.Load(snapshot, (_, _) => schema));
            _project = project; _revision = null; _source = snapshot; ShowModel(model);
            _savedState = ""; // Импорт ещё не сохранён и не должен исчезнуть при закрытии без предупреждения.
            Status.Text = importer.LastDiagnostics.Summary + " · сохраните ревизию";
        }
        catch (OperationCanceledException) { Status.Text = "Импорт отменён."; }
        catch (Exception ex) { Error(ex); }
        finally { SetBusy(false); }
    }
    private void ShowModel(HovsModel model)
    {
        CurrentContext.Text = "Объект: " + _project?.Name + (_revision == null ? " · новый импорт" : " · " + _revision.Name);
        _model = model; _rows = model.Equipment.Select(x => new HovsRow(x)).ToList();
        Installations.ItemsSource = _rows;
        Relations.ItemsSource = model.Relations;
        if (_rows.Count > 0) Installations.SelectedIndex = 0;
        Tabs.SelectedIndex = 1;
        _savedState = State();
    }
    private void OpenRevision_Click(object sender, RoutedEventArgs e)
    {
        if (Revisions.SelectedItem is not HovsRevision revision || Projects.SelectedItem is not HovsProject project) { Status.Text = "Выберите ревизию."; return; }
        if (!CanReplace()) return;
        try { var model = _repository.Load(revision); _source = revision.SourcePath; _project = project; _revision = revision; ShowModel(model); Status.Text = "Открыта ревизия «" + revision.Name + "»."; }
        catch (Exception ex) { Error(ex); }
    }
    private HovsModel UpdatedModel()
    {
        CommitGrid();
        if (_model == null) throw new InvalidOperationException("Сначала импортируйте XLSX или откройте ревизию.");
        foreach (var row in _rows)
        {
            if (row.Selected && !row.HasAirflow) throw new InvalidOperationException("У «" + row.Equipment.Id + "» нет положительного L. Снимите флаг «Включить».");
            ProjectDataOverrides.Save(row.Equipment, row.Features, row.Selected);
        }
        var active = _rows.Where(x => x.Selected && x.HasAirflow).Select(x => new Equipment(x.Equipment.Id,x.Equipment.Name,x.Features.InstallationType,x.Equipment.Room,x.Equipment.Attributes)).ToList();
        var relations = ConnectionRules.Find(active, _model.Components);
        _model = new HovsModel(_model.Equipment, _model.Components, relations);
        Relations.ItemsSource = relations; return _model;
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_project == null) { Status.Text = "Выберите объект и импортируйте XLSX."; return; }
        try
        {
            var model = UpdatedModel(); SetBusy(true);
            var project = _project;
            var revision = await Task.Run(() => _repository.Save(project, model, _source, "Ревизия " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")));
            _revision = revision; _source = revision.SourcePath; _savedState = State();
            Projects.SelectedItem = Projects.Items.Cast<HovsProject>().FirstOrDefault(x => x.Id == project.Id);
            Revisions.ItemsSource = _repository.Revisions(project);
            CurrentContext.Text = "Объект: " + project.Name + " · " + revision.Name;
            Status.Text = "Сохранено в объект «" + project.Name + "». Предыдущие ревизии сохранены.";
        }
        catch (Exception ex) { Error(ex); }
        finally { SetBusy(false); }
    }
    private void Train_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CommitGrid(); int count = 0;
            foreach (var row in Installations.SelectedItems.Cast<HovsRow>())
            {
                var correction = new InstallationFeatures(); bool changed = false;
                foreach (var property in typeof(InstallationFeatures).GetProperties())
                {
                    var value = (string?)property.GetValue(row.Features) ?? "";
                    if (value != row.Initial[property.Name] && !string.IsNullOrWhiteSpace(value))
                    { property.SetValue(correction, value); changed = true; }
                }
                if (!changed) continue;
                TrainingStore.SaveExplicitCorrection(row.Equipment, correction); row.Remember(); count++;
            }
            RefreshTraining(); Status.Text = count == 0 ? "В выбранных строках нет новых исправлений. Чтобы обозначить отсутствие элемента, введите «нет»." : $"Обучение: сохранено {count} проверенных примеров. Применятся при следующем анализе.";
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Relations_Click(object sender, RoutedEventArgs e)
    {
        try { UpdatedModel(); Tabs.SelectedIndex = 2; Status.Text = "Кандидаты связей пересчитаны."; }
        catch (Exception ex) { Error(ex); }
    }
    private void Installation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SourceText == null) return;
        if (Installations.SelectedItem is not HovsRow row) { SourceText.Clear(); return; }
        var cells = row.Equipment.Attributes.Where(a => a.Key.StartsWith(SourceCellCodec.Prefix)).ToList();
        if (cells.Count == 0) cells = row.Equipment.Attributes.Where(a => !a.Key.StartsWith("__")).ToList();
        var lines = cells.Select(a => {
            if (SourceCellCodec.TryDecode(a.Value, out var header, out var value)) return header + ": " + value;
            return a.Key + ": " + a.Value; });
        SourceText.Text = row.Evidence + "\n\n" + string.Join("\n", lines);
    }
    private void SetBusy(bool busy) { _busy = busy; Tabs.IsEnabled = !busy; }
    private void Error(Exception ex) => Status.Text = "Ошибка: " + ex.Message;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_busy) { e.Cancel = true; Status.Text = "Дождитесь завершения операции."; return; }
        if (!CanReplace()) { e.Cancel = true; return; }
        foreach (var path in _temporarySources) try { File.Delete(path); } catch (IOException) { }
    }
}

public sealed class HovsRow
{
    public Equipment Equipment { get; }
    public InstallationFeatures Features { get; }
    public string Airflow { get; }
    public string Confidence { get; }
    public string Evidence { get; }
    public bool HasAirflow { get; }
    public bool Selected { get; set; }
    public Dictionary<string,string> Initial { get; private set; } = new();
    public HovsRow(Equipment equipment)
    {
        Equipment = equipment;
        var analysis = EquipmentFeatureAnalyzer.AnalyzeDetailed(equipment);
        Features = analysis.Features; Airflow = analysis.AirflowText;
        Confidence = analysis.InstallationConfidence.ToString("P0"); Evidence = analysis.Evidence;
        HasAirflow = ConnectionRules.HasPositiveAirflow(equipment);
        Selected = HasAirflow && (!equipment.Attributes.TryGetValue(ProjectDataOverrides.Prefix + "Selected",out var chosen) || chosen == "1");
        Remember();
    }
    public void Remember() => Initial = typeof(InstallationFeatures).GetProperties().ToDictionary(p=>p.Name,p=>(string?)p.GetValue(Features) ?? "");
}
