using System.Windows;
using System.Windows.Controls;
using NGraph.Core;

namespace NGraph.Views;

public partial class LineNumberingView
{
    private readonly int _count;
    private bool _ready;
    public LineNumberingParameter? SelectedParameter => ParameterInput.SelectedItem as LineNumberingParameter;
    public LineNumberingOptions? Options { get; private set; }

    public LineNumberingView(IReadOnlyList<LineNumberingParameter> parameters, int count)
    {
        _count = count;
        DialogTheme.Prepare(this);
        InitializeComponent();
        ParameterInput.ItemsSource = parameters;
        ParameterInput.SelectedItem = parameters.FirstOrDefault(p => p.IsDefault);
        SelectionSummary.Text = $"Элементов для нумерации: {count}. Задайте параметр и формат номера.";
        DefaultHint.Text = parameters.Count == 0 ? "У выбранного оборудования нет общих записываемых текстовых параметров."
            : SelectedParameter == null ? "«Имя панели» недоступен для всей выборки. Выберите другой параметр."
            : "По умолчанию: «Имя панели», начальное значение 1, шаг 1.";
        _ready = true;
        UpdatePreview();
        Loaded += (_, _) => { StartInput.Focus(); StartInput.SelectAll(); };
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) { if (_ready) UpdatePreview(); }
    private void Parameter_Changed(object sender, SelectionChangedEventArgs e) { if (_ready) UpdatePreview(); }

    private void UpdatePreview()
    {
        var valid = LineNumberingOptions.TryCreate(StartInput.Text, IncrementInput.Text, PrefixInput.Text,
            PostfixInput.Text, _count, out var options, out var error);
        Options = options;
        if (valid && options != null)
        {
            var examples = Enumerable.Range(0, Math.Min(_count, 3)).Select(options.Format).ToList();
            if (_count > 3) { examples.Add("…"); examples.Add(options.Format(_count - 1)); }
            PreviewText.Text = string.Join("   →   ", examples);
        }
        else PreviewText.Text = "Проверьте начальное значение и шаг.";
        ErrorText.Text = !valid ? error : SelectedParameter == null ? "Выберите параметр для записи." : string.Empty;
        Confirm.IsEnabled = valid && SelectedParameter != null;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
        if (Confirm.IsEnabled) DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
