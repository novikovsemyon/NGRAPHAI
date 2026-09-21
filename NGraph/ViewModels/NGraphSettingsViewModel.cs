namespace NGraph.ViewModels;

/// <summary>Документ используется только для списка доступных чертёжных видов.</summary>
public sealed class NGraphSettingsViewModel : ObservableObject
{
    public Document Doc { get; set; } = null!;
}
