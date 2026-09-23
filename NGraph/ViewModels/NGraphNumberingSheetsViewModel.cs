namespace NGraph.ViewModels;

public sealed class NGraphNumberingSheetsViewModel : ObservableObject
{
    public Document Doc { get; }

    public NGraphNumberingSheetsViewModel(Document document)
    {
        Doc = document ?? throw new ArgumentNullException(nameof(document));
    }
    public IList<ViewSheet> Sheets { get; set; } = new List<ViewSheet>();
}
