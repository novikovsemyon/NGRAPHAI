using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;
using HOVS.Model;

namespace HOVS.Plugin;

public sealed class HovsSheetAnalysis
{
    public string WorksheetName { get; set; } = "";
    public int Score { get; set; }
    public int LastColumn { get; set; }
    public HovsSchema Schema { get; set; } = new HovsSchema();
}

public static class HovsSchemaAnalyzer
{
    private static readonly Regex HeatExchangerSlotRegex =
        new Regex(@"(?:^|\s)(?:то|теплообменник)\s*[-_№#]?\s*(\d+)", RegexOptions.IgnoreCase);

    public static HovsSheetAnalysis AnalyzeSheet(IXLWorksheet worksheet)
    {
        var result = new HovsSheetAnalysis
        {
            WorksheetName = worksheet.Name,
            Schema = new HovsSchema { WorksheetName = worksheet.Name }
        };

        var used = worksheet.RangeUsed();
        if (used == null) return result;

        var firstUsedRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstUsedColumn = used.FirstColumn().ColumnNumber();
        var lastColumn = used.LastColumn().ColumnNumber();
        result.LastColumn = lastColumn;

        var scanLastRow = Math.Min(lastRow, firstUsedRow + 59);
        var scanLastColumn = Math.Min(lastColumn, firstUsedColumn + 119);

        var bestHeaderRow = firstUsedRow;
        var bestHeaderScore = int.MinValue;
        var bestDesignationColumn = 0;

        for (var row = firstUsedRow; row <= scanLastRow; row++)
        {
            var rowScore = 0;
            var rowDesignationColumn = 0;

            for (var col = firstUsedColumn; col <= scanLastColumn; col++)
            {
                var text = Normalize(worksheet.Cell(row, col).GetString());
                int slot;
                var kind = GuessKind(text, out slot);
                rowScore += HeaderWeight(kind);
                if (kind == ColumnSemanticKind.InstallationName && rowDesignationColumn == 0)
                    rowDesignationColumn = col;
            }

            if (rowDesignationColumn > 0) rowScore += 12;

            if (rowScore > bestHeaderScore)
            {
                bestHeaderScore = rowScore;
                bestHeaderRow = row;
                bestDesignationColumn = rowDesignationColumn;
            }
        }

        if (bestHeaderScore < 0) bestHeaderScore = 0;

        var firstDataRow = FindFirstDataRow(
            worksheet,
            bestDesignationColumn,
            bestHeaderRow + 1,
            lastRow);

        if (firstDataRow <= 0)
            firstDataRow = Math.Min(lastRow + 1, bestHeaderRow + 1);

        // Most HOVS templates use one to three header rows. If the first data row
        // is far away (title block, notes), do not treat dozens of rows as headers.
        var lastHeaderRow = Math.Max(
            bestHeaderRow,
            Math.Min(firstDataRow - 1, bestHeaderRow + 3));

        result.Schema.HeaderRow = bestHeaderRow;
        result.Schema.LastHeaderRow = lastHeaderRow;
        result.Schema.FirstDataRow = firstDataRow;

        var nextSlot = new Dictionary<ColumnSemanticKind, int>();
        var repeatedGroupSlots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var col = firstUsedColumn; col <= lastColumn; col++)
        {
            var header = BuildCompositeHeader(worksheet, bestHeaderRow, lastHeaderRow, col);
            int slot;
            var kind = GuessKind(header, out slot);

            if (slot <= 0 && IsRepeatedElement(kind))
            {
                var groupKey = BuildRepeatedGroupKey(kind, header);
                if (!string.IsNullOrWhiteSpace(groupKey) &&
                    repeatedGroupSlots.TryGetValue(groupKey, out var existingSlot))
                {
                    slot = existingSlot;
                }
                else
                {
                    int previous;
                    nextSlot.TryGetValue(kind, out previous);
                    slot = Math.Max(1, previous + 1);
                    nextSlot[kind] = slot;
                    if (!string.IsNullOrWhiteSpace(groupKey))
                        repeatedGroupSlots[groupKey] = slot;
                }
            }
            else if (slot > 0 && IsRepeatedElement(kind))
            {
                int previous;
                nextSlot.TryGetValue(kind, out previous);
                nextSlot[kind] = Math.Max(previous, slot);
                var groupKey = BuildRepeatedGroupKey(kind, header);
                if (!string.IsNullOrWhiteSpace(groupKey))
                    repeatedGroupSlots[groupKey] = slot;
            }

            result.Schema.Columns.Add(new ColumnSemantic
            {
                ColumnNumber = col,
                Header = header,
                Kind = kind,
                Slot = slot,
                Role = InferRole(header)
            });
        }

        // If a designation column was found by the row scan but its composite
        // header became ambiguous, keep the strong structural evidence.
        if (bestDesignationColumn > 0)
        {
            var designation = result.Schema.Columns.FirstOrDefault(x => x.ColumnNumber == bestDesignationColumn);
            if (designation != null)
            {
                designation.Kind = ColumnSemanticKind.InstallationName;
                designation.Slot = 0;
            }
        }

        result.Score = bestHeaderScore +
                       result.Schema.Columns.Count(x => x.Kind != ColumnSemanticKind.Ignore) * 2;
        return result;
    }

    public static string BuildCompositeHeader(
        IXLWorksheet worksheet,
        int headerRow,
        int lastHeaderRow,
        int column)
    {
        var parts = new List<string>();
        for (var row = Math.Max(1, headerRow); row <= Math.Max(headerRow, lastHeaderRow); row++)
        {
            var value = ReadHeaderCellText(worksheet, row, column);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!parts.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                parts.Add(value);
        }

        return string.Join(" / ", parts);
    }

    public static string ReadHeaderCellText(
        IXLWorksheet worksheet,
        int row,
        int column)
    {
        if (worksheet == null || row <= 0 || column <= 0)
            return "";

        var direct = Normalize(worksheet.Cell(row, column).GetString());
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        foreach (var merged in worksheet.MergedRanges)
        {
            var address = merged.RangeAddress;
            if (row < address.FirstAddress.RowNumber ||
                row > address.LastAddress.RowNumber ||
                column < address.FirstAddress.ColumnNumber ||
                column > address.LastAddress.ColumnNumber)
            {
                continue;
            }

            return Normalize(worksheet.Cell(
                address.FirstAddress.RowNumber,
                address.FirstAddress.ColumnNumber).GetString());
        }

        return "";
    }

    public static ColumnSemanticKind GuessKind(string header, out int slot)
    {
        slot = 0;
        var normalized = Normalize(header);
        if (string.IsNullOrWhiteSpace(normalized)) return ColumnSemanticKind.Ignore;
        var n = normalized.ToLowerInvariant();

        if (IsDesignationHeader(n)) return ColumnSemanticKind.InstallationName;

        if (n.IndexOf("помещ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("комнат", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.Room;

        if (n.IndexOf("кол. систем", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("кол систем", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("количество систем", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("кол-во систем", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.SystemCount;

        if ((n == "тип" || n.IndexOf("тип установки", StringComparison.OrdinalIgnoreCase) >= 0 ||
             n.IndexOf("тип (наименование)", StringComparison.OrdinalIgnoreCase) >= 0) &&
            n.IndexOf("фильтр", StringComparison.OrdinalIgnoreCase) < 0)
            return ColumnSemanticKind.InstallationType;

        if (n.IndexOf("наружн", StringComparison.OrdinalIgnoreCase) >= 0 && LooksLikeAirFlowHeader(n))
            return ColumnSemanticKind.OutdoorAirFlow;

        if (n.IndexOf("рециркуляц", StringComparison.OrdinalIgnoreCase) >= 0 && LooksLikeAirFlowHeader(n))
            return ColumnSemanticKind.RecirculationAirFlow;

        if (LooksLikeAirFlowHeader(n)) return ColumnSemanticKind.AirFlow;

        var heatMatch = HeatExchangerSlotRegex.Match(n);
        if (heatMatch.Success)
        {
            int parsed;
            if (int.TryParse(heatMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                slot = parsed;
            return ColumnSemanticKind.HeatExchanger;
        }

        if (n.IndexOf("теплообмен", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("воздухонагрев", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("нагревател", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("калорифер", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("воздухоохлад", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("охладител", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.HeatExchanger;

        if (n.IndexOf("рекуператор", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("теплоутилиз", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.Recuperator;

        if (n.IndexOf("фильтр", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.Filter;

        if (n.IndexOf("вентилятор", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.Fan;

        if (n.IndexOf("увлажнител", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("парогенератор", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.Humidifier;

        if (n.IndexOf("примеч", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("комментар", StringComparison.OrdinalIgnoreCase) >= 0)
            return ColumnSemanticKind.Notes;

        return ColumnSemanticKind.Ignore;
    }

    public static string InferRole(string header)
    {
        var n = Normalize(header).ToLowerInvariant();
        if (n.IndexOf("колич", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n == "кол" || n == "кол." ||
            n.EndsWith("/ кол", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith("/ кол.", StringComparison.OrdinalIgnoreCase) ||
            n.IndexOf("кол-во", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Quantity";
        if (n.IndexOf("мощн", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("квт", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Power";
        if (n.IndexOf("температ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Regex.IsMatch(n, @"(?:^|\W)t\s*[12](?:\W|$)", RegexOptions.IgnoreCase))
            return "Temperature";
        if (n.IndexOf("давлен", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("па", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Pressure";
        if (n.IndexOf("тип", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("наимен", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Type";
        return "Value";
    }

    public static string NormalizeHeader(string value)
    {
        var n = Normalize(value).ToLowerInvariant()
            .Replace('ё', 'е')
            .Replace("³", "3")
            .Replace("^", "");
        n = Regex.Replace(n, @"\s+", " ").Trim();
        return n;
    }

    private static int FindFirstDataRow(
        IXLWorksheet worksheet,
        int designationColumn,
        int startRow,
        int lastRow)
    {
        if (designationColumn <= 0) return 0;

        var stop = Math.Min(lastRow, startRow + 200);
        for (var row = Math.Max(1, startRow); row <= stop; row++)
        {
            var value = Normalize(worksheet.Cell(row, designationColumn).GetString());
            if (LooksLikeDesignation(value)) return row;
        }
        return 0;
    }

    private static bool LooksLikeDesignation(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > 100) return false;
        return Regex.IsMatch(value, @"[A-Za-zА-Яа-яЁё]\s*\d", RegexOptions.IgnoreCase);
    }

    private static bool IsDesignationHeader(string value)
    {
        return value == "обозначение" || value == "система" || value == "установка" ||
               value.IndexOf("обозначение системы", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("обозначение установки", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeAirFlowHeader(string value)
    {
        var n = value.Replace(" ", "").Replace("³", "3").ToLowerInvariant();
        var hasL = Regex.IsMatch(value, @"(^|[^A-Za-zА-Яа-яЁё])l($|[^A-Za-zА-Яа-яЁё])", RegexOptions.IgnoreCase);
        if (hasL) return true;
        return n.IndexOf("расходвоздуха", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("воздухообмен", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("производительностьповоздуху", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string BuildRepeatedGroupKey(ColumnSemanticKind kind, string header)
    {
        if (!IsRepeatedElement(kind)) return "";
        var n = NormalizeHeader(header);
        if (string.IsNullOrWhiteSpace(n)) return "";

        // Composite headers are normally "group / subcolumn". Removing common
        // subcolumn words lets "Нагреватель / Тип" and "Нагреватель / Мощность"
        // become one logical exchanger slot automatically.
        var firstPart = n.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .FirstOrDefault(x => !IsRoleOnlyHeader(x));
        return string.IsNullOrWhiteSpace(firstPart)
            ? ""
            : kind + "|" + firstPart;
    }

    private static bool IsRoleOnlyHeader(string text)
    {
        var n = NormalizeHeader(text);
        return n == "тип" || n == "наименование" || n == "кол" || n == "кол." ||
               n == "количество" || n == "кол-во" || n == "мощность" || n == "квт" ||
               n == "t1" || n == "t2" || n == "температура" || n == "давление" || n == "па";
    }

    private static bool IsRepeatedElement(ColumnSemanticKind kind)
    {
        return kind == ColumnSemanticKind.HeatExchanger ||
               kind == ColumnSemanticKind.Filter ||
               kind == ColumnSemanticKind.Fan ||
               kind == ColumnSemanticKind.Recuperator ||
               kind == ColumnSemanticKind.Humidifier ||
               kind == ColumnSemanticKind.OtherEquipment;
    }

    private static int HeaderWeight(ColumnSemanticKind kind)
    {
        switch (kind)
        {
            case ColumnSemanticKind.InstallationName: return 12;
            case ColumnSemanticKind.AirFlow: return 7;
            case ColumnSemanticKind.Room: return 4;
            case ColumnSemanticKind.InstallationType: return 4;
            case ColumnSemanticKind.SystemCount: return 3;
            case ColumnSemanticKind.HeatExchanger:
            case ColumnSemanticKind.Filter:
            case ColumnSemanticKind.Fan:
            case ColumnSemanticKind.Recuperator:
            case ColumnSemanticKind.Humidifier:
                return 2;
            case ColumnSemanticKind.Notes: return 1;
            default: return 0;
        }
    }

    private static string Normalize(string? value)
    {
        return Regex.Replace((value ?? "").Replace('\u00A0', ' ').Trim(), @"\s+", " ");
    }
}

public static class HovsSchemaProfileStore
{
    private const string FileName = "hovs_schema_profiles_v1.xml";

    public static string Location => Path.Combine(HovsEnvironment.Root, FileName);

    public static bool TryResolve(
        XLWorkbook workbook,
        IReadOnlyList<HovsSheetAnalysis> analyses,
        out HovsSchema? schema,
        out double score)
    {
        schema = null;
        score = 0.0;

        var profiles = Load();
        if (profiles.Count == 0) return false;

        foreach (var profile in profiles)
        {
            foreach (var analysis in analyses)
            {
                var worksheet = workbook.Worksheet(analysis.WorksheetName);
                double currentScore;
                var candidate = BindProfile(profile, worksheet, analysis, out currentScore);
                if (candidate == null || currentScore <= score) continue;
                score = currentScore;
                schema = candidate;
            }
        }

        return schema != null;
    }

    public static void Save(HovsSchema schema, string workbookPath)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));

        var profiles = Load();
        var id = string.IsNullOrWhiteSpace(schema.ProfileId)
            ? Guid.NewGuid().ToString("N")
            : schema.ProfileId;

        var profile = profiles.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            profile = new HovsSchemaProfile
            {
                Id = id,
                CreatedUtc = DateTime.UtcNow
            };
            profiles.Add(profile);
        }

        profile.Name = string.IsNullOrWhiteSpace(schema.ProfileName)
            ? BuildDefaultName(workbookPath, schema.WorksheetName)
            : schema.ProfileName.Trim();
        profile.WorksheetHint = schema.WorksheetName ?? "";
        profile.HeaderRowHint = schema.HeaderRow;
        profile.LastHeaderRowHint = schema.LastHeaderRow;
        profile.FirstDataRowHint = schema.FirstDataRow;
        profile.UpdatedUtc = DateTime.UtcNow;
        profile.Columns.Clear();
        foreach (var column in schema.Columns)
            profile.Columns.Add(column.Clone());

        schema.ProfileId = profile.Id;
        schema.ProfileName = profile.Name;
        schema.ProfileConfidence = 1.0;
        schema.FromProfile = false;

        SaveAll(profiles);
    }

    private static HovsSchema? BindProfile(
        HovsSchemaProfile profile,
        IXLWorksheet worksheet,
        HovsSheetAnalysis analysis,
        out double score)
    {
        score = 0.0;
        var currentColumns = analysis.Schema.Columns.ToList();
        if (currentColumns.Count == 0) return null;

        var mappedProfileColumns = profile.Columns
            .Where(x => x.Kind != ColumnSemanticKind.Ignore)
            .OrderByDescending(SemanticWeight)
            .ThenBy(x => x.ColumnNumber)
            .ToList();

        if (mappedProfileColumns.Count == 0) return null;

        var usedCurrent = new HashSet<int>();
        var rebound = new List<ColumnSemantic>();
        double weightedScore = 0.0;
        double totalWeight = 0.0;

        foreach (var saved in mappedProfileColumns)
        {
            var weight = SemanticWeight(saved);
            totalWeight += weight;

            ColumnSemantic? best = null;
            var bestSimilarity = 0.0;
            foreach (var current in currentColumns)
            {
                if (usedCurrent.Contains(current.ColumnNumber)) continue;

                var similarity = HeaderSimilarity(saved.Header, current.Header);
                if (similarity <= 0.0 && current.ColumnNumber == saved.ColumnNumber)
                    similarity = 0.35;

                // Header duplication is common ("Тип", "Кол."). Position is only
                // a small tie-breaker; header text remains the main evidence.
                var distance = Math.Abs(current.ColumnNumber - saved.ColumnNumber);
                var positionalBonus = Math.Max(0.0, 0.06 - Math.Min(0.06, distance * 0.005));
                var combined = similarity + positionalBonus;

                if (combined > bestSimilarity)
                {
                    bestSimilarity = combined;
                    best = current;
                }
            }

            if (best == null || bestSimilarity < 0.45) continue;

            usedCurrent.Add(best.ColumnNumber);
            weightedScore += Math.Min(1.0, bestSimilarity) * weight;
            rebound.Add(new ColumnSemantic
            {
                ColumnNumber = best.ColumnNumber,
                Header = best.Header,
                Kind = saved.Kind,
                Slot = saved.Slot,
                Role = string.IsNullOrWhiteSpace(saved.Role) ? best.Role : saved.Role
            });
        }

        if (totalWeight <= 0.0) return null;
        score = weightedScore / totalWeight;

        if (string.Equals(profile.WorksheetHint, worksheet.Name, StringComparison.OrdinalIgnoreCase))
            score = Math.Min(1.0, score + 0.04);

        if (!rebound.Any(x => x.Kind == ColumnSemanticKind.InstallationName))
            score *= 0.35;

        // A familiar template may gain a new equipment column. Even if every old
        // mapping still matches perfectly, do not auto-apply the profile and hide
        // that new information from the expert. Strong newly recognized feature
        // columns force one review, unless the same header was explicitly ignored
        // in the saved profile.
        var reboundColumns = new HashSet<int>(rebound.Select(x => x.ColumnNumber));
        var ignoredSavedColumns = profile.Columns
            .Where(x => x.Kind == ColumnSemanticKind.Ignore)
            .ToList();
        var hasNewStrongFeature = currentColumns.Any(current =>
            IsStrongFeature(current.Kind) &&
            !reboundColumns.Contains(current.ColumnNumber) &&
            !ignoredSavedColumns.Any(saved => HeaderSimilarity(saved.Header, current.Header) >= 0.82));
        if (hasNewStrongFeature)
            score = Math.Min(score, 0.88);

        var schema = new HovsSchema
        {
            WorksheetName = worksheet.Name,
            HeaderRow = analysis.Schema.HeaderRow,
            LastHeaderRow = analysis.Schema.LastHeaderRow,
            FirstDataRow = analysis.Schema.FirstDataRow,
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            ProfileConfidence = score,
            FromProfile = true
        };

        // Keep all physical columns in the schema so the wizard can expose new
        // columns that did not exist when the older profile was learned.
        foreach (var current in currentColumns)
        {
            var mapped = rebound.FirstOrDefault(x => x.ColumnNumber == current.ColumnNumber);
            schema.Columns.Add(mapped?.Clone() ?? new ColumnSemantic
            {
                ColumnNumber = current.ColumnNumber,
                Header = current.Header,
                Kind = ColumnSemanticKind.Ignore,
                Slot = 0,
                Role = current.Role
            });
        }

        return schema;
    }

    private static double HeaderSimilarity(string? left, string? right)
    {
        var a = HovsSchemaAnalyzer.NormalizeHeader(left ?? "");
        var b = HovsSchemaAnalyzer.NormalizeHeader(right ?? "");
        if (a.Length == 0 || b.Length == 0) return 0.0;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (a.Contains(b) || b.Contains(a)) return 0.82;

        var at = new HashSet<string>(a.Split(new[] { ' ', '/', ',', ';', ':', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries));
        var bt = new HashSet<string>(b.Split(new[] { ' ', '/', ',', ';', ':', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries));
        if (at.Count == 0 || bt.Count == 0) return 0.0;

        var intersection = at.Count(x => bt.Contains(x));
        var union = at.Union(bt).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private static bool IsStrongFeature(ColumnSemanticKind kind)
    {
        return kind == ColumnSemanticKind.HeatExchanger ||
               kind == ColumnSemanticKind.Filter ||
               kind == ColumnSemanticKind.Fan ||
               kind == ColumnSemanticKind.Recuperator ||
               kind == ColumnSemanticKind.Humidifier;
    }

    private static int SemanticWeight(ColumnSemantic semantic)
    {
        switch (semantic.Kind)
        {
            case ColumnSemanticKind.InstallationName: return 6;
            case ColumnSemanticKind.AirFlow: return 5;
            case ColumnSemanticKind.Room: return 3;
            case ColumnSemanticKind.InstallationType: return 3;
            case ColumnSemanticKind.SystemCount: return 2;
            default: return 1;
        }
    }

    private static List<HovsSchemaProfile> Load()
    {
        try
        {
            if (!File.Exists(Location)) return new List<HovsSchemaProfile>();
            var document = XDocument.Load(Location);
            var root = document.Root;
            if (root == null) return new List<HovsSchemaProfile>();

            var result = new List<HovsSchemaProfile>();
            foreach (var element in root.Elements("Profile"))
            {
                var profile = new HovsSchemaProfile
                {
                    Id = Attr(element, "id"),
                    Name = Attr(element, "name"),
                    WorksheetHint = Attr(element, "worksheet"),
                    HeaderRowHint = IntAttr(element, "headerRow"),
                    LastHeaderRowHint = IntAttr(element, "lastHeaderRow"),
                    FirstDataRowHint = IntAttr(element, "firstDataRow"),
                    CreatedUtc = DateAttr(element, "createdUtc"),
                    UpdatedUtc = DateAttr(element, "updatedUtc")
                };

                foreach (var columnElement in element.Elements("Column"))
                {
                    ColumnSemanticKind kind;
                    if (!Enum.TryParse(Attr(columnElement, "kind"), true, out kind))
                        kind = ColumnSemanticKind.Ignore;

                    profile.Columns.Add(new ColumnSemantic
                    {
                        ColumnNumber = IntAttr(columnElement, "number"),
                        Header = Attr(columnElement, "header"),
                        Kind = kind,
                        Slot = IntAttr(columnElement, "slot"),
                        Role = Attr(columnElement, "role")
                    });
                }

                if (!string.IsNullOrWhiteSpace(profile.Id)) result.Add(profile);
            }

            return result;
        }
        catch
        {
            // A damaged learning file must not block XLSX import. The expert can
            // create a fresh profile and the next save will replace the document.
            return new List<HovsSchemaProfile>();
        }
    }

    private static void SaveAll(IReadOnlyList<HovsSchemaProfile> profiles)
    {
        var directory = Path.GetDirectoryName(Location);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var root = new XElement("HovsSchemaProfiles", new XAttribute("version", "1"));
        foreach (var profile in profiles.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var element = new XElement(
                "Profile",
                new XAttribute("id", profile.Id ?? ""),
                new XAttribute("name", profile.Name ?? ""),
                new XAttribute("worksheet", profile.WorksheetHint ?? ""),
                new XAttribute("headerRow", profile.HeaderRowHint),
                new XAttribute("lastHeaderRow", profile.LastHeaderRowHint),
                new XAttribute("firstDataRow", profile.FirstDataRowHint),
                new XAttribute("createdUtc", profile.CreatedUtc.ToString("o", CultureInfo.InvariantCulture)),
                new XAttribute("updatedUtc", profile.UpdatedUtc.ToString("o", CultureInfo.InvariantCulture)));

            foreach (var column in profile.Columns.OrderBy(x => x.ColumnNumber))
            {
                element.Add(new XElement(
                    "Column",
                    new XAttribute("number", column.ColumnNumber),
                    new XAttribute("header", column.Header ?? ""),
                    new XAttribute("kind", column.Kind.ToString()),
                    new XAttribute("slot", column.Slot),
                    new XAttribute("role", column.Role ?? "")));
            }

            root.Add(element);
        }

        var temp = Location + ".tmp";
        new XDocument(root).Save(temp);
        if (File.Exists(Location)) File.Replace(temp, Location, null);
        else File.Move(temp, Location);
    }

    private static string BuildDefaultName(string workbookPath, string worksheetName)
    {
        var file = Path.GetFileNameWithoutExtension(workbookPath) ?? "ХОВС";
        return file + " — " + (worksheetName ?? "Лист");
    }

    private static string Attr(XElement element, string name)
    {
        return (string?)element.Attribute(name) ?? "";
    }

    private static int IntAttr(XElement element, string name)
    {
        int value;
        return int.TryParse(Attr(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? value
            : 0;
    }

    private static DateTime DateAttr(XElement element, string name)
    {
        DateTime value;
        return DateTime.TryParse(
            Attr(element, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value)
            ? value
            : DateTime.UtcNow;
    }
}
