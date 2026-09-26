using System.Windows;
using NGraph.ViewModels;

namespace NGraph.Views;

public partial class DuctCableView
{
    private readonly DuctCableViewModel _viewModel;
    public DuctCableView(DuctCableViewModel viewModel, string warning = "")
    {
        _viewModel = viewModel;
        DialogTheme.Prepare(this);
        InitializeComponent(); DataContext = viewModel; SettingsWarning.Text = warning;
    }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        CableTable.CommitEdit(); CableTable.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        _viewModel.Refresh(); if (_viewModel.CanWrite) DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
