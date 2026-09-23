using System.Windows;
namespace NGraph.Views;
public partial class HovsProjectNameWindow
{
    public string ProjectName => NameInput.Text.Trim();
    public HovsProjectNameWindow(string name)
    {
        DialogTheme.Prepare(this); InitializeComponent(); NameInput.Text = name;
        Loaded += (_, _) => { NameInput.Focus(); NameInput.SelectAll(); };
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName)) { ErrorText.Text = "Введите название объекта."; return; }
        DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
