using NGraph.Core.FunctionalScheme;

namespace NGraph.ViewModels;

public sealed class NGraphCreateFootorFromBdViewModel : ObservableObject
{
    public string Title { get; set; } = "База данных";
    public string InsertLabel { get; set; } = "Построить";
    public БазаДанных BD { get; set; }
    public List<СекцияБазыДанных> Sections { get; set; }
}
