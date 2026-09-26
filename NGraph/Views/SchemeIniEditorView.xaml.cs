using System.Windows;
using NGraph.Core.ModelSchemes;

namespace NGraph.Views;

public sealed partial class SchemeIniEditorView
{
    private readonly SchemeIniFile _file;
    public SchemeIniEditorView(SchemeIniFile file)
    {
        _file = file; DialogTheme.Prepare(this); InitializeComponent();
        FilePath.Text = file.FilePath; Editor.Text = file.Text;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { _file.Save(Editor.Text); DialogResult = true; }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
