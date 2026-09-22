using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HOVS.Model;

namespace HOVS.Plugin;

public sealed class InstallationFeatures
{
    // Keep the first 10 fields in their historical order: old training_patterns.tsv
    // stores field indexes and must remain compatible with v43-v49 training data.
    public string Recuperation { get; set; } = "";
    public string RecuperatorType { get; set; } = "";
    public string FilterCount { get; set; } = "";
    public string FilterType { get; set; } = "";
    public string Recirculation { get; set; } = "";
    public string Heating { get; set; } = "";
    public string HeatingType { get; set; } = "";
    public string HeatingCount { get; set; } = "";
    public string Cooling { get; set; } = "";
    public string Humidification { get; set; } = "";

    // v50 fields are appended so old learned field indexes stay valid.
    public string CoolingType { get; set; } = "";
    public string CoolingCount { get; set; } = "";
    public string HumidifierType { get; set; } = "";

    // v53 appends normalized installation type without changing legacy indexes.
    public string InstallationType { get; set; } = "";

    // v58: canonical collection serialized as a readable summary.
    // Example:
    // "Нагрев: Электрический ×1; Нагрев: Водяной ×1; Охлаждение: Фреоновый (DX) ×1".
    // Appended after all legacy fields so old training indexes remain valid.
    public string HeatExchangers { get; set; } = "";

    // v77: exact user-labelled recovery relation. Appended after all historical
    // feature indexes so existing training TSV files stay compatible.
    public string RecoveryPartner { get; set; } = "";
    public string RecoveryPartnerSourceKey { get; set; } = "";
}


public sealed class InstallationFeatureAnalysis
{
    public InstallationFeatures Features { get; set; } = new InstallationFeatures();

    // Independent confidence channels. v53 deliberately separates:
    // 1) whether the row is an installation,
    // 2) installation type / composition extraction,
    // 3) relation confidence (calculated by RecoveryPartnerResolver in the UI).
    public double InstallationConfidence { get; set; }
    public double TypeConfidence { get; set; }
    public double RecuperatorConfidence { get; set; }
    public double FilterConfidence { get; set; }
    public double RecirculationConfidence { get; set; }
    public double HeaterConfidence { get; set; }
    public double CoolerConfidence { get; set; }
    public double HeatExchangerConfidence { get; set; }
    public double HumidifierConfidence { get; set; }

    // Compatibility aggregate used only where older code expects one score.
    public double Confidence { get; set; }
    public string ConfidenceText => Math.Round(Confidence * 100.0).ToString("0") + "%";
    public string Evidence { get; set; } = "";
    public string TypeEvidence { get; set; } = "";
    public string AirflowText { get; set; } = "—";
}

public static class EquipmentFeatureAnalyzer
{
    private const string RecuperatorTypeKey = "__Feature.Recuperator.Type";
    private const string RecuperatorQuantityKey = "__Feature.Recuperator.Quantity";
    private const string FilterTypeKey = "__Feature.Filter.Type";
    private const string FilterQuantityKey = "__Feature.Filter.Quantity";
    private const string HumidifierTypeKey = "__Feature.Humidifier.Type";
    private const string HumidifierQuantityKey = "__Feature.Humidifier.Quantity";
    private const string CoolerTypeKey = "__Feature.Cooler.Type";
    private const string CoolerQuantityKey = "__Feature.Cooler.Quantity";
    private const string HeaterTypeKey = "__Feature.Heater.Type";
    private const string HeaterQuantityKey = "__Feature.Heater.Quantity";
    private const string InstallationAirflowKey = "__Installation.Airflow";
    private const string InstallationConfidenceKey = "__Installation.Confidence";
    private const string InstallationEvidenceKey = "__Installation.Evidence";
    private const string InstallationTypeConfidenceKey = "__Installation.TypeConfidence";
    private const string InstallationTypeEvidenceKey = "__Installation.TypeEvidence";

    public static InstallationFeatures Analyze(Equipment equipment)
    {
        return AnalyzeDetailed(equipment).Features;
    }

    public static InstallationFeatureAnalysis AnalyzeDetailed(Equipment equipment)
    {
        var attrs = equipment.Attributes ?? new Dictionary<string, string>();
        var all = string.Join(" | ", attrs.Select(x => x.Key + ": " + x.Value));

        var recuperatorTypeRaw = Get(attrs, RecuperatorTypeKey);
        var recuperatorQuantityRaw = Get(attrs, RecuperatorQuantityKey);
        var filterTypeRaw = Get(attrs, FilterTypeKey);
        var filterQuantityRaw = Get(attrs, FilterQuantityKey);
        var humidifierTypeRaw = Get(attrs, HumidifierTypeKey);
        var humidifierQuantityRaw = Get(attrs, HumidifierQuantityKey);
        var coolerTypeRaw = Get(attrs, CoolerTypeKey);
        var coolerQuantityRaw = Get(attrs, CoolerQuantityKey);
        var heaterTypeRaw = Get(attrs, HeaterTypeKey);
        var heaterQuantityRaw = Get(attrs, HeaterQuantityKey);

        // Dedicated equipment groups are authoritative. The expert corpus also has
        // "Состав установок" summary columns; use them only as confirmation/fallback,
        // never as a fake detailed group with neighboring Type/Quantity columns.
        if (string.IsNullOrWhiteSpace(recuperatorTypeRaw))
            recuperatorTypeRaw = Get(attrs, "__Summary.Recuperator");
        if (string.IsNullOrWhiteSpace(filterTypeRaw))
            filterTypeRaw = Get(attrs, "__Summary.Filter");
        if (string.IsNullOrWhiteSpace(humidifierTypeRaw))
            humidifierTypeRaw = Get(attrs, "__Summary.Humidifier");
        if (string.IsNullOrWhiteSpace(coolerTypeRaw))
            coolerTypeRaw = Get(attrs, "__Summary.Cooler");
        if (string.IsNullOrWhiteSpace(heaterTypeRaw))
            heaterTypeRaw = Get(attrs, "__Summary.Heater");

        // Text fallback is intentionally restricted to layouts where neither a
        // detailed nor summary group was recognized.
        if (string.IsNullOrWhiteSpace(recuperatorTypeRaw) && !HasAnyFeatureEvidence(attrs, "Recuperator"))
            recuperatorTypeRaw = Find(attrs, all, "рекуператор", "теплоутилизатор", "утилизатор");
        if (string.IsNullOrWhiteSpace(filterTypeRaw) && !HasAnyFeatureEvidence(attrs, "Filter"))
            filterTypeRaw = Find(attrs, all, "фильтр", "filter");
        if (string.IsNullOrWhiteSpace(humidifierTypeRaw) && !HasAnyFeatureEvidence(attrs, "Humidifier"))
            humidifierTypeRaw = Find(attrs, all, "увлажн", "humid");
        if (string.IsNullOrWhiteSpace(coolerTypeRaw) && !HasAnyFeatureEvidence(attrs, "Cooler"))
            coolerTypeRaw = Find(attrs, all, "воздухоохлад", "охладител", "чиллер", "dx", "freon", "фреон");
        if (string.IsNullOrWhiteSpace(heaterTypeRaw) && !HasAnyFeatureEvidence(attrs, "Heater"))
            heaterTypeRaw = Find(attrs, all, "воздухонагрев", "нагревател", "калорифер", "heater");

        var recirculation = Get(attrs, "__Summary.Recirculation");
        if (string.IsNullOrWhiteSpace(recirculation))
            recirculation = Find(attrs, all, "рециркуля", "return air", "смесительн");

        var recuperatorType = RecuperatorKind(recuperatorTypeRaw);
        var heatExchangers = HeatExchangerParser.BuildFromRawAttributes(
            attrs,
            heaterTypeRaw,
            heaterQuantityRaw,
            coolerTypeRaw,
            coolerQuantityRaw,
            recuperatorType,
            recuperatorQuantityRaw);
        var heatExchangerSummary = HeatExchangerParser.Format(heatExchangers);

        string heating;
        string heatingType;
        string heatingCount;
        string cooling;
        string coolingType;
        string coolingCount;
        HeatExchangerParser.ApplyToLegacy(
            heatExchangerSummary,
            out heating,
            out heatingType,
            out heatingCount,
            out cooling,
            out coolingType,
            out coolingCount);

        var result = new InstallationFeatures
        {
            Recuperation = FeaturePresence(recuperatorTypeRaw, recuperatorQuantityRaw),
            RecuperatorType = recuperatorType,
            FilterCount = FilterCount(filterTypeRaw, filterQuantityRaw),
            FilterType = FilterKind(filterTypeRaw),
            Recirculation = Presence(recirculation),
            Heating = heating,
            HeatingType = heatingType,
            HeatingCount = heatingCount,
            Cooling = cooling,
            Humidification = FeaturePresence(humidifierTypeRaw, humidifierQuantityRaw),
            CoolingType = coolingType,
            CoolingCount = coolingCount,
            HumidifierType = HumidifierKind(humidifierTypeRaw),
            InstallationType = equipment.Type ?? "Другая",
            HeatExchangers = heatExchangerSummary
        };

        var evidence = new List<string>();
        var airflowText = Get(attrs, InstallationAirflowKey);
        double airflow;
        var hasAirflow = TryPositiveDouble(airflowText, out airflow);
        var installationConfidence = ParseConfidence(Get(attrs, InstallationConfidenceKey), hasAirflow ? 0.99 : 0.05);
        var typeConfidence = ParseConfidence(Get(attrs, InstallationTypeConfidenceKey),
            string.Equals(result.InstallationType, "Другая", StringComparison.OrdinalIgnoreCase) ? 0.55 : 0.90);
        var typeEvidence = Get(attrs, InstallationTypeEvidenceKey);

        if (hasAirflow)
        {
            var airflowEvidence = Get(attrs, InstallationEvidenceKey);
            evidence.Add(!string.IsNullOrWhiteSpace(airflowEvidence)
                ? airflowEvidence
                : "L=" + airflow.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " м3/ч");
        }
        else
        {
            // Critical expert rule introduced in v52: a candidate without a positive
            // source airflow L is not an installation; confidence must stay < 10%.
            installationConfidence = Math.Min(installationConfidence, 0.09);
            evidence.Add("нет положительного расхода L, м3/ч — вероятность установки <10%");
        }

        var recConfidence = CategoryConfidence(
            attrs, "Recuperator", recuperatorTypeRaw, recuperatorQuantityRaw, result.RecuperatorType);
        var filterConfidence = CategoryConfidence(
            attrs, "Filter", filterTypeRaw, filterQuantityRaw, result.FilterType);
        var coolerConfidence = CategoryConfidence(
            attrs, "Cooler", coolerTypeRaw, coolerQuantityRaw, result.CoolingType);
        var humidifierConfidence = CategoryConfidence(
            attrs, "Humidifier", humidifierTypeRaw, humidifierQuantityRaw, result.HumidifierType);
        var heaterConfidence = CategoryConfidence(
            attrs, "Heater", heaterTypeRaw, heaterQuantityRaw, result.HeatingType);
        var recirculationConfidence = PresenceConfidence(attrs, "Recirculation", recirculation);

        if (HasAnyStructuredEvidence(attrs))
            evidence.Add("структурированные группы оборудования XLSX");
        if (HasAnySummaryEvidence(attrs))
            evidence.Add("проверено по блоку 'Состав установок'");

        // Explicit corrections remain useful for unusual customer templates, but they
        // cannot turn a no-airflow candidate into an installation. Keep a snapshot so
        // confidence can be increased only for categories that learning actually changed.
        var beforeLearning = CloneFeatures(result);
        var patternApplied = TrainingStore.ApplyExplicitPatterns(equipment, result);
        if (patternApplied) evidence.Add("явные пользовательские исправления (шаблон)");

        var explicitCorrection = TrainingStore.TryGetExplicitCorrection(equipment);
        if (explicitCorrection != null)
        {
            OverlayNonEmpty(result, explicitCorrection);
            evidence.Add("точное пользовательское исправление");
        }

        // Keep the new collection and the historical heating/cooling fields in sync.
        // New v58 corrections may edit HeatExchangers directly, while older training
        // data may still edit HeatingType/CoolingType.
        var heatCollectionChangedByLearning =
            !string.Equals(beforeLearning.HeatExchangers, result.HeatExchangers, StringComparison.OrdinalIgnoreCase);
        var legacyHeatChangedByLearning =
            CategoryChanged(beforeLearning, result, "Heater") ||
            CategoryChanged(beforeLearning, result, "Cooler");

        if (heatCollectionChangedByLearning && HeatExchangerParser.HasAny(result.HeatExchangers))
        {
            string syncedHeating;
            string syncedHeatingType;
            string syncedHeatingCount;
            string syncedCooling;
            string syncedCoolingType;
            string syncedCoolingCount;
            HeatExchangerParser.ApplyToLegacy(
                result.HeatExchangers,
                out syncedHeating,
                out syncedHeatingType,
                out syncedHeatingCount,
                out syncedCooling,
                out syncedCoolingType,
                out syncedCoolingCount);
            result.Heating = syncedHeating;
            result.HeatingType = syncedHeatingType;
            result.HeatingCount = syncedHeatingCount;
            result.Cooling = syncedCooling;
            result.CoolingType = syncedCoolingType;
            result.CoolingCount = syncedCoolingCount;
        }
        else if (legacyHeatChangedByLearning)
        {
            var rebuilt = HeatExchangerParser.BuildFromRawAttributes(
                new Dictionary<string, string>(),
                result.HeatingType,
                result.HeatingCount,
                result.CoolingType,
                result.CoolingCount,
                result.RecuperatorType,
                recuperatorQuantityRaw);
            result.HeatExchangers = HeatExchangerParser.Format(rebuilt);
        }

        // If an older correction changed the recuperator to glycolic, the indirect
        // exchanger must still be added even though that correction predates v58.
        if (HeatExchangerParser.IsGlycolRecuperator(result.RecuperatorType) &&
            !HeatExchangerParser.HasFunction(result.HeatExchangers, "Косвенный"))
        {
            var withIndirect = HeatExchangerParser.ParseSummary(result.HeatExchangers);
            withIndirect.Add(new HeatExchangerEntry
            {
                Function = "Косвенный",
                Medium = "Гликолевый",
                Quantity = 1
            });
            result.HeatExchangers = HeatExchangerParser.Format(withIndirect);
        }

        // Learning confidence is applied per category, not globally.
        if (CategoryChanged(beforeLearning, result, "Recuperator")) recConfidence = Math.Max(recConfidence, 0.88);
        if (CategoryChanged(beforeLearning, result, "Filter")) filterConfidence = Math.Max(filterConfidence, 0.88);
        if (CategoryChanged(beforeLearning, result, "Heater")) heaterConfidence = Math.Max(heaterConfidence, 0.88);
        if (CategoryChanged(beforeLearning, result, "Cooler")) coolerConfidence = Math.Max(coolerConfidence, 0.88);
        if (CategoryChanged(beforeLearning, result, "HeatExchanger"))
        {
            heaterConfidence = Math.Max(heaterConfidence, 0.88);
            coolerConfidence = Math.Max(coolerConfidence, 0.88);
        }
        if (CategoryChanged(beforeLearning, result, "Humidifier")) humidifierConfidence = Math.Max(humidifierConfidence, 0.88);
        if (CategoryChanged(beforeLearning, result, "Recirculation")) recirculationConfidence = Math.Max(recirculationConfidence, 0.88);
        if (!string.Equals(beforeLearning.InstallationType, result.InstallationType, StringComparison.OrdinalIgnoreCase))
            typeConfidence = Math.Max(typeConfidence, 0.88);

        if (explicitCorrection != null)
        {
            if (HasCorrection(explicitCorrection.Recuperation, explicitCorrection.RecuperatorType)) recConfidence = 0.99;
            if (HasCorrection(explicitCorrection.FilterCount, explicitCorrection.FilterType)) filterConfidence = 0.99;
            if (HasCorrection(explicitCorrection.Heating, explicitCorrection.HeatingType, explicitCorrection.HeatingCount)) heaterConfidence = 0.99;
            if (HasCorrection(explicitCorrection.Cooling, explicitCorrection.CoolingType, explicitCorrection.CoolingCount)) coolerConfidence = 0.99;
            if (HasCorrection(explicitCorrection.HeatExchangers))
            {
                heaterConfidence = 0.99;
                coolerConfidence = 0.99;
            }
            if (HasCorrection(explicitCorrection.Humidification, explicitCorrection.HumidifierType)) humidifierConfidence = 0.99;
            if (HasCorrection(explicitCorrection.Recirculation)) recirculationConfidence = 0.99;
            if (HasCorrection(explicitCorrection.InstallationType)) typeConfidence = 0.99;
        }

        // v71: project-specific edits are loaded after global training. They belong
        // only to the selected NGrapfAI object/revision and are never written to RVT.
        var projectOverrideApplied = ProjectDataOverrides.ApplyToFeatures(equipment, result);
        if (projectOverrideApplied)
        {
            // A stored revision freezes the state that was visible to the user, but
            // loading it is not equivalent to expert confirmation. Keep the original
            // confidence channels instead of artificially promoting them to 99%.
            evidence.Add("сохранённое состояние ревизии NGrapfAI");
        }

        var featureConfidence =
            recConfidence * 0.21 +
            filterConfidence * 0.21 +
            coolerConfidence * 0.17 +
            humidifierConfidence * 0.13 +
            heaterConfidence * 0.17 +
            recirculationConfidence * 0.11;

        // Aggregate remains for backward compatibility only. UI exposes independent scores.
        var confidence = installationConfidence * 0.45 + typeConfidence * 0.10 + featureConfidence * 0.45;
        if (!hasAirflow)
        {
            installationConfidence = Math.Min(installationConfidence, 0.09);
            confidence = Math.Min(confidence, 0.09);
        }

        confidence = ClampConfidence(confidence);
        installationConfidence = ClampConfidence(installationConfidence);
        typeConfidence = ClampConfidence(typeConfidence);
        recConfidence = ClampConfidence(recConfidence);
        filterConfidence = ClampConfidence(filterConfidence);
        recirculationConfidence = ClampConfidence(recirculationConfidence);
        heaterConfidence = ClampConfidence(heaterConfidence);
        coolerConfidence = ClampConfidence(coolerConfidence);
        humidifierConfidence = ClampConfidence(humidifierConfidence);

        var heatConfidenceValues = new List<double>();
        if (HeatExchangerParser.HasFunction(result.HeatExchangers, "Нагрев"))
            heatConfidenceValues.Add(heaterConfidence);
        if (HeatExchangerParser.HasFunction(result.HeatExchangers, "Охлаждение"))
            heatConfidenceValues.Add(coolerConfidence);
        if (HeatExchangerParser.HasFunction(result.HeatExchangers, "Косвенный"))
            heatConfidenceValues.Add(recConfidence);
        var heatExchangerConfidence = heatConfidenceValues.Count > 0
            ? heatConfidenceValues.Min()
            : Math.Min(heaterConfidence, coolerConfidence);
        heatExchangerConfidence = ClampConfidence(heatExchangerConfidence);

        if (HeatExchangerParser.HasAny(result.HeatExchangers))
            evidence.Add("теплообменники: " + result.HeatExchangers);

        var noteEvidence = Get(attrs, NotesEquipmentParser.EvidenceKey);
        if (!string.IsNullOrWhiteSpace(noteEvidence))
            evidence.Add(noteEvidence);

        return new InstallationFeatureAnalysis
        {
            Features = result,
            Confidence = confidence,
            InstallationConfidence = installationConfidence,
            TypeConfidence = typeConfidence,
            RecuperatorConfidence = recConfidence,
            FilterConfidence = filterConfidence,
            RecirculationConfidence = recirculationConfidence,
            HeaterConfidence = heaterConfidence,
            CoolerConfidence = coolerConfidence,
            HeatExchangerConfidence = heatExchangerConfidence,
            HumidifierConfidence = humidifierConfidence,
            Evidence = evidence.Count > 0 ? string.Join("; ", evidence) : "эвристический анализ",
            TypeEvidence = string.IsNullOrWhiteSpace(typeEvidence) ? "тип определён эвристически" : typeEvidence,
            AirflowText = hasAirflow
                ? airflow.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                : "—"
        };
    }

    private static bool HasFeatureGroup(IDictionary<string, string> attrs, string category)
    {
        return attrs.TryGetValue("__FeatureGroup." + category, out var value) &&
               !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasSummaryGroup(IDictionary<string, string> attrs, string category)
    {
        return attrs.TryGetValue("__SummaryGroup." + category, out var value) &&
               !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasAnyFeatureEvidence(IDictionary<string, string> attrs, string category)
    {
        return HasFeatureGroup(attrs, category) || HasSummaryGroup(attrs, category) ||
               HasMeaningfulValue(Get(attrs, "__Summary." + category));
    }

    private static bool HasAnyStructuredEvidence(IDictionary<string, string> attrs)
    {
        return new[] { "Recuperator", "Filter", "Cooler", "Humidifier", "Heater", "HeatExchanger" }
                   .Any(x => HasFeatureGroup(attrs, x)) ||
               attrs.Keys.Any(x => x.StartsWith(HeatExchangerParser.RawItemPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAnySummaryEvidence(IDictionary<string, string> attrs)
    {
        return attrs.Keys.Any(x => x.StartsWith("__Summary.", StringComparison.OrdinalIgnoreCase));
    }

    private static double CategoryConfidence(
        IDictionary<string, string> attrs,
        string category,
        string typeText,
        string quantityText,
        string recognizedValue)
    {
        var hasDetailedGroup = HasFeatureGroup(attrs, category);
        var hasSummary = HasSummaryGroup(attrs, category) || HasMeaningfulValue(Get(attrs, "__Summary." + category));
        var hasValue = HasMeaningfulValue(typeText) || SumNumbers(quantityText) > 0;

        if (hasDetailedGroup)
        {
            if (!hasValue) return 0.97;
            if (!string.IsNullOrWhiteSpace(recognizedValue) &&
                recognizedValue.IndexOf("не определ", StringComparison.OrdinalIgnoreCase) < 0)
                return 0.99;
            return 0.84;
        }

        if (hasSummary)
        {
            if (!hasValue) return 0.90;
            if (!string.IsNullOrWhiteSpace(recognizedValue) &&
                recognizedValue.IndexOf("не определ", StringComparison.OrdinalIgnoreCase) < 0)
                return 0.92;
            return 0.78;
        }

        if (hasValue)
        {
            if (!string.IsNullOrWhiteSpace(recognizedValue) &&
                recognizedValue.IndexOf("не определ", StringComparison.OrdinalIgnoreCase) < 0)
                return 0.80;
            return 0.65;
        }

        return 0.55;
    }

    private static double PresenceConfidence(
        IDictionary<string, string> attrs,
        string category,
        string rawValue)
    {
        var hasStructured = HasFeatureGroup(attrs, category);
        var hasSummary = HasSummaryGroup(attrs, category) || HasMeaningfulValue(Get(attrs, "__Summary." + category));
        var hasValue = HasMeaningfulValue(rawValue);

        if (hasStructured) return hasValue ? 0.99 : 0.96;
        if (hasSummary) return hasValue ? 0.94 : 0.90;
        if (hasValue) return 0.80;
        return 0.58;
    }

    private static double ClampConfidence(double value)
    {
        if (value < 0.01) return 0.01;
        if (value > 0.99) return 0.99;
        return value;
    }

    private static InstallationFeatures CloneFeatures(InstallationFeatures x)
    {
        return new InstallationFeatures
        {
            Recuperation = x.Recuperation,
            RecuperatorType = x.RecuperatorType,
            FilterCount = x.FilterCount,
            FilterType = x.FilterType,
            Recirculation = x.Recirculation,
            Heating = x.Heating,
            HeatingType = x.HeatingType,
            HeatingCount = x.HeatingCount,
            Cooling = x.Cooling,
            Humidification = x.Humidification,
            CoolingType = x.CoolingType,
            CoolingCount = x.CoolingCount,
            HumidifierType = x.HumidifierType,
            InstallationType = x.InstallationType,
            HeatExchangers = x.HeatExchangers
        };
    }

    private static bool CategoryChanged(InstallationFeatures before, InstallationFeatures after, string category)
    {
        switch (category)
        {
            case "Recuperator":
                return !Same(before.Recuperation, after.Recuperation) || !Same(before.RecuperatorType, after.RecuperatorType);
            case "Filter":
                return !Same(before.FilterCount, after.FilterCount) || !Same(before.FilterType, after.FilterType);
            case "Heater":
                return !Same(before.Heating, after.Heating) || !Same(before.HeatingType, after.HeatingType) || !Same(before.HeatingCount, after.HeatingCount);
            case "Cooler":
                return !Same(before.Cooling, after.Cooling) || !Same(before.CoolingType, after.CoolingType) || !Same(before.CoolingCount, after.CoolingCount);
            case "HeatExchanger":
                return !Same(before.HeatExchangers, after.HeatExchangers);
            case "Humidifier":
                return !Same(before.Humidification, after.Humidification) || !Same(before.HumidifierType, after.HumidifierType);
            case "Recirculation":
                return !Same(before.Recirculation, after.Recirculation);
            default:
                return false;
        }
    }

    private static bool Same(string a, string b)
    {
        return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCorrection(params string[] values)
    {
        return values.Any(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string Get(IDictionary<string, string> attrs, string key)
    {
        return attrs.TryGetValue(key, out var value)
            ? value ?? ""
            : "";
    }

    private static string Find(IDictionary<string, string> attrs, string all, params string[] words)
    {
        foreach (var pair in attrs)
        {
            if (pair.Key.StartsWith("__Source", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.StartsWith("__Raw", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.StartsWith("__FeatureGroup", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.StartsWith("__SummaryGroup", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.StartsWith("__Installation", StringComparison.OrdinalIgnoreCase))
                continue;

            if (words.Any(w => pair.Key.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                return pair.Value ?? "";
            if (words.Any(w => (pair.Value ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                return pair.Value ?? "";
        }
        return "";
    }

    private static string FeaturePresence(string typeText, string quantityText)
    {
        if (HasMeaningfulValue(typeText)) return "Да";
        return SumNumbers(quantityText) > 0 ? "Да" : "Нет";
    }

    private static string Presence(string text)
    {
        return HasMeaningfulValue(text) ? "Да" : "Нет";
    }

    private static bool HasMeaningfulValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var n = Normalize(text).ToLowerInvariant();
        if (n == "0" || n == "-" || n == "—" || n == "нет" || n == "n/a" || n == "#n/a") return false;
        if (n.IndexOf("отсутств", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    private static string RecuperatorKind(string text)
    {
        if (!HasMeaningfulValue(text)) return "—";
        var n = Normalize(text).ToLowerInvariant();
        if (n.IndexOf("ротор", StringComparison.OrdinalIgnoreCase) >= 0) return "Роторный";
        if (n.IndexOf("пластин", StringComparison.OrdinalIgnoreCase) >= 0) return "Пластинчатый";
        if (n.IndexOf("гликол", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("гликолиев", StringComparison.OrdinalIgnoreCase) >= 0) return "Гликолевый";
        if (n.IndexOf("тепловая труба", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("теплотруб", StringComparison.OrdinalIgnoreCase) >= 0) return "Тепловая труба";
        return CleanTypeText(text, "Указан, тип не определён");
    }

    private static string FilterKind(string text)
    {
        if (!HasMeaningfulValue(text)) return "—";
        var tokens = FilterTokens(text, true);
        if (tokens.Count > 0)
        {
            // УФ is useful expert evidence, but it is disinfection, not a mechanical
            // filtration stage. Keep it in the type text without inflating FilterCount.
            if (Regex.IsMatch(text ?? "", @"(?:^|[+;,\s])УФ(?:$|[+;,\s])", RegexOptions.IgnoreCase))
                tokens.Add("УФ");
            return string.Join(", ", tokens.Distinct(StringComparer.OrdinalIgnoreCase));
        }
        return CleanTypeText(text, "Указаны, тип не определён");
    }

    private static string FilterCount(string typeText, string quantityText)
    {
        if (!HasMeaningfulValue(typeText) && SumNumbers(quantityText) <= 0) return "0";
        var stageTokens = FilterTokens(typeText, false);
        if (stageTokens.Count > 0) return stageTokens.Count.ToString();
        var quantity = SumNumbers(quantityText);
        return quantity > 0 ? quantity.ToString() : "1";
    }

    private static List<string> FilterTokens(string text, bool distinct)
    {
        var normalized = Normalize(text).Replace('М', 'M').Replace('м', 'M');
        var result = new List<string>();

        foreach (Match match in Regex.Matches(normalized, @"\b(?:G|F|M|E|H|U)\s*\d{1,2}\b", RegexOptions.IgnoreCase))
            result.Add(Regex.Replace(match.Value.ToUpperInvariant(), @"\s+", ""));

        foreach (Match match in Regex.Matches(normalized, @"\b(?:ISO\s*)?ePM(?:1|2[\.,]5|10)(?:\s*\d{1,3}\s*%)?", RegexOptions.IgnoreCase))
            result.Add(Normalize(match.Value).Replace("2,5", "2.5"));

        foreach (Match match in Regex.Matches(normalized, @"\bISO\s*Coarse(?:\s*\d{1,3}\s*%)?", RegexOptions.IgnoreCase))
            result.Add(Normalize(match.Value));

        return distinct ? result.Distinct(StringComparer.OrdinalIgnoreCase).ToList() : result;
    }

    private static string HeatingKind(string text)
    {
        if (!HasMeaningfulValue(text)) return "—";
        var kinds = new List<string>();
        foreach (var part in SplitValues(text))
        {
            var n = part.ToLowerInvariant();
            if (n.IndexOf("электр", StringComparison.OrdinalIgnoreCase) >= 0 || n == "электро")
                AddUnique(kinds, "Электрический");
            else if (n.IndexOf("водян", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("вода", StringComparison.OrdinalIgnoreCase) >= 0)
                AddUnique(kinds, "Водяной");
            else if (n.IndexOf("пар", StringComparison.OrdinalIgnoreCase) >= 0)
                AddUnique(kinds, "Паровой");
            else if (n.IndexOf("фреон", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("dx", StringComparison.OrdinalIgnoreCase) >= 0)
                AddUnique(kinds, "Фреоновый (DX)");
        }
        return kinds.Count > 0 ? string.Join(", ", kinds) : CleanTypeText(text, "Указан, тип не определён");
    }

    private static string CoolingKind(string text)
    {
        if (!HasMeaningfulValue(text)) return "—";
        var kinds = new List<string>();
        foreach (var part in SplitValues(text))
        {
            var n = part.ToLowerInvariant();
            if (n.IndexOf("фреон", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("dx", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("direct expansion", StringComparison.OrdinalIgnoreCase) >= 0)
                AddUnique(kinds, "Фреоновый (DX)");
            else if (n.IndexOf("водян", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("вода", StringComparison.OrdinalIgnoreCase) >= 0)
                AddUnique(kinds, "Водяной");
            else if (n.IndexOf("гликол", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("этиленглик", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("пропиленглик", StringComparison.OrdinalIgnoreCase) >= 0)
                AddUnique(kinds, "Гликолевый");
        }
        return kinds.Count > 0 ? string.Join(", ", kinds) : CleanTypeText(text, "Указан, тип не определён");
    }

    private static string HumidifierKind(string text)
    {
        if (!HasMeaningfulValue(text)) return "—";
        var n = Normalize(text).ToLowerInvariant();
        if (n.IndexOf("адиабат", StringComparison.OrdinalIgnoreCase) >= 0) return "Адиабатический";
        if (n.IndexOf("изотерм", StringComparison.OrdinalIgnoreCase) >= 0) return "Изотермический";
        if (n.IndexOf("электрод", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("погружн", StringComparison.OrdinalIgnoreCase) >= 0) return "Электродный";
        if (n.IndexOf("пароувлаж", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("паровой", StringComparison.OrdinalIgnoreCase) >= 0) return "Паровой";
        if (n.IndexOf("ультразв", StringComparison.OrdinalIgnoreCase) >= 0) return "Ультразвуковой";
        return CleanTypeText(text, "Указан, тип не определён");
    }

    private static string FeatureCount(string typeText, string quantityText)
    {
        var quantity = SumNumbers(quantityText);
        if (quantity > 0) return quantity.ToString();
        if (!HasMeaningfulValue(typeText)) return "0";
        var parts = SplitValues(typeText).Where(HasMeaningfulValue).ToList();
        return parts.Count > 0 ? parts.Count.ToString() : "1";
    }

    private static int SumNumbers(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var sum = 0;
        foreach (Match match in Regex.Matches(text, @"(?<![A-Za-zА-Яа-яЁё])\d+(?![A-Za-zА-Яа-яЁё])"))
        {
            int value;
            if (int.TryParse(match.Value, out value)) sum += value;
        }
        return sum;
    }

    private static IEnumerable<string> SplitValues(string text)
    {
        return (text ?? "")
            .Split(new[] { '|', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(x => x.Length > 0);
    }

    private static string CleanTypeText(string text, string fallback)
    {
        var parts = SplitValues(text)
            .Select(x => Regex.Replace(x, @"^(?:рекуператор|теплоутилизатор|фильтр|увлажнитель|воздухоохладитель|охладитель|воздухонагреватель)\s*[:\-]?\s*", "", RegexOptions.IgnoreCase))
            .Select(x => Regex.Replace(x, @"(?<![A-Za-zА-Яа-яЁё0-9])(?:ПВ|PV|П|В|P|V)[A-Za-zА-Яа-яЁё0-9]*\d[A-Za-zА-Яа-яЁё0-9]*(?:[.\-_][A-Za-zА-Яа-яЁё0-9]+)*", "", RegexOptions.IgnoreCase))
            .Select(Normalize)
            .Where(HasMeaningfulValue)
            .Where(x => !IsPresenceOnlyText(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return parts.Count > 0 ? string.Join(", ", parts) : fallback;
    }

    private static bool IsPresenceOnlyText(string text)
    {
        var n = Normalize(text).ToLowerInvariant();
        if (n == "да" || n == "есть" || n == "yes" || n == "+") return true;
        if (Regex.IsMatch(n, @"^да\s*(?:x|х)\s*\d+$", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    private static bool TryPositiveDouble(string text, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Trim().Replace(',', '.');
        double parsed;
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
            return false;
        if (parsed <= 0.0) return false;
        value = parsed;
        return true;
    }

    private static double ParseConfidence(string text, double fallback)
    {
        var normalized = (text ?? "").Trim().Replace(',', '.');
        double value;
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
            return fallback;
        if (value < 0.0) return 0.0;
        if (value > 1.0) return 1.0;
        return value;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
            values.Add(value);
    }

    private static void OverlayNonEmpty(InstallationFeatures target, InstallationFeatures learned)
    {
        if (!string.IsNullOrWhiteSpace(learned.Recuperation)) target.Recuperation = learned.Recuperation;
        if (!string.IsNullOrWhiteSpace(learned.RecuperatorType)) target.RecuperatorType = learned.RecuperatorType;
        if (!string.IsNullOrWhiteSpace(learned.FilterCount)) target.FilterCount = learned.FilterCount;
        if (!string.IsNullOrWhiteSpace(learned.FilterType)) target.FilterType = learned.FilterType;
        if (!string.IsNullOrWhiteSpace(learned.Recirculation)) target.Recirculation = learned.Recirculation;
        if (!string.IsNullOrWhiteSpace(learned.Heating)) target.Heating = learned.Heating;
        if (!string.IsNullOrWhiteSpace(learned.HeatingType)) target.HeatingType = learned.HeatingType;
        if (!string.IsNullOrWhiteSpace(learned.HeatingCount)) target.HeatingCount = learned.HeatingCount;
        if (!string.IsNullOrWhiteSpace(learned.Cooling)) target.Cooling = learned.Cooling;
        if (!string.IsNullOrWhiteSpace(learned.Humidification)) target.Humidification = learned.Humidification;
        if (!string.IsNullOrWhiteSpace(learned.CoolingType)) target.CoolingType = learned.CoolingType;
        if (!string.IsNullOrWhiteSpace(learned.CoolingCount)) target.CoolingCount = learned.CoolingCount;
        if (!string.IsNullOrWhiteSpace(learned.HumidifierType)) target.HumidifierType = learned.HumidifierType;
        if (!string.IsNullOrWhiteSpace(learned.InstallationType)) target.InstallationType = learned.InstallationType;
        if (!string.IsNullOrWhiteSpace(learned.HeatExchangers)) target.HeatExchangers = learned.HeatExchangers;
        if (!string.IsNullOrWhiteSpace(learned.RecoveryPartner)) target.RecoveryPartner = learned.RecoveryPartner;
        if (!string.IsNullOrWhiteSpace(learned.RecoveryPartnerSourceKey)) target.RecoveryPartnerSourceKey = learned.RecoveryPartnerSourceKey;
    }

    public static string Normalize(string value)
    {
        return Regex.Replace((value ?? "").Replace('\u00A0', ' ').Trim(), @"\s+", " ");
    }
}

public static class TrainingStore
{
    // Sparse corrections use empty text as "not changed". This marker means the
    // user explicitly confirmed that no linked installation exists.
    public const string NoRecoveryPartnerMarker = "<нет связи>";

    private static readonly object Sync = new object();
    private static string Folder => HovsEnvironment.Root;
    private static string ExactFile => Path.Combine(Folder, "training_exact.tsv");
    private static string PatternFile => Path.Combine(Folder, "training_patterns.tsv");

    // v51 uses a clean training stream. Legacy v43-v50 files are intentionally
    // preserved in the same folder, but they are no longer allowed to override
    // structured XLSX extraction because old versions saved untouched predictions.
    private static string ExplicitCorrectionFile => Path.Combine(Folder, "training_corrections_v2.tsv");
    private static string ExplicitPatternFile => Path.Combine(Folder, "training_patterns_v2.tsv");

    public static InstallationFeatures? TryGetExplicitCorrection(Equipment equipment)
    {
        lock (Sync)
        {
            if (!File.Exists(ExplicitCorrectionFile)) return null;

            var key = Signature(equipment);
            var legacyV76Key = SignatureV76(equipment);
            var legacyV57Key = SignatureV57(equipment);
            var legacyKey = SignatureV52(equipment);
            var lines = File.ReadAllLines(ExplicitCorrectionFile, Encoding.UTF8);
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                var p = lines[index].Split('\t');
                if ((p.Length == 15 || p.Length == 16 || p.Length == 17 ||
                     p.Length == 18 || p.Length == 19) &&
                    (p[0] == key || p[0] == legacyV76Key ||
                     p[0] == legacyV57Key || p[0] == legacyKey))
                    return FromFields(p.Skip(2).ToArray());
            }

            return null;
        }
    }

    public static int SaveExplicitCorrection(Equipment equipment, InstallationFeatures correction)
    {
        lock (Sync)
        {
            var fields = ToFields(correction);
            var changed = fields.Count(x => !string.IsNullOrWhiteSpace(Unescape(x)));
            if (changed == 0) return 0;

            Directory.CreateDirectory(Folder);

            File.AppendAllText(
                ExplicitCorrectionFile,
                Signature(equipment) + "\t" + DateTime.UtcNow.ToString("O") + "\t" +
                string.Join("\t", fields) + Environment.NewLine,
                new UTF8Encoding(false));

            var tokens = (equipment.Attributes ?? new Dictionary<string, string>())
                .Where(x =>
                    !x.Key.StartsWith(ProjectDataOverrides.Prefix, StringComparison.OrdinalIgnoreCase) &&
                    !x.Key.StartsWith("__Source", StringComparison.OrdinalIgnoreCase) &&
                    !x.Key.StartsWith("__Raw", StringComparison.OrdinalIgnoreCase) &&
                    !x.Key.StartsWith("__DesignationOccurrence", StringComparison.OrdinalIgnoreCase))
                .Select(x => EquipmentFeatureAnalyzer.Normalize(x.Key + " " + x.Value))
                .Where(x => x.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var field in fields.Select((v, i) => new { i, v = Unescape(v) }))
            {
                if (string.IsNullOrWhiteSpace(field.v)) continue;

                // Do not generalize a literal partner ID through the generic token
                // learner. Relation corrections remain exact labelled examples.
                if (field.i >= 15) continue;

                foreach (var token in tokens)
                {
                    File.AppendAllText(
                        ExplicitPatternFile,
                        Escape(token) + "\t" + field.i + "\t" + Escape(field.v) + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }

            return changed;
        }
    }

    public static bool ApplyExplicitPatterns(Equipment equipment, InstallationFeatures result)
    {
        lock (Sync)
        {
            if (!File.Exists(ExplicitPatternFile)) return false;

            var attrs = equipment.Attributes ?? new Dictionary<string, string>();
            var tokens = attrs
                .Where(x =>
                    !x.Key.StartsWith(ProjectDataOverrides.Prefix, StringComparison.OrdinalIgnoreCase) &&
                    !x.Key.StartsWith("__Source", StringComparison.OrdinalIgnoreCase) &&
                    !x.Key.StartsWith("__Raw", StringComparison.OrdinalIgnoreCase) &&
                    !x.Key.StartsWith("__DesignationOccurrence", StringComparison.OrdinalIgnoreCase))
                .Select(x => EquipmentFeatureAnalyzer.Normalize(x.Key + " " + x.Value))
                .Where(x => x.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tokens.Count == 0) return false;

            var votes = new Dictionary<int, Dictionary<string, int>>();
            foreach (var line in File.ReadAllLines(ExplicitPatternFile, Encoding.UTF8))
            {
                var p = line.Split('\t');
                int fieldIndex;
                if (p.Length != 3 || !int.TryParse(p[1], out fieldIndex)) continue;

                var token = Unescape(p[0]);
                if (!tokens.Any(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var value = Unescape(p[2]);
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (!votes.TryGetValue(fieldIndex, out var bucket) || bucket == null)
                {
                    bucket = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    votes[fieldIndex] = bucket;
                }

                int count;
                bucket[value] = bucket.TryGetValue(value, out count) ? count + 1 : 1;
            }

            if (votes.Count == 0) return false;

            var current = ToFields(result).Select(Unescape).ToArray();
            var applied = false;

            foreach (var vote in votes)
            {
                if (vote.Key < 0 || vote.Key >= current.Length) continue;
                if (!CanPatternOverride(attrs, vote.Key, current[vote.Key])) continue;

                var best = vote.Value
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (best.Value <= 0) continue;
                current[vote.Key] = best.Key;
                applied = true;
            }

            if (applied)
                Copy(current.Select(Escape).ToArray(), result);

            return applied;
        }
    }

    private static bool TrainingHasFeatureGroup(
        IDictionary<string, string> attrs,
        string category)
    {
        return attrs.TryGetValue("__FeatureGroup." + category, out var value) &&
               !string.IsNullOrWhiteSpace(value);
    }

    private static bool TrainingHasSummaryGroup(
        IDictionary<string, string> attrs,
        string category)
    {
        return attrs.TryGetValue("__SummaryGroup." + category, out var value) &&
               !string.IsNullOrWhiteSpace(value);
    }

    private static bool CanPatternOverride(
        IDictionary<string, string> attrs,
        int fieldIndex,
        string currentValue)
    {
        if (fieldIndex >= 15)
            return false;

        string? category = null;

        switch (fieldIndex)
        {
            case 0:
            case 1:
                category = "Recuperator";
                break;
            case 2:
            case 3:
                category = "Filter";
                break;
            case 4:
                category = "Recirculation";
                break;
            case 5:
            case 6:
            case 7:
                category = "Heater";
                break;
            case 8:
            case 10:
            case 11:
                category = "Cooler";
                break;
            case 9:
            case 12:
                category = "Humidifier";
                break;
            case 14:
                category = "HeatExchanger";
                break;
        }

        if (fieldIndex == 13)
        {
            var confidence = ParseTrainingConfidence(attrs, "__Installation.TypeConfidence");
            return confidence < 0.85 || string.IsNullOrWhiteSpace(currentValue);
        }

        if (category == null)
            return true;

        var structured =
            string.Equals(category, "HeatExchanger", StringComparison.OrdinalIgnoreCase)
                ? attrs.Keys.Any(x => x.StartsWith(
                    HeatExchangerParser.RawItemPrefix,
                    StringComparison.OrdinalIgnoreCase))
                : TrainingHasFeatureGroup(attrs, category) ||
                  TrainingHasSummaryGroup(attrs, category);

        if (!structured)
            return true;

        // Structured Excel evidence wins unless the base parser explicitly says that
        // the type is unknown. This keeps user learning useful for unusual type names
        // without letting old/general patterns replace clear G4/F7, "роторный", etc.
        return string.IsNullOrWhiteSpace(currentValue) ||
               currentValue.IndexOf("не определ", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static double ParseTrainingConfidence(IDictionary<string, string> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return 0.0;
        var normalized = (raw ?? "").Replace(',', '.').Trim();
        double value;
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
            return 0.0;
        return value;
    }

    public static InstallationFeatures? TryGetExact(Equipment equipment)
    {
        lock (Sync)
        {
            if (!File.Exists(ExactFile)) return null;
            var key = Signature(equipment);
            var legacyV76Key = SignatureV76(equipment);
            var legacyV57Key = SignatureV57(equipment);
            var legacyKey = SignatureV52(equipment);
            var lines = File.ReadAllLines(ExactFile, Encoding.UTF8);
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                var p = lines[index].Split('\t');
                // v43-v49: 2 metadata fields + 10 feature fields = 12.
                // v50-v52: 2 metadata fields + 13 feature fields = 15.
                // v53-v57: 2 metadata fields + 14 feature fields = 16.\n                // v58+:    2 metadata fields + 15 feature fields = 17.
                if ((p.Length == 12 || p.Length == 15 || p.Length == 16 ||
                     p.Length == 17 || p.Length == 18 || p.Length == 19) &&
                    (p[0] == key || p[0] == legacyV76Key ||
                     p[0] == legacyV57Key || p[0] == legacyKey))
                    return FromFields(p.Skip(2).ToArray());
            }
            return null;
        }
    }

    public static void SaveCorrection(Equipment equipment, InstallationFeatures features)
    {
        lock (Sync)
        {
            Directory.CreateDirectory(Folder);
            var fields = ToFields(features);
            File.AppendAllText(ExactFile,
                Signature(equipment) + "\t" + DateTime.UtcNow.ToString("O") + "\t" + string.Join("\t", fields) + Environment.NewLine,
                new UTF8Encoding(false));

            foreach (var attr in equipment.Attributes ?? new Dictionary<string, string>())
            {
                var token = EquipmentFeatureAnalyzer.Normalize(attr.Key + " " + attr.Value);
                if (token.Length < 3) continue;
                foreach (var field in fields.Select((v, i) => new { i, v }))
                    File.AppendAllText(PatternFile,
                        Escape(token) + "\t" + field.i + "\t" + Escape(field.v) + Environment.NewLine,
                        new UTF8Encoding(false));
            }
        }
    }

    public static void ApplyLearnedPatterns(Equipment equipment, InstallationFeatures result)
    {
        lock (Sync)
        {
            if (!File.Exists(PatternFile)) return;
            var tokens = (equipment.Attributes ?? new Dictionary<string, string>())
                .Select(x => EquipmentFeatureAnalyzer.Normalize(x.Key + " " + x.Value))
                .Where(x => x.Length >= 3).ToList();
            if (tokens.Count == 0) return;

            var votes = new Dictionary<int, Dictionary<string, int>>();
            foreach (var line in File.ReadAllLines(PatternFile, Encoding.UTF8))
            {
                var p = line.Split('\t');
                int index;
                if (p.Length != 3 || !int.TryParse(p[1], out index)) continue;
                var token = Unescape(p[0]);
                if (!tokens.Any(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase))) continue;
                var value = Unescape(p[2]);
                if (!votes.TryGetValue(index, out var bucket) || bucket == null)
                {
                    bucket = new Dictionary<string, int>();
                    votes[index] = bucket;
                }
                int count;
                bucket[value] = bucket.TryGetValue(value, out count) ? count + 1 : 1;
            }

            var fields = ToFields(result);
            foreach (var vote in votes)
            {
                var best = vote.Value.OrderByDescending(x => x.Value).ThenBy(x => x.Key).FirstOrDefault();
                if (best.Value > 0 && vote.Key >= 0 && vote.Key < fields.Length)
                    fields[vote.Key] = best.Key;
            }
            Copy(fields, result);
        }
    }

    public static string Location => Folder;

    private static string Signature(Equipment e)
    {
        var source = e.Id + "\n" + string.Join("\n", (e.Attributes ?? new Dictionary<string,string>())
            .Where(x => !x.Key.StartsWith(
                ProjectDataOverrides.Prefix,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key + "=" + x.Value));
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
    }

    // v77 changed the current signature so saved project state cannot fragment
    // global learning. This recreates the exact v58-v76 signature.
    private static string SignatureV76(Equipment e)
    {
        var source = e.Id + "\n" + string.Join("\n", (e.Attributes ?? new Dictionary<string,string>())
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key + "=" + x.Value));
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
    }

    // v58 adds per-physical-exchanger attributes. Exclude only those new attributes
    // to recreate the signature used by v53-v57 corrections.
    private static string SignatureV57(Equipment e)
    {
        var source = e.Id + "\n" + string.Join("\n", (e.Attributes ?? new Dictionary<string,string>())
            .Where(x => !x.Key.StartsWith(HeatExchangerParser.RawItemPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key + "=" + x.Value));
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
    }

    // v53 adds type-classification metadata to Attributes. This fallback recreates
    // the v52 signature so exact corrections collected during v51/v52 remain usable.
    private static string SignatureV52(Equipment e)
    {
        var source = e.Id + "\n" + string.Join("\n", (e.Attributes ?? new Dictionary<string,string>())
            .Where(x =>
                !x.Key.StartsWith(HeatExchangerParser.RawItemPrefix, StringComparison.OrdinalIgnoreCase) &&
                !x.Key.Equals("__Installation.RawType", StringComparison.OrdinalIgnoreCase) &&
                !x.Key.Equals("__Installation.TypeConfidence", StringComparison.OrdinalIgnoreCase) &&
                !x.Key.Equals("__Installation.TypeEvidence", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key + "=" + x.Value));
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
    }

    private static string[] ToFields(InstallationFeatures x)
    {
        return new[]
        {
            x.Recuperation,
            x.RecuperatorType,
            x.FilterCount,
            x.FilterType,
            x.Recirculation,
            x.Heating,
            x.HeatingType,
            x.HeatingCount,
            x.Cooling,
            x.Humidification,
            x.CoolingType,
            x.CoolingCount,
            x.HumidifierType,
            x.InstallationType,
            x.HeatExchangers,
            x.RecoveryPartner,
            x.RecoveryPartnerSourceKey
        }.Select(Escape).ToArray();
    }

    private static InstallationFeatures FromFields(string[] p)
    {
        var f = p.Select(Unescape).ToArray();
        var result = new InstallationFeatures();
        if (f.Length > 0) result.Recuperation = f[0];
        if (f.Length > 1) result.RecuperatorType = f[1];
        if (f.Length > 2) result.FilterCount = f[2];
        if (f.Length > 3) result.FilterType = f[3];
        if (f.Length > 4) result.Recirculation = f[4];
        if (f.Length > 5) result.Heating = f[5];
        if (f.Length > 6) result.HeatingType = f[6];
        if (f.Length > 7) result.HeatingCount = f[7];
        if (f.Length > 8) result.Cooling = f[8];
        if (f.Length > 9) result.Humidification = f[9];
        if (f.Length > 10) result.CoolingType = f[10];
        if (f.Length > 11) result.CoolingCount = f[11];
        if (f.Length > 12) result.HumidifierType = f[12];
        if (f.Length > 13) result.InstallationType = f[13];
        if (f.Length > 14) result.HeatExchangers = f[14];
        if (f.Length > 15) result.RecoveryPartner = f[15];
        if (f.Length > 16) result.RecoveryPartnerSourceKey = f[16];
        return result;
    }

    private static void Copy(string[] f, InstallationFeatures x)
    {
        var v = f.Select(Unescape).ToArray();
        if (v.Length > 0) x.Recuperation = v[0];
        if (v.Length > 1) x.RecuperatorType = v[1];
        if (v.Length > 2) x.FilterCount = v[2];
        if (v.Length > 3) x.FilterType = v[3];
        if (v.Length > 4) x.Recirculation = v[4];
        if (v.Length > 5) x.Heating = v[5];
        if (v.Length > 6) x.HeatingType = v[6];
        if (v.Length > 7) x.HeatingCount = v[7];
        if (v.Length > 8) x.Cooling = v[8];
        if (v.Length > 9) x.Humidification = v[9];
        if (v.Length > 10) x.CoolingType = v[10];
        if (v.Length > 11) x.CoolingCount = v[11];
        if (v.Length > 12) x.HumidifierType = v[12];
        if (v.Length > 13) x.InstallationType = v[13];
        if (v.Length > 14) x.HeatExchangers = v[14];
        if (v.Length > 15) x.RecoveryPartner = v[15];
        if (v.Length > 16) x.RecoveryPartnerSourceKey = v[16];
    }

    private static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "").Replace("\n", "\\n");
    private static string Unescape(string s) => (s ?? "").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
}
