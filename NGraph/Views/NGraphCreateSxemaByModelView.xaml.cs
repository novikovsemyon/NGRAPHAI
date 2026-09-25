using System.Windows;
using NGraph.ViewModels;

namespace NGraph.Views;

public sealed partial class NGraphCreateSxemaByModelView
{
    private readonly NGraphCreateSxemaByModelViewModel _viewModel;
    public NGraphCreateSxemaByModelView(NGraphCreateSxemaByModelViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        DialogTheme.Prepare(this);
        InitializeComponent();
    }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Refresh();
        if (_viewModel.CanBuild) DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
