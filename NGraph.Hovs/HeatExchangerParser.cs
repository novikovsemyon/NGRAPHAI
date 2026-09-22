using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HOVS.Plugin;

public sealed class HeatExchangerEntry
{
    public string Function { get; set; } = "";
    public string Medium { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Canonical heat-exchanger model introduced in v58.
/// One installation may have several independent heat exchangers. Their functional
/// type is one of: Нагрев, Охлаждение, Косвенный. The latter is used for the coil of
/// a glycol heat-recovery circuit.
/// </summary>
public static class HeatExchangerParser
{
    public const string RawItemPrefix = "__Feature.HeatExchanger.Item.";

    public static string EncodeRawItem(string function, string rawType, string rawQuantity)
    {
        return Clean(function) + ";;" + Clean(rawType).Replace(";;", "; ") + ";;" + Clean(rawQuantity);
    }

    public static bool TryDecodeRawItem(string text, out HeatExchangerEntry entry)
    {
        entry = new HeatExchangerEntry();
        var p = (text ?? "").Split(new[] { ";;" }, StringSplitOptions.None);
        if (p.Length < 2) return false;

        var function = NormalizeFunction(p[0]);
        if (function.Length == 0) return false;

        var rawType = p.Length > 1 ? p[1] : "";
        var rawQuantity = p.Length > 2 ? p[2] : "";
        entry.Function = function;
        entry.Medium = NormalizeMedium(rawType, function);
        entry.Quantity = ParseQuantity(rawQuantity, 1);
        return true;
    }

    public static string InferFunction(string category, string header, string rawType)
    {
        if (string.Equals(category, "Heater", StringComparison.OrdinalIgnoreCase))
            return "Нагрев";
        if (string.Equals(category, "Cooler", StringComparison.OrdinalIgnoreCase))
            return "Охлаждение";

        var text = (header + " " + rawType).ToLowerInvariant();
        if (text.IndexOf("косвен", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Косвенный";
        if (text.IndexOf("охлажд", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("холод", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Охлаждение";
        if (text.IndexOf("нагрев", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("подогрев", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("калорифер", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Нагрев";

        // Generic exchanger groups sometimes contain only the medium/type. Use
        // strong media clues, but do not classify a generic glycol coil as indirect:
        // glycol also occurs in cooling. Indirect is added only when a glycol
        // recuperator is actually present.
        if (text.IndexOf("электр", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("электро", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("водян", StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf("паровой", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Нагрев";
        if (text.IndexOf("фреон", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Regex.IsMatch(text, @"\bdx\b", RegexOptions.IgnoreCase))
            return "Охлаждение";

        return "";
    }

    public static string NormalizeFunction(string value)
    {
        var n = Clean(value).ToLowerInvariant();
        if (n.IndexOf("нагрев", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("подогрев", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Нагрев";
        if (n.IndexOf("охлажд", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Охлаждение";
        if (n.IndexOf("косвен", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("indirect", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Косвенный";
        return "";
    }

    public static string NormalizeMedium(string value, string function)
    {
        var raw = Clean(value);
        if (raw.Length == 0 || raw == "—" || raw == "-")
            return "Тип не определён";

        var n = raw.ToLowerInvariant();
        if (n.IndexOf("электр", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("электро", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Электрический";
        if (n.IndexOf("водян", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Regex.IsMatch(n, @"(^|\W)вода($|\W)", RegexOptions.IgnoreCase))
            return "Водяной";
        if (n.IndexOf("пар", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Паровой";
        if (n.IndexOf("фреон", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Regex.IsMatch(n, @"\bdx\b", RegexOptions.IgnoreCase) ||
            n.IndexOf("direct expansion", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Фреоновый (DX)";
        if (n.IndexOf("гликол", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("этиленглик", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("пропиленглик", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Гликолевый";

        // Remove a leading functional word if it is redundantly repeated in the
        // source cell: "нагрев водяной" -> "Водяной" where possible.
        raw = Regex.Replace(raw, @"^\s*(?:нагрев|охлаждение|охлажд|косвенный)\s*[:\-]?\s*", "", RegexOptions.IgnoreCase);
        raw = Clean(raw);
        return raw.Length == 0 ? "Тип не определён" : raw;
    }

    public static List<HeatExchangerEntry> BuildFromRawAttributes(
        IDictionary<string, string> attrs,
        string heaterType,
        string heaterQuantity,
        string coolerType,
        string coolerQuantity,
        string recuperatorType,
        string recuperatorQuantity)
    {
        var result = new List<HeatExchangerEntry>();

        foreach (var pair in (attrs ?? new Dictionary<string, string>())
                     .Where(x => x.Key.StartsWith(RawItemPrefix, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            HeatExchangerEntry entry;
            if (TryDecodeRawItem(pair.Value, out entry))
                result.Add(entry);
        }

        if (result.Count == 0)
        {
            AddLegacy(result, "Нагрев", heaterType, heaterQuantity);
            AddLegacy(result, "Охлаждение", coolerType, coolerQuantity);
        }

        // Expert rule: the air-side coil belonging to a glycol recuperation loop is
        // a heat exchanger of functional type "Косвенный". It is separate from any
        // heating/cooling coils already present in the same installation.
        if (IsGlycolRecuperator(recuperatorType) &&
            !result.Any(x => string.Equals(x.Function, "Косвенный", StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(new HeatExchangerEntry
            {
                Function = "Косвенный",
                Medium = "Гликолевый",
                Quantity = ParseQuantity(recuperatorQuantity, 1)
            });
        }

        return Aggregate(result);
    }

    public static List<HeatExchangerEntry> ParseSummary(string summary)
    {
        var result = new List<HeatExchangerEntry>();
        foreach (var rawPart in (summary ?? "").Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var part = Clean(rawPart);
            if (part.Length == 0) continue;

            var match = Regex.Match(
                part,
                @"^(Нагрев|Охлаждение|Косвенный)\s*:\s*(.*?)(?:\s*[×xX]\s*(\d+))?$",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var function = NormalizeFunction(match.Groups[1].Value);
            var medium = NormalizeMedium(match.Groups[2].Value, function);
            var quantity = ParseQuantity(match.Groups[3].Value, 1);
            result.Add(new HeatExchangerEntry
            {
                Function = function,
                Medium = medium,
                Quantity = quantity
            });
        }
        return Aggregate(result);
    }

    public static string Format(IEnumerable<HeatExchangerEntry> entries)
    {
        var list = Aggregate(entries ?? Array.Empty<HeatExchangerEntry>());
        return string.Join("; ", list.Select(x =>
            x.Function + ": " + (string.IsNullOrWhiteSpace(x.Medium) ? "Тип не определён" : x.Medium) +
            " ×" + Math.Max(1, x.Quantity).ToString(CultureInfo.InvariantCulture)));
    }

    public static string NormalizeForSignature(string summary)
    {
        var parsed = ParseSummary(summary);
        if (parsed.Count == 0) return Clean(summary).ToUpperInvariant();
        return Format(parsed).ToUpperInvariant();
    }

    public static bool HasAny(string summary)
    {
        return ParseSummary(summary).Count > 0;
    }

    public static bool HasFunction(string summary, string function)
    {
        var normalized = NormalizeFunction(function);
        return ParseSummary(summary).Any(x =>
            string.Equals(x.Function, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static int GetQuantity(string summary, string function)
    {
        var normalized = NormalizeFunction(function);
        return ParseSummary(summary)
            .Where(x => string.Equals(x.Function, normalized, StringComparison.OrdinalIgnoreCase))
            .Sum(x => Math.Max(1, x.Quantity));
    }

    public static string GetMediums(string summary, string function)
    {
        var normalized = NormalizeFunction(function);
        var values = ParseSummary(summary)
            .Where(x => string.Equals(x.Function, normalized, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Medium)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return values.Count == 0 ? "—" : string.Join(", ", values);
    }

    public static void ApplyToLegacy(
        string summary,
        out string heating,
        out string heatingType,
        out string heatingCount,
        out string cooling,
        out string coolingType,
        out string coolingCount)
    {
        var heatQty = GetQuantity(summary, "Нагрев");
        var coolQty = GetQuantity(summary, "Охлаждение");
        heating = heatQty > 0 ? "Да" : "Нет";
        heatingType = heatQty > 0 ? GetMediums(summary, "Нагрев") : "—";
        heatingCount = heatQty.ToString(CultureInfo.InvariantCulture);
        cooling = coolQty > 0 ? "Да" : "Нет";
        coolingType = coolQty > 0 ? GetMediums(summary, "Охлаждение") : "—";
        coolingCount = coolQty.ToString(CultureInfo.InvariantCulture);
    }

    public static int TotalQuantity(string summary)
    {
        return ParseSummary(summary).Sum(x => Math.Max(1, x.Quantity));
    }

    public static bool IsGlycolRecuperator(string text)
    {
        return (text ?? "").IndexOf("гликол", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string BuildRawFingerprint(IDictionary<string, string> attrs)
    {
        return string.Join("~", (attrs ?? new Dictionary<string, string>())
            .Where(x => x.Key.StartsWith(RawItemPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Value ?? ""));
    }

    private static void AddLegacy(List<HeatExchangerEntry> target, string function, string typeText, string quantityText)
    {
        var media = SplitValues(typeText)
            .Where(x => HasMeaningful(x))
            .Select(x => NormalizeMedium(x, function))
            .ToList();
        if (media.Count == 0) return;

        var total = ParseQuantity(quantityText, media.Count);
        if (media.Count == 1)
        {
            target.Add(new HeatExchangerEntry { Function = function, Medium = media[0], Quantity = Math.Max(1, total) });
            return;
        }

        // In old data the quantities may have been collapsed and therefore lost
        // their one-to-one association. Preserve each distinct medium as at least one
        // physical exchanger, then place any remaining quantity on the first medium.
        foreach (var medium in media)
            target.Add(new HeatExchangerEntry { Function = function, Medium = medium, Quantity = 1 });

        var remainder = total - media.Count;
        if (remainder > 0) target[0].Quantity += remainder;
    }

    private static List<HeatExchangerEntry> Aggregate(IEnumerable<HeatExchangerEntry> entries)
    {
        return entries
            .Where(x => x != null && NormalizeFunction(x.Function).Length > 0)
            .Select(x => new HeatExchangerEntry
            {
                Function = NormalizeFunction(x.Function),
                Medium = NormalizeMedium(x.Medium, x.Function),
                Quantity = Math.Max(1, x.Quantity)
            })
            .GroupBy(x => x.Function + "\u001F" + x.Medium, StringComparer.OrdinalIgnoreCase)
            .Select(g => new HeatExchangerEntry
            {
                Function = g.First().Function,
                Medium = g.First().Medium,
                Quantity = g.Sum(x => Math.Max(1, x.Quantity))
            })
            .OrderBy(x => FunctionOrder(x.Function))
            .ThenBy(x => x.Medium, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int FunctionOrder(string function)
    {
        if (string.Equals(function, "Нагрев", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(function, "Охлаждение", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(function, "Косвенный", StringComparison.OrdinalIgnoreCase)) return 2;
        return 9;
    }

    private static int ParseQuantity(string value, int fallback)
    {
        var match = Regex.Match(value ?? "", @"\d+");
        int quantity;
        if (match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) && quantity > 0)
            return quantity;
        return Math.Max(1, fallback);
    }

    private static IEnumerable<string> SplitValues(string text)
    {
        return (text ?? "")
            .Split(new[] { " | ", "|", ";", ",", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Clean)
            .Where(x => x.Length > 0);
    }

    private static bool HasMeaningful(string value)
    {
        var n = Clean(value);
        return n.Length > 0 && n != "—" && n != "-" && n != "0";
    }

    private static string Clean(string value)
    {
        return Regex.Replace((value ?? "").Trim(), @"\s+", " ");
    }
}
