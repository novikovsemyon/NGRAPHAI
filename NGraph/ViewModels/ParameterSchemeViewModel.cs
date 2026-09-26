using System.ComponentModel;
using System.Runtime.CompilerServices;
using NGraph.Core.ModelSchemes;

namespace NGraph.ViewModels;

/// <summary>Один план используется для предпросмотра и последующего построения.</summary>
public sealed class ParameterSchemeViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<SchemeSourceElement> _elements;
    private readonly Func<IReadOnlyList<SchemeSourceElement>, IReadOnlyList<SchemeSymbolRow>>? _symbolLoader;
    private IReadOnlyList<SchemeSymbolRow> _allSymbols = Array.Empty<SchemeSymbolRow>();
    public IReadOnlyList<SchemeSymbolRow> Symbols { get; private set; } = Array.Empty<SchemeSymbolRow>();
    public string SymbolsTitle => $"Оборудование / INI ({Symbols.Count})";
    public string IniStatus { get; private set; } = "";
    public IReadOnlyList<SchemeParameterChoice> Parameters { get; }
    public IReadOnlyList<SchemeParameterChoice> FilterParameters { get; }
    public IReadOnlyList<SchemeParameterChoice> SectionParameters { get; }
    public IReadOnlyList<SchemeParameterChoice> NumberParameters { get; }
    public IReadOnlyList<SchemeFilterValue> FilterValues { get; private set; } = Array.Empty<SchemeFilterValue>();
    public IReadOnlyList<SchemePolicyChoice> MissingPolicies { get; } = new[] {
        new SchemePolicyChoice(MissingSchemeValuePolicy.SeparateGroup, "Собирать в отдельные группы"),
        new SchemePolicyChoice(MissingSchemeValuePolicy.Exclude, "Исключать такие элементы"),
        new SchemePolicyChoice(MissingSchemeValuePolicy.Stop, "Не строить до заполнения") };
    public string IniDirectory { get; }
    public SchemePlan Plan { get; private set; } = new();
    public string Summary => $"В модели: {_elements.Count}   ·   После фильтра: {Plan.FilteredCount}   ·   В схему: {Plan.Elements.Count}   ·   Исключено: {Plan.SkippedCount}";
    public string GroupsTitle => $"Группы ({Plan.Groups.Count})";
    public string IssuesTitle => $"Пустые поля ({Plan.Issues.Count})";
    public string ValidationText => string.Join(Environment.NewLine, Plan.Errors.Concat(HasIniErrors ? new[] { "Исправьте соответствия на вкладке «Оборудование / INI»." } : Array.Empty<string>()));
    private bool HasIniErrors { get; set; }
    public bool CanBuild => Plan.CanBuild && !HasIniErrors;
    public bool UseModelLevel { get => _levelSource == SchemeLevelSource.Model; set { if (value) SetLevelSource(SchemeLevelSource.Model); } }
    public bool UseParameterLevel { get => _levelSource == SchemeLevelSource.Parameter; set { if (value) SetLevelSource(SchemeLevelSource.Parameter); } }
    public bool CanChooseLevelParameter => UseParameterLevel;

    private SchemeLevelSource _levelSource;
    private SchemeParameterChoice? _filterParameter, _sectionParameter, _groupNameParameter, _groupNumberParameter, _levelParameter;
    private SchemeFilterValue? _filterValue;
    private MissingSchemeValuePolicy _missingValues;
    private string _viewName = "Структурная схема";

    public SchemeParameterChoice? FilterParameter
    {
        get => _filterParameter;
        set
        {
            if (_filterParameter == value) return;
            _filterParameter = value; Notify();
            var values = new List<SchemeFilterValue> { new(null, "Все значения") };
            if (value != null && value.Key.Length > 0)
                values.AddRange(_elements.Select(e => e.Value(value.Key)).Distinct(StringComparer.Ordinal)
                    .OrderBy(v => v, SchemeValueComparer.Instance).Select(v => new SchemeFilterValue(v, v.Length == 0 ? "(Пустое значение)" : v)));
            FilterValues = values; _filterValue = values[0]; Notify(nameof(FilterValues)); Notify(nameof(FilterValue)); Refresh();
        }
    }
    public SchemeFilterValue? FilterValue { get => _filterValue; set { _filterValue = value; Notify(); Refresh(); } }
    public SchemeParameterChoice? SectionParameter { get => _sectionParameter; set { _sectionParameter = value; Notify(); Refresh(); } }
    public SchemeParameterChoice? GroupNameParameter { get => _groupNameParameter; set { _groupNameParameter = value; Notify(); Refresh(); } }
    public SchemeParameterChoice? GroupNumberParameter { get => _groupNumberParameter; set { _groupNumberParameter = value; Notify(); Refresh(); } }
    public SchemeParameterChoice? LevelParameter { get => _levelParameter; set { _levelParameter = value; Notify(); Refresh(); } }
    public MissingSchemeValuePolicy MissingValues { get => _missingValues; set { _missingValues = value; Notify(); Refresh(); } }
    public string ViewName { get => _viewName; set { _viewName = value; Notify(); Refresh(); } }

    public ParameterSchemeViewModel(IReadOnlyList<SchemeSourceElement> elements,
        IReadOnlyList<SchemeParameterChoice> parameters, string iniDirectory,
        Func<IReadOnlyList<SchemeSourceElement>, IReadOnlyList<SchemeSymbolRow>>? symbolLoader = null)
    {
        _elements = elements; Parameters = parameters; IniDirectory = iniDirectory; _symbolLoader = symbolLoader;
        FilterParameters = new[] { new SchemeParameterChoice("", "Без фильтра") }.Concat(parameters).ToList();
        SectionParameters = new[] { new SchemeParameterChoice("", "Без разделения на секции") }.Concat(parameters).ToList();
        NumberParameters = new[] { new SchemeParameterChoice("", "Не использовать номер группы") }.Concat(parameters).ToList();
        var section = parameters.Where(p => p.Name == "ADSK_Номер секции" && !p.IsType).ToList();
        if (section.Count == 0) section = parameters.Where(p => p.Name == "ADSK_Номер секции" && p.IsType).ToList();
        _sectionParameter = section.Count == 1 ? section[0] : SectionParameters[0];
        _groupNumberParameter = NumberParameters[0];
        _groupNameParameter = FindDefault("ADSK_Зона");
        _levelParameter = FindDefault("ADSK_Этаж");
        // Штатное имя Revit — «Комментарии». Учитываем также пользовательский вариант с одной «м».
        FilterParameter = FindDefault("Комментарии", true) ?? FindDefault("Коментарии", true) ?? FilterParameters[0];
        ReloadSymbols();
    }

    private SchemeParameterChoice? FindDefault(string name, bool instanceOnly = false)
    {
        var candidates = Parameters.Where(p => p.Name == name && !p.IsType).ToList();
        if (candidates.Count == 0 && !instanceOnly) candidates = Parameters.Where(p => p.Name == name && p.IsType).ToList();
        return candidates.Count == 1 ? candidates[0] : null; // Не выбираем произвольное одноимённое определение.
    }
    public void ReloadSymbols()
    {
        _allSymbols = _symbolLoader?.Invoke(_elements) ?? Array.Empty<SchemeSymbolRow>();
        Refresh();
    }
    public void InvalidateImages() { foreach (var row in _allSymbols) row.ResetPreview(); }

    public SchemeOptions GetOptions() => new() {
        LevelSource = _levelSource, MissingValues = _missingValues, ViewName = ViewName.Trim(),
        FilterParameterKey = _filterParameter?.Key ?? "", FilterValue = _filterValue?.Value,
        SectionParameterKey = _sectionParameter?.Key ?? "", GroupNameParameterKey = _groupNameParameter?.Key ?? "",
        GroupNumberParameterKey = _groupNumberParameter?.Key ?? "", LevelParameterKey = _levelParameter?.Key ?? "" };

    private void SetLevelSource(SchemeLevelSource source)
    {
        _levelSource = source;
        Notify(nameof(UseModelLevel)); Notify(nameof(UseParameterLevel)); Notify(nameof(CanChooseLevelParameter)); Refresh();
    }
    public void Refresh()
    {
        try { Plan = SchemePlanner.Build(_elements, GetOptions()); }
        catch (Exception ex) { Plan = new SchemePlan(); Plan.Errors.Add("Не удалось подготовить группировку: " + ex.Message); }
        var options = GetOptions();
        Symbols = _allSymbols.Where(s => options.FilterParameterKey.Length == 0 || options.FilterValue == null
            || s.Source.Value(options.FilterParameterKey) == options.FilterValue).ToList();
        var plannedIds = new HashSet<string>(Plan.Elements.Select(e => e.Id));
        HasIniErrors = _symbolLoader != null && Symbols.Any(s => !s.IsValid && plannedIds.Contains(s.Id));
        IniStatus = $"Соответствий: {Symbols.Count(s => s.IsValid)} · Ошибок: {Symbols.Count(s => !s.IsValid)}";
        foreach (var name in new[] { nameof(Plan), nameof(Summary), nameof(GroupsTitle), nameof(IssuesTitle), nameof(ValidationText), nameof(CanBuild), nameof(Symbols), nameof(SymbolsTitle), nameof(IniStatus) }) Notify(name);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class SchemeFilterValue
{
    public string? Value { get; }
    public string Name { get; }
    public SchemeFilterValue(string? value, string name) { Value = value; Name = name; }
}
public sealed class SchemePolicyChoice
{
    public MissingSchemeValuePolicy Value { get; }
    public string Name { get; }
    public SchemePolicyChoice(MissingSchemeValuePolicy value, string name) { Value = value; Name = name; }
}
