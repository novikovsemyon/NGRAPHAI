namespace NGraph.ViewModels;

public sealed class NGraphNumberingSheetsViewModel : ObservableObject
{
    public Document Doc { get; set; }
    public IList<ViewSheet> Sheets { get; set; } = new List<ViewSheet>();
}
