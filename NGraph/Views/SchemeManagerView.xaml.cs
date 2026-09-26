using System.Windows;

namespace NGraph.Views;

public enum SchemeBuildMethod { Spaces, Parameters }

/// <summary>Выбирает команду. Не считывает модель и не меняет настройки построителей.</summary>
public sealed partial class SchemeManagerView
{
    public SchemeBuildMethod? SelectedMethod { get; private set; }
    public SchemeManagerView()
    {
        DialogTheme.Prepare(this);
        InitializeComponent();
    }
    private void Spaces_Click(object sender, RoutedEventArgs e) => Select(SchemeBuildMethod.Spaces);
    private void Parameters_Click(object sender, RoutedEventArgs e) => Select(SchemeBuildMethod.Parameters);
    private void Select(SchemeBuildMethod method) { SelectedMethod = method; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
