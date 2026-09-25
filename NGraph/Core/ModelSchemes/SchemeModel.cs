namespace NGraph.Core.ModelSchemes;

public enum SchemeLevelSource { Model, Parameter }
public enum MissingSchemeValuePolicy { SeparateGroup, Exclude, Stop }

/// <summary>Источники отбора и группировки. Ключи параметров различают экземпляр и тип.</summary>
public sealed class SchemeOptions
{
    public SchemeLevelSource LevelSource { get; set; }
    public MissingSchemeValuePolicy MissingValues { get; set; }
    public string FilterParameterKey { get; set; } = string.Empty;
    public string? FilterValue { get; set; }
    public string SectionParameterKey { get; set; } = string.Empty;
    public string GroupNameParameterKey { get; set; } = string.Empty;
    public string GroupNumberParameterKey { get; set; } = string.Empty;
    public string LevelParameterKey { get; set; } = string.Empty;
    public string ViewName { get; set; } = "Структурная схема";
}

public sealed class SchemeParameterChoice
{
    public string Key { get; }
    public string Name { get; }
    public bool IsType { get; }
    public int FilledCount { get; }
    public string DisplayName { get; }
    public SchemeParameterChoice(string key, string name, bool isType = false, int filledCount = 0, int total = 0, string? disambiguation = null)
    {
        Key = key; Name = name; IsType = isType; FilledCount = filledCount;
        DisplayName = key.Length == 0 ? name : $"{name}{(disambiguation == null ? "" : " [" + disambiguation + "]")} · {(isType ? "тип" : "экземпляр")} · {filledCount}/{total}";
    }
}

public sealed class SchemeLevel
{
    public string Id { get; }
    public string Name { get; }
    public double ElevationMillimeters { get; }
    public SchemeLevel(string id, string name, double elevationMillimeters)
    { Id = id; Name = name; ElevationMillimeters = elevationMillimeters; }
}

/// <summary>Данные для схемы по параметрам. Уровень модели запрашивается только при выборе этого источника.</summary>
public sealed class SchemeSourceElement
{
    public string Id { get; }
    public string Name { get; }
    public string IniGroup { get; }
    public string Position { get; }
    public string PanelName { get; }
    public string Mark { get; }
    public string ShortName { get; }
    public IReadOnlyDictionary<string, string> Values { get; }
    private readonly Lazy<SchemeLevel?> _level;
    public SchemeLevel? Level => _level.Value;

    public SchemeSourceElement(string id, string name, string iniGroup, string position, string panelName,
        string mark, string shortName, IReadOnlyDictionary<string, string> values,
        Func<SchemeLevel?> level)
    {
        Id = id; Name = name; IniGroup = iniGroup; Position = position; PanelName = panelName;
        Mark = mark; ShortName = shortName; Values = values;
        _level = new Lazy<SchemeLevel?>(level);
    }
    public string Value(string key) => Values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
}

public sealed class SchemePlannedElement
{
    public SchemeSourceElement Source { get; }
    public string Id => Source.Id;
    public string Name => Source.Name;
    public string PanelName => Source.PanelName;
    public string SectionKey { get; }
    public string SectionName { get; }
    public string LevelKey { get; }
    public string LevelName { get; }
    public double? ElevationMillimeters { get; }
    public string GroupName { get; }
    public string GroupNumber { get; }
    public string GroupCaption { get; }
    internal Tuple<string, string, string, string> GroupKey { get; }

    internal SchemePlannedElement(SchemeSourceElement source, string sectionKey, string sectionName,
        string levelKey, string levelName, double? elevation, string groupName,
        string groupNumber, string groupCaption)
    {
        Source = source; SectionKey = sectionKey; SectionName = sectionName; LevelKey = levelKey;
        LevelName = levelName; ElevationMillimeters = elevation; GroupName = groupName;
        GroupNumber = groupNumber; GroupCaption = groupCaption;
        // Не склеиваем строки разделителем: одинаковые имена в разных секциях/уровнях не должны сливаться.
        GroupKey = Tuple.Create(sectionKey, levelKey, groupName, groupNumber);
    }
}

public sealed class SchemePreviewGroup
{
    public IReadOnlyList<SchemePlannedElement> Elements { get; }
    public SchemePlannedElement First => Elements[0];
    public string SectionName => First.SectionName;
    public string LevelName => First.LevelName;
    public string GroupCaption => First.GroupCaption;
    public int Count => Elements.Count;
    internal SchemePreviewGroup(IReadOnlyList<SchemePlannedElement> elements) { Elements = elements; }
}

public sealed class SchemeIssue
{
    public string ElementId { get; }
    public string ElementName { get; }
    public string Message { get; }
    public SchemeIssue(string id, string name, string message) { ElementId = id; ElementName = name; Message = message; }
}

public sealed class SchemePlan
{
    public List<SchemePlannedElement> Elements { get; } = new();
    public List<SchemePreviewGroup> Groups { get; } = new();
    public List<SchemeIssue> Issues { get; } = new();
    public List<string> Errors { get; } = new();
    public int FilteredCount { get; internal set; }
    public int SkippedCount { get; internal set; }
    public bool CanBuild => Elements.Count > 0 && Errors.Count == 0;
}
