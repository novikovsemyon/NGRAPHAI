using System;
using System.Collections.Generic;
using System.Linq;

namespace HOVS.Model;

/// <summary>
/// Logical meaning assigned by the expert to one XLSX source column.
/// Several source columns may intentionally have the same Kind + Slot: their
/// values then describe one logical object (for example HeatExchanger #1).
/// </summary>
public enum ColumnSemanticKind
{
    Ignore = 0,
    InstallationName = 1,
    InstallationType = 2,
    Room = 3,
    SystemCount = 4,
    AirFlow = 5,
    OutdoorAirFlow = 6,
    RecirculationAirFlow = 7,
    HeatExchanger = 20,
    Filter = 21,
    Fan = 22,
    Recuperator = 23,
    Humidifier = 24,
    Notes = 30,
    OtherEquipment = 31
}

/// <summary>
/// Mapping of one physical XLSX column to a semantic destination.
/// Slot groups several columns into the same repeated logical element:
/// HeatExchanger/1, HeatExchanger/2, Filter/1, etc.
/// </summary>
public sealed class ColumnSemantic
{
    public int ColumnNumber { get; set; }
    public string Header { get; set; } = "";
    public ColumnSemanticKind Kind { get; set; } = ColumnSemanticKind.Ignore;
    public int Slot { get; set; }

    /// <summary>
    /// Optional source role. It is deliberately free text because templates
    /// can use arbitrary subcolumns: Type, Qty, Power, t1/t2, Pressure, etc.
    /// </summary>
    public string Role { get; set; } = "";

    public string Key => Kind + ":" + Math.Max(0, Slot);

    public ColumnSemantic Clone()
    {
        return new ColumnSemantic
        {
            ColumnNumber = ColumnNumber,
            Header = Header ?? "",
            Kind = Kind,
            Slot = Slot,
            Role = Role ?? ""
        };
    }
}

/// <summary>
/// Runtime schema of one workbook. Exactly one worksheet is selected for HOVS
/// recognition, while each logical element may be assembled from many columns.
/// </summary>
public sealed class HovsSchema
{
    public string WorksheetName { get; set; } = "";
    public int HeaderRow { get; set; }
    public int LastHeaderRow { get; set; }
    public int FirstDataRow { get; set; }
    public List<ColumnSemantic> Columns { get; } = new List<ColumnSemantic>();

    public string ProfileId { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public double ProfileConfidence { get; set; }
    public bool FromProfile { get; set; }

    public IEnumerable<ColumnSemantic> Get(ColumnSemanticKind kind)
    {
        return Columns
            .Where(x => x.Kind == kind)
            .OrderBy(x => x.Slot)
            .ThenBy(x => x.ColumnNumber);
    }

    public IEnumerable<ColumnSemantic> Get(ColumnSemanticKind kind, int slot)
    {
        return Columns
            .Where(x => x.Kind == kind && x.Slot == slot)
            .OrderBy(x => x.ColumnNumber);
    }

    public HovsSchema Clone()
    {
        var result = new HovsSchema
        {
            WorksheetName = WorksheetName ?? "",
            HeaderRow = HeaderRow,
            LastHeaderRow = LastHeaderRow,
            FirstDataRow = FirstDataRow,
            ProfileId = ProfileId ?? "",
            ProfileName = ProfileName ?? "",
            ProfileConfidence = ProfileConfidence,
            FromProfile = FromProfile
        };

        foreach (var column in Columns)
            result.Columns.Add(column.Clone());

        return result;
    }
}

/// <summary>
/// Persisted learned description of a HOVS table format. Column numbers are a
/// fallback only: on another workbook the profile is re-bound primarily by
/// normalized header text, so moved columns can still be recognized.
/// </summary>
public sealed class HovsSchemaProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string WorksheetHint { get; set; } = "";
    public int HeaderRowHint { get; set; }
    public int LastHeaderRowHint { get; set; }
    public int FirstDataRowHint { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<ColumnSemantic> Columns { get; } = new List<ColumnSemantic>();

    public HovsSchema ToSchema()
    {
        var schema = new HovsSchema
        {
            WorksheetName = WorksheetHint ?? "",
            HeaderRow = HeaderRowHint,
            LastHeaderRow = LastHeaderRowHint,
            FirstDataRow = FirstDataRowHint,
            ProfileId = Id ?? "",
            ProfileName = Name ?? "",
            FromProfile = true
        };

        foreach (var column in Columns)
            schema.Columns.Add(column.Clone());

        return schema;
    }
}
