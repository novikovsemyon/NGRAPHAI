namespace NGraph.ViewModels;

public sealed class NGraphCreateSxemaByModelViewModel : ObservableObject
{
    public Document Doc { get; }

    public NGraphCreateSxemaByModelViewModel(Document document)
    {
        Doc = document ?? throw new ArgumentNullException(nameof(document));
    }
}
