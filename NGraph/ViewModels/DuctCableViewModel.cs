using NGraph.Core.CableRouting;

namespace NGraph.ViewModels;

public sealed class DuctCableViewModel : CableNotify
{
    private readonly IReadOnlyList<CableDuctSnapshot> _ducts;
    private CableChoice? _parameter; private CableChoice? _tagView; private CableChoice? _tagType;
    private bool _append; private bool _clearUnused; private bool _createTags; private string _rounding = "1";
    public string ViewName { get; }
    public IReadOnlyList<CableRouteRow> Routes { get; }
    public IReadOnlyList<CableChoice> Parameters { get; }
    public IReadOnlyList<CableChoice> TagViews { get; }
    public IReadOnlyList<CableChoice> TagTypes { get; }
    public IReadOnlyList<CableDuctWrite> Writes { get; private set; } = Array.Empty<CableDuctWrite>();
    public CableChoice? Parameter { get => _parameter; set { _parameter = value; Changed(); Refresh(); } }
    public CableChoice? TagView { get => _tagView; set { _tagView = value; Changed(); Refresh(); } }
    public CableChoice? TagType { get => _tagType; set { _tagType = value; Changed(); Refresh(); } }
    public bool Append { get => _append; set { _append = value; Changed(); Refresh(); } }
    public bool ClearUnused { get => _clearUnused; set { _clearUnused = value; Changed(); Refresh(); } }
    public bool CreateTags { get => _createTags; set { _createTags = value; Changed(); Refresh(); } }
    public string Rounding { get => _rounding; set { _rounding = value; Changed(); Refresh(); } }
    public int RoundingStep { get; private set; } = 1;
    public bool CanWrite { get; private set; }
    public string Error { get; private set; } = "";
    public string Summary { get; private set; } = "";
    public string WriteSummary { get; private set; } = "";
    public string SelectedLength { get; private set; } = "";
    private CableRouteRow? _selectedRoute;
    public CableRouteRow? SelectedRoute { get => _selectedRoute; set { _selectedRoute = value; Changed(); Refresh(); } }

    public DuctCableViewModel(string viewName, IReadOnlyList<CableRouteRow> routes, IReadOnlyList<CableDuctSnapshot> ducts,
        IReadOnlyList<CableChoice> parameters, IReadOnlyList<CableChoice> tagViews, IReadOnlyList<CableChoice> tagTypes, string savedParameter)
    {
        ViewName = viewName; Routes = routes; _ducts = ducts; Parameters = parameters; TagViews = tagViews; TagTypes = tagTypes;
        _parameter = parameters.FirstOrDefault(p => p.Key == savedParameter);
        foreach (var row in routes) row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CableRouteRow.Included)) Refresh(); };
        _selectedRoute = routes.FirstOrDefault(); Refresh();
    }

    public void Refresh()
    {
        var selected = Routes.Where(r => r.Included).ToList();
        var validRounding = int.TryParse(Rounding, out var step) && step > 0 && step <= 1000;
        if (validRounding) RoundingStep = step;
        Writes = Parameter == null ? Array.Empty<CableDuctWrite>() : CableWritePlanner.Plan(Routes, _ducts, Parameter.Key, Append, ClearUnused);
        Error = selected.Count == 0 ? "Выберите кабели для записи." : selected.Any(r => !r.Ready) ? "Исправьте ошибки выбранных кабелей или снимите с них флажки."
            : Parameter == null ? "Выберите текстовый параметр экземпляра воздуховода."
            : !validRounding ? "Шаг округления — целое число метров от 1 до 1000."
            : ClearUnused && (Routes.Any(r => !r.Included || !r.Ready) || Append) ? "Очистка вне маршрутов доступна только для всей схемы без ошибок, в режиме замены."
            : Writes.Any(w => w.Error.Length > 0) ? "На части воздуховодов выбранный параметр недоступен. См. таблицу записи."
            : Writes.Count == 0 ? "Нет воздуховодов для записи."
            : CreateTags && (TagView == null || TagType == null) ? "Выберите план/разрез и тип марки воздуховода." : "";
        try
        {
            foreach (var row in Routes.Where(r => r.Ready)) row.NewLength = CableWritePlanner.RoundedMeters(row.Path!.Meters, RoundingStep).ToString();
            SelectedLength = SelectedRoute?.Path == null ? "" : $"В CJ_Длина кабеля: {CableWritePlanner.RoundedMeters(SelectedRoute.Path.Meters, RoundingStep)} м (вверх, шаг {RoundingStep} м).";
        }
        catch (InvalidOperationException ex) { Error = ex.Message; }
        CanWrite = Error.Length == 0;
        Summary = $"Кабелей: {Routes.Count} · Выбрано: {selected.Count} · С ошибками: {Routes.Count(r => !r.Ready)}";
        WriteSummary = $"Воздуховодов: {Writes.Count} · Изменятся: {Writes.Count(w => w.OldValue != w.NewValue)} · Будут очищены: {Writes.Count(w => w.NewValue.Length == 0 && w.OldValue.Length > 0)}";
        Changed(nameof(Writes)); Changed(nameof(Error)); Changed(nameof(CanWrite)); Changed(nameof(Summary)); Changed(nameof(WriteSummary)); Changed(nameof(SelectedLength));
    }
}
