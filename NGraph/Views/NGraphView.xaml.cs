using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using NGraph.ViewModels;

namespace NGraph.Views;

/// <summary>Локальная справка доступна без сети; внешние материалы открываются только по нажатию ссылки.</summary>
public sealed partial class NGraphView
{
    public NGraphView(NGraphViewModel viewModel)
    {
        NGraph.Views.DialogTheme.Prepare(this);
        InitializeComponent();
        var assembly = typeof(NGraphView).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString() ?? "не определена";
        VersionText.Text = $"Версия NGraph: {version}\nRevit: {viewModel.RevitVersion}";
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            // UseShellExecute необходим для открытия URL и в .NET Framework, и в современных .NET.
            if (e.Uri.Scheme != Uri.UriSchemeHttps) return;
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
        {
            MessageBox.Show(this, "Не удалось открыть ссылку: " + ex.Message, "NGraph");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // В буфер попадают только версии, без пути проекта и пользовательских данных.
            Clipboard.SetText(VersionText.Text);
            Status.Text = "Сведения о версии скопированы.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Status.Text = "Буфер обмена занят. Попробуйте ещё раз.";
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) e.Handled = true;
    }
}
