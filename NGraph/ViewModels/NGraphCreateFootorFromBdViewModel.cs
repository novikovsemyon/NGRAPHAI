using NGraph.Core.FunctionalScheme;

namespace NGraph.ViewModels;

public sealed class NGraphCreateFootorFromBdViewModel : ObservableObject
{
    public БазаДанных BD { get; set; }
    public List<СекцияБазыДанных> Sections { get; set; }
}
