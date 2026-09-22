using System.Windows;
using System.Windows.Controls;
using NGraph.Core.FunctionalScheme;
using NGraph.ViewModels;
namespace NGraph.Views;
/// <summary>Каталог по образцу 2.0.37: группы, поиск и детали. Revit изменяется только после подтверждения.</summary>
public sealed partial class NGraphCreateFootorFromBdView
{
    private readonly List<СекцияБазыДанных> _sections;
    public СекцияБазыДанных? selectedSections => Items.SelectedItem as СекцияБазыДанных;
    public bool Cancel { get; private set; } = true;
    public bool CreateFSA => IsFSAcreate.IsChecked == true;
    public NGraphCreateFootorFromBdView(NGraphCreateFootorFromBdViewModel model)
    {
        DialogTheme.Prepare(this);
        InitializeComponent();
        _sections = model.Sections ?? new List<СекцияБазыДанных>();
        Title = "NGraph — " + model.Title;
        HeadingText.Text = model.Title;
        Groups.ItemsSource = new[] { "Все группы" }.Concat(_sections.Select(x => x.GroupName).Distinct().OrderBy(x => x)).ToList();
        Groups.SelectedIndex = 0;
        Insert.Content = model.InsertLabel;
        Loaded += (_, _) => ApplyFilter();
    }
    private void ApplyFilter()
    {
        if (_sections == null || Items == null) return;
        var group = Groups.SelectedItem as string;
        var search = Search.Text.Trim();
        var rows = _sections.Where(x => (Groups.SelectedIndex <= 0 || x.GroupName == group) &&
            (x.Name + " " + x.GroupName + " " + x.Code).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        Items.ItemsSource = rows;
        if (rows.Count > 0) Items.SelectedIndex = 0;
        Insert.IsEnabled = rows.Count > 0;
        Status.Text = $"Вариантов: {rows.Count} из {_sections.Count}";
    }
    private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void Group_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void Item_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Details == null) return;
        var s = selectedSections;
        Details.Text = s == null ? "Нет выбранного варианта." :
            $"{s.Name}\nГруппа: {s.GroupName}   Код: {s.Code}\nТип области: {s.Type}\nРазмер: {s.Габариты.X * 304.8:0} × {s.Габариты.Y * 304.8:0} мм";
    }
    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (selectedSections == null)
        { Status.Text = "Выберите вариант для вставки."; return; }
        Cancel = false; DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
