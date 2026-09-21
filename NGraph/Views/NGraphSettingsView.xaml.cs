using System.Windows;
using System.Windows.Navigation;
using NGraph.ViewModels;
using NGraph.Core;

namespace NGraph.Views;

public sealed partial class NGraphSettingsView
{
    public NGraphSettingsView(NGraphSettingsViewModel viewModel)
    {
        DataContext = viewModel;
        var helpers = new Helpers();
        InitializeComponent();
        TextBlock_Name.Text = viewModel.Doc.Title;

        var GenericAnnotation = helpers.AllElementsOfCategory(viewModel.Doc, BuiltInCategory.OST_GenericAnnotation).ToList(); //Типовые аннотации

        var mmm = GenericAnnotation.ConvertAll(i => i.Name);

        FamilyType.ItemsSource = mmm;
        Family.ItemsSource = mmm;
        Family.SelectedIndex = 0;
        SelectFile_ini.Click += SelectFile_ini_Click;
    }
    private void FamilyType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
     

    }

    private void Family_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
     
    }

    private void SelectFile_ini_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        MessageBox.Show("Файл сохранен");


    }

  
}