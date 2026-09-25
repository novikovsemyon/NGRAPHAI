using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NGraph.Core;
using NGraph.Views;

internal static class LineNumberingTests
{
    public static void Run()
    {
        Check(LineNumberingOptions.TryCreate("1", "1", "", "", 3, out var defaults, out _)
            && defaults!.Format(0) == "1" && defaults.Format(2) == "3", "Default sequence");
        Check(LineNumberingOptions.TryCreate("10", "2", "ЩР-", ".А", 6, out var custom, out _)
            && custom!.Format(0) == "ЩР-10.А" && custom.Format(5) == "ЩР-20.А", "Prefix, increment and postfix");
        Check(LineNumberingOptions.TryCreate("0", "-2", " ", " ", 3, out var descending, out _)
            && descending!.Format(2) == " -4 ", "Negative increment, zero start and intentional spaces");
        Check(!LineNumberingOptions.TryCreate("1", "0", "", "", 3, out _, out _), "Reject zero increment");
        foreach (var invalid in new[] { "", "text", "1.5", "1,5", "2147483648" })
            Check(!LineNumberingOptions.TryCreate(invalid, "1", "", "", 3, out _, out _), "Reject invalid/pasted start");
        Check(!LineNumberingOptions.TryCreate("2147483647", "1", "", "", 2, out _, out _), "Detect upper overflow");
        Check(!LineNumberingOptions.TryCreate("-2147483648", "-1", "", "", 2, out _, out _), "Detect lower overflow");
        Check(LineNumberingOptions.TryCreate("2147483647", "1", "", "", 1, out var boundary, out _)
            && boundary!.Format(0) == "2147483647", "No unused increment after last element");
        Check(!LineNumberingOptions.TryCreate("1", "1", "", "", 0, out _, out _), "Empty selection");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            Check(custom!.Format(1) == "ЩР-12.А", "Stable formatting in Russian locale");
        }
        finally { CultureInfo.CurrentCulture = culture; }

        var panel = new LineNumberingParameter("panel", "Имя панели", "Имя панели", true);
        var mark = new LineNumberingParameter("mark", "Марка", "Марка", false);
        var window = new LineNumberingView(new[] { mark, panel }, 6);
        Show(window);
        var confirm = (Button)window.FindName("Confirm");
        var preview = (TextBlock)window.FindName("PreviewText");
        Check(window.SelectedParameter == panel && confirm.IsEnabled, "Prefer panel name to alphabetical first");
        Input(window, "StartInput", "10"); Input(window, "IncrementInput", "2");
        Input(window, "PrefixInput", "ЩР-"); Input(window, "PostfixInput", ".А");
        ((ComboBox)window.FindName("ParameterInput")).SelectedItem = mark;
        Check(preview.Text.Contains("ЩР-10.А") && preview.Text.Contains("ЩР-20.А"), "Live preview includes first and last");
        Input(window, "IncrementInput", "0");
        Check(!confirm.IsEnabled && window.Options == null, "Invalid input cannot be submitted");
        Input(window, "StartInput", "2147483647"); Input(window, "IncrementInput", "1");
        Check(!confirm.IsEnabled, "Overflow cannot be submitted");
        Input(window, "StartInput", "10"); Input(window, "IncrementInput", "2");
        window.Hide();
        window.Dispatcher.BeginInvoke(new Action(() => confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
        Check(window.ShowDialog() == true && window.SelectedParameter == mark && window.Options!.Format(5) == "ЩР-20.А",
            "Modal confirmation returns chosen parameter and format");

        var missingDefault = new LineNumberingView(new[] { mark }, 1); Show(missingDefault);
        Check(missingDefault.SelectedParameter == null && !((Button)missingDefault.FindName("Confirm")).IsEnabled,
            "No silent switch to another parameter when panel name is absent");
        ((ComboBox)missingDefault.FindName("ParameterInput")).SelectedItem = mark;
        Check(((Button)missingDefault.FindName("Confirm")).IsEnabled, "Explicit alternative is accepted");
        missingDefault.Close();
        var empty = new LineNumberingView(Array.Empty<LineNumberingParameter>(), 0); Show(empty);
        Check(!((Button)empty.FindName("Confirm")).IsEnabled, "Empty catalog cannot be submitted"); empty.Close();
        Console.WriteLine("PASS: line numbering defaults, prefix/postfix, increments, range checks, parameter selection, WPF preview and confirmation.");
    }

    private static void Input(LineNumberingView window, string name, string text) => ((TextBox)window.FindName(name)).Text = text;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Show(Window window)
    {
        window.Show(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout();
    }
}
