namespace NGraph.ViewModels;

/// <summary>Документ используется только для списка доступных чертёжных видов.</summary>
public sealed class NGraphSettingsViewModel : ObservableObject
{
    public Document Doc { get; }

    public NGraphSettingsViewModel(Document document)
    {
        Doc = document ?? throw new ArgumentNullException(nameof(document));
    }
}
