using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Binding = System.Windows.Data.Binding;
using HOVS.Plugin;

namespace NGraph.Views;

public partial class HovsRevisionCompareWindow
{
    private readonly HovsRepository _repository;
    private readonly List<RevisionChoice> _choices;
    private IReadOnlyList<RevisionCompareRow> _rows = Array.Empty<RevisionCompareRow>();
    private int _generation;
    private bool _batch;
    private bool _closed;

    public HovsRevisionCompareWindow(HovsRepository repository, HovsProject project)
    {
        DialogTheme.Prepare(this); InitializeComponent();
        _repository = repository;
        HeadingText.Text = "Сравнение ХОВС · " + project.Name;
        _choices = repository.Revisions(project).Select(r => new RevisionChoice(r)).ToList();
        RevisionList.ItemsSource = _choices;
        foreach (var choice in _choices) choice.PropertyChanged += Choice_Changed;
        Loaded += (_, _) => Select(2);
    }
    private void Choice_Changed(object? sender, PropertyChangedEventArgs e) { if (!_batch) Rebuild(); }
    private void Select(int count)
    {
        _batch = true;
        for (int i = 0; i < _choices.Count; i++) _choices[i].IsSelected = i < count;
        _batch = false; Rebuild();
    }
    private async void Rebuild()
    {
        int generation = ++_generation;
        var selected = _choices.Where(c => c.IsSelected).OrderBy(c => c.Revision.Created, StringComparer.Ordinal).ToList();
        _rows = Array.Empty<RevisionCompareRow>(); Comparison.ItemsSource = null; Comparison.Columns.Clear();
        if (selected.Count < 2) { Summary.Text = "Выберите хотя бы две ревизии."; return; }
        Summary.Text = "Сравнение сохранённых ревизий…";
        try
        {
            // Быстрая смена флагов не может подменить таблицу результатом старого запроса.
            var rows = await Task.Run(() => RevisionComparison.Compare(selected.Select(c => new RevisionSnapshot(c.Revision.Name, _repository.Load(c.Revision))).ToList()));
            if (_closed || generation != _generation) return;
            AddColumn("Установка", "Designation", 125);
            AddColumn("Статус", "Status", 110);
            AddColumn("Что изменилось · было → стало", "Details", 340);
            for (int i = 0; i < selected.Count; i++) AddColumn(selected[i].Revision.Name + "\n" + selected[i].Revision.CreatedLocal, "Cells[R" + i + "]", 290);
            _rows = rows; Filter();
        }
        catch (Exception ex) { if (!_closed && generation == _generation) Summary.Text = "Сравнение не выполнено: " + ex.Message; }
    }
    private void AddColumn(string header, string path, double width)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5)));
        Comparison.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(path), Width = width, ElementStyle = style });
    }
    private void Filter()
    {
        if (Comparison == null || Search == null || Summary == null) return;
        var search = Search.Text.Trim();
        var filtered = _rows.Where(r => (OnlyChanges.IsChecked != true || r.Status != "Без изменений") &&
            (search.Length == 0 || (r.Designation + " " + r.Status + " " + r.Details + " " + string.Join(" ", r.Cells.Values)).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
        Comparison.ItemsSource = filtered;
        if (_rows.Count == 0) { Summary.Text = "В выбранных ревизиях нет установок."; return; }
        Summary.Text = $"Показано {filtered.Count} из {_rows.Count} · Добавлено: {_rows.Count(r => r.Status == "Добавлена")} · Удалено: {_rows.Count(r => r.Status == "Удалена")} · Изменено: {_rows.Count(r => r.Status == "Изменена")}";
    }
    private void Latest_Click(object sender, RoutedEventArgs e) => Select(2);
    private void All_Click(object sender, RoutedEventArgs e) => Select(_choices.Count);
    private void None_Click(object sender, RoutedEventArgs e) => Select(0);
    private void Filter_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) Filter(); }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (IsLoaded) Filter(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closed(object? sender, EventArgs e)
    {
        _closed = true;
        foreach (var choice in _choices) choice.PropertyChanged -= Choice_Changed;
    }
}

public sealed class RevisionChoice : INotifyPropertyChanged
{
    public HovsRevision Revision { get; }
    public RevisionChoice(HovsRevision revision) => Revision = revision;
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
