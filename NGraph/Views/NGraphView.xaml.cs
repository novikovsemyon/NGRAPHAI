using System.Windows.Navigation;
using NGraph.Core;
using NGraph.ViewModels;

namespace NGraph.Views;

public sealed partial class NGraphView
{
    public NGraphView(NGraphViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        
        
    }

    private void Hyperlink_OnRequestNavigate1(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start("https://shsystems.ru");
    }
    private void Hyperlink_OnRequestNavigate2(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start("https://beta-bim.com/");
    }
    
}