using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HOVS.Model;

namespace HOVS.Plugin;

public sealed class NoteEquipmentItem
{
    public string ComponentType { get; set; } = "";
    public string Value { get; set; } = "";
    public int Quantity { get; set; } = 1;

    public string Display
    {
        get
        {
            var text = ComponentType;
            if (Quantity > 0)
                text += " ×" + Quantity.ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(Value))
                text += " (" + Value.Trim() + ")";

            return text;
        }
    }
}

public sealed class NoteEquipmentAnalysis
{
    public string RawNotes { get; set; } = "";
    public string ReserveSummary { get; set; } = "";
    public string ReserveScheme { get; set; } = "";
    public int ReserveFanCount { get; set; }
    public int ReserveMotorCount { get; set; }
    public int ReserveInstallationCount { get; set; }
    public bool ReserveAmbiguous { get; set; }
    public bool HasElectricHeater { get; set; }
    public string AdditionalEquipmentSummary { get; set; } = "";
    public string Evidence { get; set; } = "";
    public List<NoteEquipmentItem> AdditionalEquipment { get; } =
        new List<NoteEquipmentItem>();

    public bool HasReserve =>
        ReserveFanCount > 0 ||
        ReserveMotorCount > 0 ||
        ReserveInstallationCount > 0 ||
        !string.IsNullOrWhiteSpace(ReserveScheme) ||
        ReserveAmbiguous;

    public bool HasAdditionalEquipment => AdditionalEquipment.Count > 0;
}

/// <summary>
/// Parses source XLSX remarks and dedicated reserve fields.
///
/// The expert corpus contains real examples such as:
/// - "Рез. вентилятор";
/// - "С резервным вентилятором n+1";
/// - "Рез. эл. двигателя, циклон";
/// - "2 шумоглушителя. Резерв вентилятора";
/// - "Рез. вентилятор; смесительный узел в комплекте";
/// - "Рез. вентилятор; ТИОН G4+H13 в помещении; эл. нагреватель...";
/// - "Насосы: 2 рабочих + 1 резервный";
/// - "Резерв. Зимний комплект".
///
/// Only explicit equipment phrases are converted into components. Other remark text
/// stays available as RawNotes, so location/regime/free-form comments are not
/// incorrectly treated as installation equipment.
/// </summary>
public static class NotesEquipmentParser
{
    public const string RawNotesKey = "__Notes.Raw";
    public const string ReserveSummaryKey = "__Notes.ReserveSummary";
    public const string ReserveSchemeKey = "__Notes.ReserveScheme";
    public const string ReserveFanCountKey = "__Notes.ReserveFanCount";
    public const string ReserveMotorCountKey = "__Notes.ReserveMotorCount";
    public const string ReserveInstallationCountKey = "__Notes.ReserveInstallationCount";
    public const string ReserveAmbiguousKey = "__Notes.ReserveAmbiguous";
    public const string AdditionalEquipmentKey = "__Notes.AdditionalEquipment";
    public const string EvidenceKey = "__Notes.Evidence";

    private const string SourceCellPrefix = SourceCellCodec.Prefix;

    private sealed class TextSource
    {
        public string Header { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public static NoteEquipmentAnalysis Analyze(IDictionary<string, string> attributes)
    {
        var result = new NoteEquipmentAnalysis();
        if (attributes == null || attributes.Count == 0)
            return result;

        var sources = ExtractRelevantSources(attributes);
        if (sources.Count == 0)
            return result;

        var noteValues = sources
            .Where(x => IsRemarkHeader(x.Header))
            .Select(x => Clean(x.Value))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        result.RawNotes = string.Join(" | ", noteValues);

        var combined = string.Join(
            " | ",
            sources.Select(x => Clean(x.Value))
                   .Where(x => x.Length > 0)
                   .Distinct(StringComparer.OrdinalIgnoreCase));

        if (combined.Length == 0)
            return result;

        ParseReserve(combined, result);
        ParseAdditionalEquipment(combined, result);

        result.ReserveSummary = BuildReserveSummary(result);
        result.AdditionalEquipmentSummary = string.Join(
            "; ",
            result.AdditionalEquipment.Select(x => x.Display));

        var evidence = new List<string>();
        if (result.HasReserve)
            evidence.Add("резерв из XLSX: " + result.ReserveSummary);
        if (result.HasAdditionalEquipment)
            evidence.Add("доп. оборудование из примечания: " + result.AdditionalEquipmentSummary);

        result.Evidence = string.Join("; ", evidence);
        return result;
    }

    public static void ApplyToAttributes(
        IDictionary<string, string> attributes,
        NoteEquipmentAnalysis analysis)
    {
        if (attributes == null || analysis == null)
            return;

        if (!string.IsNullOrWhiteSpace(analysis.RawNotes))
            attributes[RawNotesKey] = analysis.RawNotes;

        if (!string.IsNullOrWhiteSpace(analysis.ReserveSummary))
            attributes[ReserveSummaryKey] = analysis.ReserveSummary;

        if (!string.IsNullOrWhiteSpace(analysis.ReserveScheme))
            attributes[ReserveSchemeKey] = analysis.ReserveScheme;

        if (analysis.ReserveFanCount > 0)
            attributes[ReserveFanCountKey] =
                analysis.ReserveFanCount.ToString(CultureInfo.InvariantCulture);

        if (analysis.ReserveMotorCount > 0)
            attributes[ReserveMotorCountKey] =
                analysis.ReserveMotorCount.ToString(CultureInfo.InvariantCulture);

        if (analysis.ReserveInstallationCount > 0)
            attributes[ReserveInstallationCountKey] =
                analysis.ReserveInstallationCount.ToString(CultureInfo.InvariantCulture);

        if (analysis.ReserveAmbiguous)
            attributes[ReserveAmbiguousKey] = "1";

        if (!string.IsNullOrWhiteSpace(analysis.AdditionalEquipmentSummary))
            attributes[AdditionalEquipmentKey] = analysis.AdditionalEquipmentSummary;

        if (!string.IsNullOrWhiteSpace(analysis.Evidence))
            attributes[EvidenceKey] = analysis.Evidence;
    }

    public static void AddComponents(
        string equipmentId,
        NoteEquipmentAnalysis analysis,
        ICollection<EquipmentComponent> components)
    {
        if (analysis == null || components == null)
            return;

        if (analysis.ReserveFanCount > 0)
        {
            components.Add(new EquipmentComponent(
                equipmentId,
                "Вентилятор резервный",
                "Из примечания XLSX",
                analysis.ReserveFanCount));
        }

        if (analysis.ReserveMotorCount > 0)
        {
            components.Add(new EquipmentComponent(
                equipmentId,
                "Электродвигатель резервный",
                "Из примечания XLSX",
                analysis.ReserveMotorCount));
        }

        if (analysis.ReserveInstallationCount > 0)
        {
            components.Add(new EquipmentComponent(
                equipmentId,
                "Установка резервная",
                "Из примечания XLSX",
                analysis.ReserveInstallationCount));
        }

        foreach (var item in analysis.AdditionalEquipment)
        {
            components.Add(new EquipmentComponent(
                equipmentId,
                item.ComponentType,
                item.Value,
                item.Quantity > 0 ? (int?)item.Quantity : null));
        }
    }

    private static List<TextSource> ExtractRelevantSources(
        IDictionary<string, string> attributes)
    {
        var result = new List<TextSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in attributes)
        {
            if (pair.Key.StartsWith(SourceCellPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var payload = pair.Value ?? "";
                SourceCellCodec.TryDecode(
                    payload,
                    out var header,
                    out var value);

                AddSourceIfRelevant(result, seen, header, value);
                continue;
            }

            if (pair.Key.StartsWith("__", StringComparison.Ordinal))
                continue;

            AddSourceIfRelevant(result, seen, pair.Key, pair.Value);
        }

        return result;
    }

    private static void AddSourceIfRelevant(
        List<TextSource> result,
        HashSet<string> seen,
        string header,
        string? value)
    {
        var cleanHeader = Clean(header);
        var cleanValue = Clean(value);
        if (cleanValue.Length == 0 || !IsRelevantHeader(cleanHeader))
            return;

        var fingerprint = cleanHeader + "\u001E" + cleanValue;
        if (!seen.Add(fingerprint))
            return;

        result.Add(new TextSource
        {
            Header = cleanHeader,
            Value = cleanValue
        });
    }

    private static bool IsRelevantHeader(string header)
    {
        var n = Clean(header).ToLowerInvariant();
        return n.IndexOf("примеч", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("резерв", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("схема работы", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("n+n", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("дополн", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsRemarkHeader(string header)
    {
        return Clean(header).IndexOf(
            "примеч",
            StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ParseReserve(
        string text,
        NoteEquipmentAnalysis result)
    {
        var hasReserveWord =
            Regex.IsMatch(text, @"рез\.?|резерв", RegexOptions.IgnoreCase);

        // Explicit reserve fan.
        if (Regex.IsMatch(
            text,
            @"рез(?:\.|ерв\w*)?\s*(?:вент(?:\.|илят\w*)|вентилят\w*)|" +
            @"вентилят\w*[^.;|]{0,30}резерв",
            RegexOptions.IgnoreCase))
        {
            result.ReserveFanCount = Math.Max(
                result.ReserveFanCount,
                ParseExplicitQuantity(
                    text,
                    @"(?<q>\d+)\s*(?:шт\.?\s*)?(?:резервн\w*\s+)?вентилят\w*",
                    1));
        }

        // Corpus shorthand: "резервный вент./двиг." means fan/motor reserve evidence.
        if (Regex.IsMatch(
            text,
            @"вент\s*\.?\s*/\s*двиг|вентилят\w*\s*/\s*(?:эл\.\s*)?двиг",
            RegexOptions.IgnoreCase))
        {
            result.ReserveFanCount = Math.Max(result.ReserveFanCount, 1);
            result.ReserveMotorCount = Math.Max(result.ReserveMotorCount, 1);
        }

        // Explicit reserve electric motor.
        if (Regex.IsMatch(
            text,
            @"рез(?:\.|ерв\w*)?\s*(?:эл\.?\s*)?(?:двиг\w*|электродвиг\w*)|" +
            @"электродвиг\w*[^.;|]{0,30}резерв",
            RegexOptions.IgnoreCase))
        {
            result.ReserveMotorCount = Math.Max(
                result.ReserveMotorCount,
                ParseExplicitQuantity(
                    text,
                    @"(?<q>\d+)\s*(?:шт\.?\s*)?резервн\w*\s+(?:электро)?двиг\w*",
                    1));
        }

        // Explicit reserve installation.
        if (Regex.IsMatch(
            text,
            @"резерв\w*\s+установк\w*|рез\.\s*установк\w*",
            RegexOptions.IgnoreCase))
        {
            result.ReserveInstallationCount = Math.Max(
                result.ReserveInstallationCount,
                1);
        }

        // Work/reserve schemes: 1раб/1рез, 2раб/1рез, 1 раб. + 1 рез.
        var scheme = Regex.Match(
            text,
            @"(?<work>\d+)\s*раб\w*\.?\s*(?:\+|/)\s*(?<reserve>\d+)\s*рез\w*\.?",
            RegexOptions.IgnoreCase);

        if (scheme.Success)
        {
            var work = ParseInt(scheme.Groups["work"].Value, 0);
            var reserve = ParseInt(scheme.Groups["reserve"].Value, 0);
            if (work > 0 && reserve > 0)
            {
                result.ReserveScheme =
                    work.ToString(CultureInfo.InvariantCulture) +
                    " раб. + " +
                    reserve.ToString(CultureInfo.InvariantCulture) +
                    " рез.";
            }
        }
        else if (Regex.IsMatch(text, @"\bN\s*\+\s*\d+\b", RegexOptions.IgnoreCase))
        {
            var nMatch = Regex.Match(text, @"\bN\s*\+\s*(?<reserve>\d+)\b", RegexOptions.IgnoreCase);
            if (nMatch.Success)
                result.ReserveScheme =
                    "N+" + ParseInt(nMatch.Groups["reserve"].Value, 1)
                        .ToString(CultureInfo.InvariantCulture);
        }

        // If a scheme says "N+1" next to an explicit reserve fan, it confirms one
        // reserve fan but does not multiply the working fan count.
        if (result.ReserveFanCount > 0 &&
            string.Equals(result.ReserveScheme, "N+1", StringComparison.OrdinalIgnoreCase))
        {
            result.ReserveFanCount = Math.Max(result.ReserveFanCount, 1);
        }

        // Generic reserve note cannot safely be converted to a fan/motor/unit.
        if (hasReserveWord &&
            result.ReserveFanCount == 0 &&
            result.ReserveMotorCount == 0 &&
            result.ReserveInstallationCount == 0 &&
            !ContainsSpecificReservePump(text))
        {
            result.ReserveAmbiguous = true;
        }
    }

    private static void ParseAdditionalEquipment(
        string text,
        NoteEquipmentAnalysis result)
    {
        // Silencers.
        var silencers = Regex.Match(
            text,
            @"(?:(?<q>\d+)\s*)?шумоглушител\w*",
            RegexOptions.IgnoreCase);
        if (silencers.Success)
        {
            AddItem(
                result,
                "Шумоглушитель",
                "",
                ParseInt(silencers.Groups["q"].Value, 1));
        }

        // Mixing unit.
        if (Regex.IsMatch(text, @"смесительн\w*\s+узел", RegexOptions.IgnoreCase))
            AddItem(result, "Смесительный узел", "Из примечания XLSX", 1);

        // Cyclone.
        if (Regex.IsMatch(text, @"\bциклон\w*\b", RegexOptions.IgnoreCase))
            AddItem(result, "Циклон", "Из примечания XLSX", 1);

        // Winter kit for outdoor AC equipment.
        if (Regex.IsMatch(text, @"зимн\w*\s+комплект", RegexOptions.IgnoreCase))
            AddItem(result, "Зимний комплект", "Из примечания XLSX", 1);

        // TION / local compact filtration-heating unit.
        var tion = Regex.Match(
            text,
            @"\bТИОН(?:\s+(?<model>[A-ZА-Я0-9+.\-]+))?",
            RegexOptions.IgnoreCase);
        if (tion.Success)
        {
            var model = Clean(tion.Groups["model"].Value);
            AddItem(
                result,
                "ТИОН",
                model.Length > 0 ? model : "Из примечания XLSX",
                1);
        }

        // Explicit electric heater mentioned only in the remarks.
        if (Regex.IsMatch(
            text,
            @"(?:эл\.?|электрическ\w*)\s*нагревател\w*",
            RegexOptions.IgnoreCase))
        {
            result.HasElectricHeater = true;
            AddItem(
                result,
                "Нагреватель",
                "Электрический; из примечания XLSX",
                1);
        }

        // Pump set with an explicit working/reserve scheme.
        var pumps = Regex.Match(
            text,
            @"насос\w*\s*:\s*(?<work>\d+)\s*раб\w*\s*\+\s*(?<reserve>\d+)\s*резерв\w*",
            RegexOptions.IgnoreCase);
        if (pumps.Success)
        {
            var work = ParseInt(pumps.Groups["work"].Value, 0);
            var reserve = ParseInt(pumps.Groups["reserve"].Value, 0);
            var total = Math.Max(1, work + reserve);
            AddItem(
                result,
                "Насос",
                work.ToString(CultureInfo.InvariantCulture) +
                " раб. + " +
                reserve.ToString(CultureInfo.InvariantCulture) +
                " рез.",
                total);
        }

        // Other explicit equipment phrases occurring in the corpus.
        if (Regex.IsMatch(text, @"бытов\w*\s+осушител\w*\s+воздух", RegexOptions.IgnoreCase))
            AddItem(result, "Осушитель воздуха", "Бытовой", 1);
    }

    private static bool ContainsSpecificReservePump(string text)
    {
        return Regex.IsMatch(
            text,
            @"насос\w*[^.;|]{0,40}резерв",
            RegexOptions.IgnoreCase);
    }

    private static string BuildReserveSummary(NoteEquipmentAnalysis result)
    {
        var parts = new List<string>();

        if (result.ReserveFanCount > 0)
            parts.Add(
                "Резервный вентилятор ×" +
                result.ReserveFanCount.ToString(CultureInfo.InvariantCulture));

        if (result.ReserveMotorCount > 0)
            parts.Add(
                "Резервный электродвигатель ×" +
                result.ReserveMotorCount.ToString(CultureInfo.InvariantCulture));

        if (result.ReserveInstallationCount > 0)
            parts.Add(
                "Резервная установка ×" +
                result.ReserveInstallationCount.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(result.ReserveScheme))
            parts.Add("Схема " + result.ReserveScheme);

        if (result.ReserveAmbiguous && parts.Count == 0)
            parts.Add("Резерв предусмотрен; тип оборудования не определён");
        else if (result.ReserveAmbiguous)
            parts.Add("есть дополнительная неоднозначная отметка о резерве");

        return string.Join("; ", parts);
    }

    private static void AddItem(
        NoteEquipmentAnalysis result,
        string componentType,
        string value,
        int quantity)
    {
        quantity = Math.Max(1, quantity);
        var existing = result.AdditionalEquipment.FirstOrDefault(x =>
            string.Equals(x.ComponentType, componentType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Clean(x.Value),
                Clean(value),
                StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Quantity = Math.Max(existing.Quantity, quantity);
            return;
        }

        result.AdditionalEquipment.Add(new NoteEquipmentItem
        {
            ComponentType = componentType,
            Value = value ?? "",
            Quantity = quantity
        });
    }

    private static int ParseExplicitQuantity(
        string text,
        string pattern,
        int fallback)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return fallback;

        return ParseInt(match.Groups["q"].Value, fallback);
    }

    private static int ParseInt(string value, int fallback)
    {
        int number;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : fallback;
    }

    private static string Clean(string? value)
    {
        var text = (value ?? "")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\u00A0", " ")
            .Trim();
        return Regex.Replace(text, @"\s+", " ");
    }
}
