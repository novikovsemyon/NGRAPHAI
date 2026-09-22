using System;
using System.Collections.Generic;

namespace HOVS.Plugin;

public sealed class InstallationTypeClassification
{
    public string Type { get; set; } = "Другая";
    public double Confidence { get; set; }
    public string Evidence { get; set; } = "";
}

/// <summary>
/// Normalizes source installation/system descriptions into the controlled HVAC taxonomy
/// requested for NGrapfAI. The order is intentional: composite/special installation
/// prefixes must be checked before the generic П/В prefixes.
/// </summary>
public static class InstallationTypeClassifier
{
    public static readonly string[] AllTypes =
    {
        "В",    // вытяжка
        "П",    // приточка
        "ПВУ",  // приточно-вытяжная установка
        "ВТЗ",  // воздушно-тепловая завеса
        "АВО",  // агрегат воздушного отопления
        "Другая"
    };

    public static InstallationTypeClassification Classify(string designation, string? rawType)
    {
        var id = Normalize(designation).ToUpperInvariant();
        var text = Normalize(rawType).ToLowerInvariant();

        // Explicit source description wins when it clearly names one of the controlled types.
        // Smoke-control systems are intentionally mapped to "Другая": they are not
        // ordinary supply/exhaust installations for recovery-pair inference.
        if (ContainsAny(text, "противодым", "дымоудал", "дымоудаление", "подпор воздуха", "smoke"))
            return Result("Другая", 0.99, "противодымная система относится к категории 'Другая'");

        if (StartsWithAny(id, "ПД", "ДВ", "ДУ", "PD", "DV", "DU"))
            return Result("Другая", 0.97, "противодымная система определена по префиксу обозначения");

        if (ContainsAny(text, "агрегат воздушного отопления", "воздушного отопления", "аво"))
            return Result("АВО", 0.995, "тип явно указан в исходном XLSX: агрегат воздушного отопления/АВО");

        if (ContainsAny(text, "воздушно-теплов", "воздушно теплов", "тепловая завеса", "тепловой завес", "втз"))
            return Result("ВТЗ", 0.995, "тип явно указан в исходном XLSX: воздушно-тепловая завеса/ВТЗ");

        if (ContainsAny(text, "приточно-вытяж", "приточно вытяж", "пву", "supply-exhaust", "supply exhaust"))
            return Result("ПВУ", 0.99, "тип явно указан в исходном XLSX: приточно-вытяжная установка");

        if (ContainsAny(text, "вытяжная установ", "вытяжн. установ", "вытяжной вент", "exhaust unit"))
            return Result("В", 0.97, "тип определён по явному описанию вытяжной установки");

        if (ContainsAny(text, "приточная установ", "приточн. установ", "приточный вент", "supply unit"))
            return Result("П", 0.97, "тип определён по явному описанию приточной установки");

        // Designation-based classification is very reliable in the expert corpus.
        if (StartsWithAny(id, "ВТЗ", "VTZ"))
            return Result("ВТЗ", 0.985, "тип определён по префиксу обозначения ВТЗ");

        if (StartsWithAny(id, "АВО", "AVO"))
            return Result("АВО", 0.985, "тип определён по префиксу обозначения АВО");

        if (StartsWithAny(id, "ПВУ", "PVU", "ПВ", "PV"))
            return Result("ПВУ", 0.975, "тип определён по префиксу обозначения ПВ/ПВУ");

        if (StartsWithAny(id, "П", "P"))
            return Result("П", 0.94, "тип определён по префиксу обозначения П");

        if (StartsWithAny(id, "В", "V"))
            return Result("В", 0.94, "тип определён по префиксу обозначения В");

        if (!string.IsNullOrWhiteSpace(rawType))
            return Result("Другая", 0.65, "тип не относится к известным категориям; сохранён как 'Другая'");

        return Result("Другая", 0.45, "недостаточно данных для уверенного определения типа установки");
    }

    private static InstallationTypeClassification Result(string type, double confidence, string evidence)
    {
        return new InstallationTypeClassification
        {
            Type = type,
            Confidence = confidence,
            Evidence = evidence
        };
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        foreach (var value in values)
        {
            if (text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool StartsWithAny(string text, params string[] values)
    {
        foreach (var value in values)
        {
            if (text.StartsWith(value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\u00A0", " ")
            .Trim();
    }
}
