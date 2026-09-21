namespace NGraph.ViewModels;

/// <summary>Справке передаётся только версия Revit; доступ к документу ей не нужен.</summary>
public sealed class NGraphViewModel : ObservableObject
{
    public string RevitVersion { get; set; } = string.Empty;
}
