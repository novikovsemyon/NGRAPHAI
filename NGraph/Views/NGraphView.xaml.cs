using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using NGraph.Core;
using NGraph.ViewModels;

namespace NGraph.Views;

/// <summary>Окно и PDF используют общий текст инструкции из встроенного Commands.xml.</summary>
public sealed partial class NGraphView
{
    private readonly List<HelpTopic> _topics;
    public NGraphView(NGraphViewModel viewModel)
    {
        DialogTheme.Prepare(this); InitializeComponent();
        HeaderVersion.Text = "NGraph " + ProgramVersion.Current + " · Revit " + viewModel.RevitVersion;
        VersionText.Text = "Версия NGraph: " + ProgramVersion.Current + "\nRevit: " + viewModel.RevitVersion;
        BuildText.Text = "Полная версия сборки: " + ProgramVersion.Full;
        try { _topics = HelpContent.Load(); }
        catch (Exception ex) { _topics = new List<HelpTopic>(); Status.Text = ex.Message; }
        Topics.ItemsSource = _topics; Topics.SelectedIndex = 0;
    }
    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_topics == null || Topics == null) return;
        var current = Topics.SelectedItem;
        var query = Search.Text.Trim();
        var visible = _topics.Where(t => t.SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
        Topics.ItemsSource = visible;
        Topics.SelectedItem = current is HelpTopic topic && visible.Contains(topic) ? topic : visible.FirstOrDefault();
        Status.Text = visible.Count == 0 ? "Ничего не найдено. Измените запрос." : "Разделов: " + visible.Count;
    }
    private void Topic_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Article == null) return;
        var document = new FlowDocument { FontFamily = FontFamily, FontSize = 14, PagePadding = new Thickness(16, 8, 20, 16) };
        Article.Document = document;
        if (Topics.SelectedItem is not HelpTopic topic) return;
        document.Blocks.Add(new Paragraph(new Run(topic.Title)) { FontSize = 23, FontWeight = FontWeights.SemiBold });
        document.Blocks.Add(new Paragraph(new Run(topic.Intro)));
        foreach (var section in topic.Sections)
        {
            document.Blocks.Add(new Paragraph(new Run(section.Title)) { FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 6), KeepWithNext = true });
            foreach (var paragraph in section.Paragraphs) document.Blocks.Add(new Paragraph(new Run(paragraph)) { Margin = new Thickness(0, 0, 0, 9), LineHeight = 22 });
        }
    }
    private void Pdf_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(HelpContent.ExtractPdf()) { UseShellExecute = true }); Status.Text = "PDF-инструкция открыта."; }
        catch (Exception ex) { Status.Text = "Не удалось открыть PDF. " + ex.Message; }
    }
    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try { if (e.Uri.Scheme == Uri.UriSchemeHttps) Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { Status.Text = "Не удалось открыть ссылку. " + ex.Message; }
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(VersionText.Text + "\n" + BuildText.Text); Status.Text = "Сведения о версии скопированы."; }
        catch (System.Runtime.InteropServices.ExternalException) { Status.Text = "Буфер обмена занят. Попробуйте ещё раз."; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
