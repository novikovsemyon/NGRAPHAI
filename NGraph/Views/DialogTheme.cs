using System.Windows;
using System.Windows.Input;
namespace NGraph.Views;
/// <summary>Один ресурс для всех окон плагина. Системные диалоги выбора файла сохраняют стиль Windows.</summary>
public static class DialogTheme
{
    public static void Prepare(Window window)
    {
        window.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri("/NGraph;component/Views/DialogStyles.xaml", UriKind.Relative) });
        window.SetResourceReference(FrameworkElement.StyleProperty, typeof(Window));
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) e.Handled = true; };
    }
}
