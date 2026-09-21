using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NGraph.ViewModels;

namespace NGraph.Views;

public sealed partial class NGraphNumberingSheetsView
{
    public bool Cancel { get; private set; } = true;
    private readonly IList<ViewSheet> _sheets;

    public NGraphNumberingSheetsView(NGraphNumberingSheetsViewModel viewModel)
    {
        DataContext = viewModel;
        _sheets = viewModel.Sheets;
        InitializeComponent();

        var parameters = _sheets
            .SelectMany(sheet => sheet.Parameters.Cast<Parameter>())
            .Where(parameter => !parameter.IsReadOnly &&
                (parameter.StorageType == StorageType.String || parameter.StorageType == StorageType.Integer))
            .GroupBy(parameter => parameter.Definition.Name)
            .Select(group => group.First())
            .OrderBy(parameter => parameter.Definition.Name)
            .ToList();

        CB_Param.DisplayMemberPath = "Definition.Name";
        CB_Param.ItemsSource = parameters;
        CB_Param.SelectedItem = parameters.FirstOrDefault(parameter =>
            parameter.Definition.Name == "CISP_Номер страницы для выпуска") ?? parameters.FirstOrDefault();
        Confirm.IsEnabled = parameters.Count > 0;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (CB_Param.SelectedItem is not Parameter ||
            !int.TryParse(TB_Value.Text, out var start) || start <= 0 ||
            (long)start + _sheets.Count - 1 > int.MaxValue)
        {
            MessageBox.Show(this, "Выберите параметр и укажите положительный начальный номер в допустимом диапазоне.",
                "NGraph", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Cancel = false;
        Close();
    }

    private void CB_Param_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CB_Param.SelectedItem is not Parameter parameter)
        {
            CB_Value.ItemsSource = null;
            return;
        }

        CB_Value.ItemsSource = _sheets
            .Select(sheet => sheet.LookupParameter(parameter.Definition.Name))
            .Where(value => value is not null)
            .Select(value => value.StorageType == StorageType.String ? value.AsString() : value.AsValueString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .OrderBy(value => value)
            .ToList();
        CB_Value.SelectedIndex = 0;
    }

    private void CB_Value_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    private void TextBox_TextChanged(object sender, TextChangedEventArgs e) { }

    private void TB_Value_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }
}
