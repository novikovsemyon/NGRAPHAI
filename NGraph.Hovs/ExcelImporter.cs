using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HOVS.Model;

namespace HOVS.Plugin;

public sealed class ExcelImportDiagnostics
{
    public string WorkbookPath { get; set; } = "";
    public string SheetName { get; set; } = "";
    public int HeaderRow { get; set; }
    public int DesignationColumn { get; set; }
    public string DesignationColumns { get; set; } = "";
    public string SchemaProfileName { get; set; } = "";
    public double SchemaProfileConfidence { get; set; }
    public bool SchemaFromProfile { get; set; }
    public int FirstDataRow { get; set; }
    public int ImportedRows { get; set; }
    public int ImportedInstallations { get; set; }
    public int SkippedNonEmptyRows { get; set; }
    public int DuplicateDesignationRows { get; set; }
    public int DuplicateCrossSheetRows { get; set; }
    public int ContinuationRowsMerged { get; set; }
    public int FeatureGroupsDetected { get; set; }
    public int WorksheetsScanned { get; set; }
    public int WorksheetsImported { get; set; }
    public int WorksheetsSkipped { get; set; }
    public int RejectedNoAirflowRows { get; set; }
    public int ProvisionalNoAirflowRows { get; set; }
    public int NotesRowsAnalyzed { get; set; }
    public int ReserveRowsDetected { get; set; }
    public int AdditionalEquipmentRowsDetected { get; set; }
    public string LogPath { get; set; } = "";
    public List<string> Imported { get; } = new List<string>();
    public List<string> Skipped { get; } = new List<string>();
    public List<string> Duplicates { get; } = new List<string>();
    public List<string> Continuations { get; } = new List<string>();
    public List<string> FeatureGroups { get; } = new List<string>();
    public List<string> WorksheetDetails { get; } = new List<string>();
    public List<string> RejectedNoAirflow { get; } = new List<string>();
    public List<string> ProvisionalNoAirflow { get; } = new List<string>();
    public List<string> NotesEquipment { get; } = new List<string>();

    public string Summary
    {
        get
        {
            return "Листы: " + WorksheetsImported + "/" + WorksheetsScanned +
                   (string.IsNullOrWhiteSpace(SchemaProfileName) ? "" : " | профиль ХОВС: " + SchemaProfileName) +
                   " | установок: " + ImportedInstallations +
                   " | строк XLSX: " + ImportedRows +
                   " | без L кандидатов: " + ProvisionalNoAirflowRows +
                   " | без L отклонено: " + RejectedNoAirflowRows +
                   " | продолжений: " + ContinuationRowsMerged +
                   " | резерв из примечаний: " + ReserveRowsDetected +
                   " | доп. оборудование: " + AdditionalEquipmentRowsDetected +
                   " | групп оборудования: " + FeatureGroupsDetected +
                   " | дубликатов: " + (DuplicateDesignationRows + DuplicateCrossSheetRows) +
                   " | пропущено: " + SkippedNonEmptyRows;
        }
    }
}

public sealed class ExcelImporter
{
    private const string RecuperatorTypeKey = "__Feature.Recuperator.Type";
    private const string RecuperatorQuantityKey = "__Feature.Recuperator.Quantity";
    private const string RecuperatorPartnerKey = "__Feature.Recuperator.Partner";
    private const string FilterTypeKey = "__Feature.Filter.Type";
    private const string FilterQuantityKey = "__Feature.Filter.Quantity";
    private const string HumidifierTypeKey = "__Feature.Humidifier.Type";
    private const string HumidifierQuantityKey = "__Feature.Humidifier.Quantity";
    private const string CoolerTypeKey = "__Feature.Cooler.Type";
    private const string CoolerQuantityKey = "__Feature.Cooler.Quantity";
    private const string HeaterTypeKey = "__Feature.Heater.Type";
    private const string HeaterQuantityKey = "__Feature.Heater.Quantity";

    private const string InstallationAirflowKey = "__Installation.Airflow";
    private const string InstallationAirflowAllKey = "__Installation.AirflowAll";
    private const string InstallationAirflowSourceKey = "__Installation.AirflowSource";
    private const string InstallationConfidenceKey = "__Installation.Confidence";
    private const string InstallationEvidenceKey = "__Installation.Evidence";
    private const string InstallationRawTypeKey = "__Installation.RawType";
    private const string InstallationTypeConfidenceKey = "__Installation.TypeConfidence";
    private const string InstallationTypeEvidenceKey = "__Installation.TypeEvidence";

    // v63: raw source snapshot for side-by-side expert verification in the
    // analysis window. Key format: __SourceCell.RRRRRR.CCCC
    // Value format: <header> UNIT_SEPARATOR <raw cell text>.
    private const string SourceCellPrefix = SourceCellCodec.Prefix;

    private sealed class FeatureGroupColumns
    {
        public string Category { get; set; } = "";
        public string Header { get; set; } = "";
        public int HeaderRow { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public int TypeColumn { get; set; }
        public int QuantityColumn { get; set; }
    }

    private sealed class SummaryFeatureColumn
    {
        public string Category { get; set; } = "";
        public string Header { get; set; } = "";
        public int Column { get; set; }
    }

    private sealed class AirflowColumn
    {
        public int Column { get; set; }
        public string Header { get; set; } = "";
        public int Rank { get; set; }
    }

    private sealed class AirflowEvidence
    {
        public double Value { get; set; }
        public string Source { get; set; } = "";
        public List<string> All { get; } = new List<string>();
        public bool IsPositive => Value > 0.0;
    }

    public ExcelImportDiagnostics LastDiagnostics { get; private set; } = new ExcelImportDiagnostics();

    public HovsModel Load(string path, Func<XLWorkbook, string, HovsSchema> resolveSchema)
    {
        using var wb = new XLWorkbook(path);

        var schema = resolveSchema(wb, path);

        var equipment = new List<Equipment>();
        var components = new List<EquipmentComponent>();
        var diagnostics = new ExcelImportDiagnostics
        {
            WorkbookPath = path,
            SchemaProfileName = schema.ProfileName,
            SchemaProfileConfidence = schema.ProfileConfidence,
            SchemaFromProfile = schema.FromProfile
        };
        var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var crossSheetFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var importedSheets = new List<string>();

        foreach (var ws in wb.Worksheets)
        {
            diagnostics.WorksheetsScanned++;

            if (!string.Equals(ws.Name, schema.WorksheetName, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.WorksheetsSkipped++;
                diagnostics.WorksheetDetails.Add(
                    ws.Name + ": пропущен — профиль ХОВС выбрал рабочий лист '" +
                    schema.WorksheetName + "'");
                continue;
            }

            var used = ws.RangeUsed();
            if (used == null)
            {
                diagnostics.WorksheetsSkipped++;
                diagnostics.WorksheetDetails.Add(ws.Name + ": пропущен — лист пуст");
                continue;
            }

            var lastRow = used.LastRow().RowNumber();
            var lastColumn = used.LastColumn().ColumnNumber();

            var headerRow = schema.HeaderRow > 0 ? schema.HeaderRow : used.FirstRow().RowNumber();
            var lastHeaderRow = schema.LastHeaderRow >= headerRow
                ? schema.LastHeaderRow
                : headerRow;

            var idColumns = GetSemanticColumns(schema, ColumnSemanticKind.InstallationName);
            if (idColumns.Count == 0)
            {
                int detectedHeaderRow;
                int detectedIdColumn;
                if (!TryFindDesignationHeader(ws, out detectedHeaderRow, out detectedIdColumn))
                {
                    diagnostics.WorksheetsSkipped++;
                    diagnostics.WorksheetDetails.Add(
                        ws.Name + ": пропущен — в профиле и заголовках не найдено обозначение установки");
                    continue;
                }

                headerRow = detectedHeaderRow;
                idColumns.Add(detectedIdColumn);
            }

            var idCol = idColumns[0];
            var firstDataRow = schema.FirstDataRow > lastHeaderRow
                ? schema.FirstDataRow
                : FindFirstDataRow(ws, idColumns, lastHeaderRow + 1, lastRow);

            if (firstDataRow <= 0 || firstDataRow > lastRow)
                firstDataRow = FindFirstDataRow(ws, idColumns, lastHeaderRow + 1, lastRow);

            if (firstDataRow == 0)
            {
                diagnostics.WorksheetsSkipped++;
                diagnostics.WorksheetDetails.Add(ws.Name + ": пропущен — нет обозначений ниже заголовка");
                continue;
            }

            lastHeaderRow = Math.Min(Math.Max(headerRow, lastHeaderRow), firstDataRow - 1);

            var schemaAirflowColumns = GetSemanticColumns(schema, ColumnSemanticKind.AirFlow);
            var airflowColumns = schemaAirflowColumns.Count > 0
                ? BuildAirflowColumnsFromSchema(schema, schemaAirflowColumns)
                : DiscoverAirflowColumns(ws, headerRow, lastHeaderRow, lastColumn);

            // If the expert intentionally saved a schema without the mandatory L
            // semantic, keep the rows visible only as <10% candidates. Outdoor and
            // recirculation flows never substitute for installation design L.
            var provisionalNoAirflowSheet =
                airflowColumns.Count == 0 &&
                (schema.Columns.Any(x => x.Kind == ColumnSemanticKind.InstallationName) ||
                 HasLegacyInstallationTableSignature(ws, headerRow, lastHeaderRow, lastColumn));

            if (provisionalNoAirflowSheet)
            {
                // A reduced/legacy HOVS sheet can contain enough information to identify
                // installation CANDIDATES (designation, system count, room, equipment type,
                // power, heater data, etc.) while omitting L entirely.
                //
                // We load those candidates for review/export instead of failing the workbook,
                // but the expert rule remains intact: without positive L they are NOT confirmed
                // installations and their confidence must stay below 10%.
                diagnostics.WorksheetDetails.Add(
                    ws.Name + ": режим совместимости — столбец L отсутствует; " +
                    "кандидаты будут загружены с вероятностью <10% и не выбраны для построения");
            }

            var map = BuildHeaderMap(ws, headerRow, lastHeaderRow);
            var sourceColumnLabels = BuildSourceColumnLabels(ws, headerRow, lastHeaderRow, lastColumn);
            var featureGroups = DiscoverFeatureGroups(ws, headerRow, lastHeaderRow, lastColumn);
            SuppressFeatureGroupsOwnedBySchema(featureGroups, schema);
            var summaryColumns = DiscoverSummaryFeatureColumns(ws, headerRow, lastHeaderRow, lastColumn);
            var schemaOwnedColumns = new HashSet<int>(
                schema.Columns
                    .Where(x => x.Kind != ColumnSemanticKind.Ignore)
                    .Select(x => x.ColumnNumber));

            int FindExact(params string[] names)
            {
                foreach (var n in names)
                {
                    var key = Normalize(n);
                    int c;
                    if (map.TryGetValue(key, out c)) return c;
                }
                return 0;
            }

            int FindContains(params string[] fragments)
            {
                foreach (var fragment in fragments)
                {
                    var needle = Normalize(fragment);
                    foreach (var kv in map)
                    {
                        if (kv.Key.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                            return kv.Value;
                    }
                }
                return 0;
            }

            if (idCol == 0)
            {
                idCol = FindExact(
                    "Установка", "Обозначение", "Обозначение установки",
                    "Обозначение системы", "Система");
                if (idCol == 0) idCol = FindContains("Обозначение", "Система");
            }

            var typeColumns = GetSemanticColumns(schema, ColumnSemanticKind.InstallationType);
            if (typeColumns.Count == 0)
            {
                var typeCol = FindExact("Тип", "Тип установки", "Тип (наименование)", "Наименование");
                if (typeCol == 0) typeCol = FindContains("Тип (наименование)", "Тип установки");
                if (typeCol > 0) typeColumns.Add(typeCol);
            }

            var roomColumns = GetSemanticColumns(schema, ColumnSemanticKind.Room);
            if (roomColumns.Count == 0)
            {
                var roomCol = FindExact(
                    "Помещение", "Номер помещения", "Комната",
                    "Помещение расположения установки",
                    "Наименование обслуживаемого помещения (технологического оборудования)");
                if (roomCol == 0)
                    roomCol = FindContains("Наименование обслуживаемого помещения", "Помещение");
                if (roomCol > 0) roomColumns.Add(roomCol);
            }

            var systemCountColumns = GetSemanticColumns(schema, ColumnSemanticKind.SystemCount);
            if (systemCountColumns.Count == 0)
            {
                var systemCountCol = FindExact(
                    "Кол. систем", "Кол систем", "Количество систем",
                    "Кол-во систем", "Кол. сис-тем", "Кол. сис тем");
                if (systemCountCol == 0)
                    systemCountCol = FindContains("Кол. систем", "Количество систем", "Кол-во систем", "Кол. сис");
                if (systemCountCol > 0) systemCountColumns.Add(systemCountCol);
            }

            diagnostics.WorksheetsImported++;
            importedSheets.Add(ws.Name);
            if (diagnostics.HeaderRow == 0)
            {
                diagnostics.HeaderRow = headerRow;
                diagnostics.DesignationColumn = idCol;
                diagnostics.DesignationColumns = string.Join(", ", idColumns);
                diagnostics.FirstDataRow = firstDataRow;
            }

            diagnostics.FeatureGroupsDetected += featureGroups.Count;
            diagnostics.WorksheetDetails.Add(
                ws.Name + ": импорт | header row=" + headerRow +
                " | id col=" + idCol +
                " | L cols=" + (airflowColumns.Count == 0
                    ? "нет (режим кандидатов <10%)"
                    : string.Join(", ", airflowColumns.Select(x => x.Column + " [" + x.Header + "]"))) +
                " | feature groups=" + featureGroups.Count +
                " | summary fields=" + summaryColumns.Count +
                " | schema profile='" + (string.IsNullOrWhiteSpace(schema.ProfileName) ? "новый" : schema.ProfileName) + "'" +
                " | schema confidence=" + Math.Round(schema.ProfileConfidence * 100.0).ToString("0") + "%");

            foreach (var group in featureGroups)
            {
                diagnostics.FeatureGroups.Add(
                    ws.Name + " | " + group.Category + " | " + group.Header +
                    " | columns=" + group.StartColumn + "-" + group.EndColumn +
                    " | type col=" + group.TypeColumn +
                    " | qty col=" + group.QuantityColumn);
            }

            for (var rowNumber = firstDataRow; rowNumber <= lastRow; rowNumber++)
            {
                var rawId = FirstNonEmptyRawValue(ws, idColumns, rowNumber, rowNumber);
                var id = NormalizeDesignation(rawId);
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!IsCandidateDesignation(id))
                {
                    diagnostics.SkippedNonEmptyRows++;
                    diagnostics.Skipped.Add(
                        ws.Name + " row " + rowNumber + ": '" + id + "' -> не похоже на обозначение установки");
                    continue;
                }

                // Every candidate owns the blank-id continuation rows below it until
                // the next non-empty designation cell. This is how many expert XLSX
                // templates store a second air stream or child equipment rows.
                var groupEndRow = rowNumber;
                for (var probe = rowNumber + 1; probe <= lastRow; probe++)
                {
                    var nextId = NormalizeDesignation(
                        FirstNonEmptyRawValue(ws, idColumns, probe, probe));
                    if (!string.IsNullOrWhiteSpace(nextId)) break;
                    groupEndRow = probe;
                }

                var rawType = CombineFirstValuesByColumn(ws, typeColumns, rowNumber, groupEndRow);
                var room = CombineFirstValuesByColumn(ws, roomColumns, rowNumber, groupEndRow);
                var systemCountRaw = FirstNonEmptyValue(ws, systemCountColumns, rowNumber, groupEndRow);

                var airflow = airflowColumns.Count == 0
                    ? new AirflowEvidence()
                    : ExtractAirflowEvidence(ws, rowNumber, groupEndRow, airflowColumns);

                var provisionalNoAirflow =
                    !airflow.IsPositive &&
                    provisionalNoAirflowSheet;

                if (!airflow.IsPositive && !provisionalNoAirflow)
                {
                    diagnostics.RejectedNoAirflowRows++;
                    diagnostics.RejectedNoAirflow.Add(
                        ws.Name + " row " + rowNumber + ": " + id +
                        " -> ОТКЛОНЕНО: в таблице есть поля L, но для этой установки " +
                        "нет положительного L, м3/ч; вероятность установки 5%");
                    rowNumber = groupEndRow;
                    continue;
                }

                var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (var sourceRow = rowNumber; sourceRow <= groupEndRow; sourceRow++)
                {
                    var row = ws.Row(sourceRow);
                    var genericRowAttributes = ExtractGenericAttributes(
                        row,
                        map,
                        idCol,
                        schemaOwnedColumns);
                    var canonicalRowAttributes = ExtractCanonicalFeatures(row, featureGroups);
                    var summaryRowAttributes = ExtractSummaryFeatures(row, summaryColumns);
                    var schemaRowAttributes = ExtractSchemaFeatures(row, schema);
                    MergeAttributes(attrs, genericRowAttributes);
                    MergeAttributes(attrs, canonicalRowAttributes);
                    MergeAttributes(attrs, summaryRowAttributes);
                    MergeAttributes(attrs, schemaRowAttributes);

                    // Preserve the original XLSX row contents separately from all
                    // normalized/derived attributes. This allows the expert to compare
                    // the program result with the actual source cells without opening
                    // Excel and without changing recognition logic.
                    CaptureSourceCells(
                        attrs,
                        ws,
                        sourceRow,
                        sourceColumnLabels,
                        lastColumn);

                    if (sourceRow > rowNumber &&
                        (genericRowAttributes.Count > 0 ||
                         canonicalRowAttributes.Count > 0 ||
                         summaryRowAttributes.Count > 0 ||
                         schemaRowAttributes.Count > 0))
                    {
                        diagnostics.ContinuationRowsMerged++;
                        diagnostics.Continuations.Add(
                            ws.Name + " row " + sourceRow + " -> продолжение " + id +
                            " (source row " + rowNumber + ")");
                    }
                }

                foreach (var category in featureGroups
                             .Select(x => x.Category)
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    attrs["__FeatureGroup." + category] = "1";
                }

                foreach (var category in summaryColumns
                             .Select(x => x.Category)
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    attrs["__SummaryGroup." + category] = "1";
                }

                // v70: remarks are part of the installation composition. They may
                // contain reserve fans/motors/installations and explicit additional
                // equipment (silencers, mixing units, cyclone, TION, winter kit, etc.).
                // Analyze both normal attributes and the raw source-cell snapshot so
                // duplicate/multilevel "Примечание" columns are not lost.
                var noteAnalysis = NotesEquipmentParser.Analyze(attrs);
                NotesEquipmentParser.ApplyToAttributes(attrs, noteAnalysis);

                if (!string.IsNullOrWhiteSpace(noteAnalysis.RawNotes))
                    diagnostics.NotesRowsAnalyzed++;
                if (noteAnalysis.HasReserve)
                    diagnostics.ReserveRowsDetected++;
                if (noteAnalysis.HasAdditionalEquipment)
                    diagnostics.AdditionalEquipmentRowsDetected++;

                if (noteAnalysis.HasReserve || noteAnalysis.HasAdditionalEquipment)
                {
                    diagnostics.NotesEquipment.Add(
                        ws.Name + " row " + rowNumber + ": " + id +
                        (noteAnalysis.HasReserve
                            ? " | резерв: " + noteAnalysis.ReserveSummary
                            : "") +
                        (noteAnalysis.HasAdditionalEquipment
                            ? " | доп.: " + noteAnalysis.AdditionalEquipmentSummary
                            : ""));
                }

                // Explicit electric heater in remarks is a real physical heat
                // exchanger. Add one canonical raw item instead of flattening it into
                // the old single-heater field, so multiple heaters remain possible.
                if (noteAnalysis.HasElectricHeater)
                {
                    var noteHeatKey =
                        HeatExchangerParser.RawItemPrefix +
                        "NOTE." +
                        rowNumber.ToString("D5", CultureInfo.InvariantCulture) +
                        ".0001";

                    if (!attrs.ContainsKey(noteHeatKey))
                    {
                        attrs[noteHeatKey] =
                            HeatExchangerParser.EncodeRawItem(
                                "Нагрев",
                                "Электрический",
                                "1");
                    }
                }

                // Partner installation is often written directly in the recuperator
                // type cell, e.g. "Гликолевый\nВ1.2.ЖК.К2". Treat this expert-provided
                // text as authoritative relation evidence.
                if (attrs.TryGetValue(RecuperatorTypeKey, out var recuperatorRaw) &&
                    recuperatorRaw is not null &&
                    !string.IsNullOrWhiteSpace(recuperatorRaw))
                {
                    foreach (var partner in ExtractSystemReferences(recuperatorRaw))
                        AppendAttribute(attrs, RecuperatorPartnerKey, partner);
                }

                attrs["__SourceSheet"] = ws.Name;
                attrs["__SourceRow"] = rowNumber.ToString(CultureInfo.InvariantCulture);
                attrs["__SourceEndRow"] = groupEndRow.ToString(CultureInfo.InvariantCulture);
                attrs["__RawDesignation"] = rawId ?? "";
                if (airflow.IsPositive)
                {
                    attrs[InstallationAirflowKey] = airflow.Value.ToString("0.###", CultureInfo.InvariantCulture);
                    attrs[InstallationAirflowAllKey] = string.Join(" | ", airflow.All);
                    attrs[InstallationAirflowSourceKey] = airflow.Source;
                    attrs[InstallationConfidenceKey] = "0.99";
                    attrs[InstallationEvidenceKey] =
                        "положительный расход L=" + airflow.Value.ToString("0.###", CultureInfo.InvariantCulture) +
                        " м3/ч; " + airflow.Source;
                }
                else
                {
                    var provisionalConfidence = CalculateLegacyCandidateConfidence(rawType, room, systemCountRaw);
                    attrs[InstallationAirflowKey] = "";
                    attrs[InstallationAirflowAllKey] = "";
                    attrs[InstallationAirflowSourceKey] =
                        "в исходной сокращённой таблице отсутствует столбец L, м3/ч";
                    attrs[InstallationConfidenceKey] =
                        provisionalConfidence.ToString("0.00", CultureInfo.InvariantCulture);
                    attrs[InstallationEvidenceKey] =
                        "кандидат распознан по структуре сокращённой таблицы: " +
                        BuildLegacyCandidateEvidence(rawType, room, systemCountRaw) +
                        "; положительный L отсутствует — кандидат не является подтверждённой установкой";

                    diagnostics.ProvisionalNoAirflowRows++;
                    diagnostics.ProvisionalNoAirflow.Add(
                        ws.Name + " row " + rowNumber + ": " + id +
                        " -> КАНДИДАТ: L отсутствует; вероятность " +
                        Math.Round(provisionalConfidence * 100.0).ToString("0") +
                        "%; " + BuildLegacyCandidateEvidence(rawType, room, systemCountRaw));
                }

                // Split the RAW cell, not the normalized value, so expert files that
                // write two systems on separate lines remain two separate systems.
                var systemIds = SplitSystemDesignations(rawId);
                if (systemIds.Count == 0) systemIds.Add(id);
                attrs["__SourceGroupIds"] = string.Join(" | ", systemIds);

                diagnostics.ImportedRows++;
                foreach (var systemId in systemIds)
                {
                    int occurrence;
                    if (!occurrences.TryGetValue(systemId, out occurrence)) occurrence = 0;
                    occurrence++;
                    occurrences[systemId] = occurrence;

                    var equipmentAttrs = new Dictionary<string, string>(attrs, StringComparer.OrdinalIgnoreCase);
                    equipmentAttrs["__DesignationOccurrence"] = occurrence.ToString(CultureInfo.InvariantCulture);

                    // v53 separates installation type classification from feature recognition.
                    // The source text is preserved, while Equipment.Type is normalized to the
                    // controlled taxonomy: В / П / ПВУ / ВТЗ / АВО / Другая.
                    var typeClassification = InstallationTypeClassifier.Classify(systemId, rawType);
                    equipmentAttrs[InstallationRawTypeKey] = rawType ?? "";
                    equipmentAttrs[InstallationTypeConfidenceKey] =
                        typeClassification.Confidence.ToString("0.####", CultureInfo.InvariantCulture);
                    equipmentAttrs[InstallationTypeEvidenceKey] = typeClassification.Evidence;

                    // Only remove exact copies originating from another worksheet.
                    // Repeated designations inside one worksheet remain separate rows.
                    var fingerprint = BuildFingerprint(systemId, typeClassification.Type, room, equipmentAttrs);
                    string? previousSheet;
                    if (crossSheetFingerprints.TryGetValue(fingerprint, out previousSheet) &&
                        !string.Equals(previousSheet, ws.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.DuplicateCrossSheetRows++;
                        diagnostics.Duplicates.Add(
                            ws.Name + " row " + rowNumber + ": " + systemId +
                            " -> точная копия с листа " + previousSheet);
                        continue;
                    }
                    crossSheetFingerprints[fingerprint] = ws.Name;

                    var item = new Equipment(systemId, systemId, typeClassification.Type, room, equipmentAttrs);
                    equipment.Add(item);
                    AddComponents(systemId, equipmentAttrs, components);
                    AddCanonicalComponents(systemId, equipmentAttrs, components);
                    NotesEquipmentParser.AddComponents(
                        systemId,
                        noteAnalysis,
                        components);

                    diagnostics.ImportedInstallations++;
                    diagnostics.Imported.Add(
                        ws.Name + " row " + rowNumber + ": " + systemId +
                        " | тип=" + typeClassification.Type +
                        " (" + Math.Round(typeClassification.Confidence * 100.0).ToString("0") + "%)" +
                        (airflow.IsPositive
                            ? " | L=" + airflow.Value.ToString("0.###", CultureInfo.InvariantCulture) + " м3/ч"
                            : " | L=нет | КАНДИДАТ <10%, построение по умолчанию отключено") +
                        (string.IsNullOrWhiteSpace(room) ? "" : " | " + room));
                }

                rowNumber = groupEndRow;
            }
        }

        diagnostics.SheetName = string.Join(", ", importedSheets);

        foreach (var duplicate in occurrences.Where(x => x.Value > 1).OrderBy(x => x.Key))
        {
            diagnostics.DuplicateDesignationRows += duplicate.Value - 1;
            diagnostics.Duplicates.Add(duplicate.Key + " -> " + duplicate.Value + " исходных строк/записей");
        }

        if (equipment.Count == 0)
        {
            WriteDiagnostics(diagnostics);
            LastDiagnostics = diagnostics;
            throw new InvalidOperationException(
                "Не найдено установок, прошедших обязательный критерий расхода L, м3/ч. " +
                "Журнал импорта: " + diagnostics.LogPath);
        }

        var relations = ConnectionRules.Find(equipment, components).ToList();
        AddExplicitRecoveryRelations(equipment, relations);

        WriteDiagnostics(diagnostics);
        LastDiagnostics = diagnostics;
        return new HovsModel(equipment, components, relations);
    }

    private static bool IsImportableWorksheet(IXLWorksheet ws)
    {
        var used = ws.RangeUsed();
        if (used == null) return false;

        int headerRow;
        int idCol;
        if (!TryFindDesignationHeader(ws, out headerRow, out idCol)) return false;
        var firstDataRow = FindFirstDataRow(ws, idCol, headerRow + 1, used.LastRow().RowNumber());
        if (firstDataRow == 0) return false;
        var lastHeaderRow = Math.Max(headerRow, firstDataRow - 1);
        var lastColumn = used.LastColumn().ColumnNumber();

        if (DiscoverAirflowColumns(ws, headerRow, lastHeaderRow, lastColumn).Count > 0)
            return true;

        return HasLegacyInstallationTableSignature(ws, headerRow, lastHeaderRow, lastColumn);
    }

    private static bool IsGenericContinuationSheetName(string name)
    {
        var n = Normalize(name).ToLowerInvariant();
        if (Regex.IsMatch(n, @"^(?:лист|sheet)\s*\d+$", RegexOptions.IgnoreCase)) return true;
        if (n == "ховс" || n == "ов_эом" || n == "ов-эом" || n == "ов эом") return true;
        if (n.IndexOf("таблица ховс", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static string FirstNonEmptyValue(
        IXLWorksheet ws, int column, int firstRow, int lastRow)
    {
        if (column <= 0) return "";
        for (var row = firstRow; row <= lastRow; row++)
        {
            var value = Normalize(ws.Cell(row, column).GetString());
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return "";
    }

    private static List<int> GetSemanticColumns(HovsSchema schema, ColumnSemanticKind kind)
    {
        return schema.Get(kind)
            .Select(x => x.ColumnNumber)
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    private static List<AirflowColumn> BuildAirflowColumnsFromSchema(
        HovsSchema schema,
        IReadOnlyList<int> columns)
    {
        var result = new List<AirflowColumn>();
        foreach (var column in columns)
        {
            var mapping = schema.Columns.FirstOrDefault(x =>
                x.ColumnNumber == column &&
                x.Kind == ColumnSemanticKind.AirFlow);
            var header = mapping?.Header ?? "Расход L";
            result.Add(new AirflowColumn
            {
                Column = column,
                Header = string.IsNullOrWhiteSpace(header) ? "Расход L" : header,
                Rank = CalculateAirflowRank(header)
            });
        }

        return result
            .OrderByDescending(x => x.Rank)
            .ThenBy(x => x.Column)
            .ToList();
    }

    private static int CalculateAirflowRank(string header)
    {
        var n = Normalize(header).ToLowerInvariant();
        if (Regex.IsMatch(
            Normalize(header),
            @"^\s*L\s*[,;]?\s*(?:м3/ч|м³/ч|m3/h)",
            RegexOptions.IgnoreCase))
            return 100;
        if (n.IndexOf("формул", StringComparison.OrdinalIgnoreCase) >= 0) return 96;
        if (n.IndexOf("стадии", StringComparison.OrdinalIgnoreCase) >= 0) return 95;
        if (n.IndexOf("воздухообмен", StringComparison.OrdinalIgnoreCase) >= 0) return 94;
        if (n.IndexOf("+10", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("*1,1", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("*1.1", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("*1,05", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("*1.05", StringComparison.OrdinalIgnoreCase) >= 0)
            return 93;
        return 90;
    }

    private static string FirstNonEmptyValue(
        IXLWorksheet ws,
        IReadOnlyList<int> columns,
        int firstRow,
        int lastRow)
    {
        if (columns == null || columns.Count == 0) return "";
        for (var row = firstRow; row <= lastRow; row++)
        {
            foreach (var column in columns)
            {
                if (column <= 0) continue;
                var value = Normalize(ws.Cell(row, column).GetString());
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return "";
    }

    private static string CombineFirstValuesByColumn(
        IXLWorksheet ws,
        IReadOnlyList<int> columns,
        int firstRow,
        int lastRow)
    {
        if (columns == null || columns.Count == 0) return "";
        var values = new List<string>();
        foreach (var column in columns)
        {
            var value = FirstNonEmptyValue(ws, column, firstRow, lastRow);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!values.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                values.Add(value);
        }
        return string.Join(" | ", values);
    }

    private static string FirstNonEmptyRawValue(
        IXLWorksheet ws,
        IReadOnlyList<int> columns,
        int firstRow,
        int lastRow)
    {
        if (columns == null || columns.Count == 0) return "";
        for (var row = firstRow; row <= lastRow; row++)
        {
            foreach (var column in columns)
            {
                if (column <= 0) continue;
                var value = ws.Cell(row, column).GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return "";
    }

    private static Dictionary<string, string> ExtractSchemaFeatures(
        IXLRow row,
        HovsSchema schema)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var repeatedKinds = new[]
        {
            ColumnSemanticKind.HeatExchanger,
            ColumnSemanticKind.Filter,
            ColumnSemanticKind.Fan,
            ColumnSemanticKind.Recuperator,
            ColumnSemanticKind.Humidifier,
            ColumnSemanticKind.OtherEquipment
        };

        foreach (var kind in repeatedKinds)
        {
            var groups = schema.Columns
                .Where(x => x.Kind == kind)
                .GroupBy(x => Math.Max(1, x.Slot))
                .OrderBy(x => x.Key);

            foreach (var group in groups)
            {
                var populated = group
                    .Select(x => new
                    {
                        Mapping = x,
                        Value = Normalize(row.Cell(x.ColumnNumber).GetString())
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                    .ToList();

                if (populated.Count == 0) continue;

                var typeValues = populated
                    .Where(x => !string.Equals(x.Mapping.Role, "Quantity", StringComparison.OrdinalIgnoreCase))
                    .Select(x => FormatMappedPart(x.Mapping, x.Value))
                    .ToList();
                var quantityValues = populated
                    .Where(x => string.Equals(x.Mapping.Role, "Quantity", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Value)
                    .ToList();

                var combined = typeValues.Count > 0
                    ? string.Join(" | ", typeValues)
                    : string.Join(" | ", populated.Select(x => FormatMappedPart(x.Mapping, x.Value)));
                var quantity = quantityValues.FirstOrDefault() ?? "";
                var slot = group.Key;

                switch (kind)
                {
                    case ColumnSemanticKind.Filter:
                        AppendAttribute(result, FilterTypeKey, combined);
                        if (!string.IsNullOrWhiteSpace(quantity))
                            AppendAttribute(result, FilterQuantityKey, quantity);
                        result["Фильтр " + slot + " [схема ХОВС]"] = combined;
                        break;

                    case ColumnSemanticKind.Fan:
                        result["Вентилятор " + slot + " [схема ХОВС]"] =
                            AppendQuantityText(combined, quantity);
                        break;

                    case ColumnSemanticKind.Recuperator:
                        AppendAttribute(result, RecuperatorTypeKey, combined);
                        if (!string.IsNullOrWhiteSpace(quantity))
                            AppendAttribute(result, RecuperatorQuantityKey, quantity);
                        result["Рекуператор " + slot + " [схема ХОВС]"] = combined;
                        break;

                    case ColumnSemanticKind.Humidifier:
                        AppendAttribute(result, HumidifierTypeKey, combined);
                        if (!string.IsNullOrWhiteSpace(quantity))
                            AppendAttribute(result, HumidifierQuantityKey, quantity);
                        result["Увлажнитель " + slot + " [схема ХОВС]"] = combined;
                        break;

                    case ColumnSemanticKind.HeatExchanger:
                    {
                        result["Теплообменник ТО" + slot + " [схема ХОВС]"] =
                            AppendQuantityText(combined, quantity);

                        var headers = string.Join(" | ", group.Select(x => x.Header));
                        var function = HeatExchangerParser.InferFunction(
                            "HeatExchanger",
                            headers,
                            combined);
                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            var itemKey =
                                HeatExchangerParser.RawItemPrefix +
                                row.RowNumber().ToString("D5", CultureInfo.InvariantCulture) +
                                ".S" + slot.ToString("D3", CultureInfo.InvariantCulture);
                            result[itemKey] = HeatExchangerParser.EncodeRawItem(
                                function,
                                combined,
                                quantity);
                        }
                        break;
                    }

                    case ColumnSemanticKind.OtherEquipment:
                        result["Оборудование " + slot + " [схема ХОВС]"] =
                            AppendQuantityText(combined, quantity);
                        break;
                }
            }
        }

        foreach (var mapping in schema.Get(ColumnSemanticKind.Notes))
        {
            var value = Normalize(row.Cell(mapping.ColumnNumber).GetString());
            if (!string.IsNullOrWhiteSpace(value))
                AppendAttribute(result, "Примечание [схема ХОВС]", value);
        }

        foreach (var mapping in schema.Get(ColumnSemanticKind.OutdoorAirFlow))
        {
            var value = Normalize(row.Cell(mapping.ColumnNumber).GetString());
            if (!string.IsNullOrWhiteSpace(value))
                AppendAttribute(result, "__Installation.OutdoorAirflow", value);
        }

        foreach (var mapping in schema.Get(ColumnSemanticKind.RecirculationAirFlow))
        {
            var value = Normalize(row.Cell(mapping.ColumnNumber).GetString());
            if (!string.IsNullOrWhiteSpace(value))
                AppendAttribute(result, "__Installation.RecirculationAirflow", value);
        }

        return result;
    }

    private static string FormatMappedPart(ColumnSemantic mapping, string value)
    {
        var header = Normalize(mapping.Header);
        if (string.IsNullOrWhiteSpace(header) ||
            string.Equals(mapping.Role, "Value", StringComparison.OrdinalIgnoreCase))
            return value;
        return header + ": " + value;
    }

    private static string AppendQuantityText(string value, string quantity)
    {
        if (string.IsNullOrWhiteSpace(quantity)) return value;
        if (string.IsNullOrWhiteSpace(value)) return "Количество: " + quantity;
        return value + " | Количество: " + quantity;
    }

    private static void SuppressFeatureGroupsOwnedBySchema(
        List<FeatureGroupColumns> groups,
        HovsSchema schema)
    {
        if (groups == null || groups.Count == 0) return;

        var heatColumns = new HashSet<int>(schema.Get(ColumnSemanticKind.HeatExchanger).Select(x => x.ColumnNumber));
        var filterColumns = new HashSet<int>(schema.Get(ColumnSemanticKind.Filter).Select(x => x.ColumnNumber));
        var recuperatorColumns = new HashSet<int>(schema.Get(ColumnSemanticKind.Recuperator).Select(x => x.ColumnNumber));
        var humidifierColumns = new HashSet<int>(schema.Get(ColumnSemanticKind.Humidifier).Select(x => x.ColumnNumber));

        bool Overlaps(FeatureGroupColumns group, HashSet<int> columns)
        {
            return columns.Any(column => column >= group.StartColumn && column <= group.EndColumn);
        }

        groups.RemoveAll(x =>
            ((string.Equals(x.Category, "Heater", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(x.Category, "Cooler", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(x.Category, "HeatExchanger", StringComparison.OrdinalIgnoreCase)) &&
             Overlaps(x, heatColumns)) ||
            (string.Equals(x.Category, "Filter", StringComparison.OrdinalIgnoreCase) &&
             Overlaps(x, filterColumns)) ||
            (string.Equals(x.Category, "Recuperator", StringComparison.OrdinalIgnoreCase) &&
             Overlaps(x, recuperatorColumns)) ||
            (string.Equals(x.Category, "Humidifier", StringComparison.OrdinalIgnoreCase) &&
             Overlaps(x, humidifierColumns)));
    }

    private static Dictionary<string, string> ExtractGenericAttributes(
        IXLRow row,
        Dictionary<string, int> map,
        int idCol,
        ISet<int>? excludedColumns = null)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map)
        {
            if (kv.Value == idCol) continue;
            if (excludedColumns != null && excludedColumns.Contains(kv.Value)) continue;
            var value = Normalize(row.Cell(kv.Value).GetString());
            if (!string.IsNullOrWhiteSpace(value)) attrs[kv.Key] = value;
        }
        return attrs;
    }

    private static Dictionary<string, string> ExtractCanonicalFeatures(
        IXLRow row,
        List<FeatureGroupColumns> groups)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var type = group.TypeColumn > 0 ? Normalize(row.Cell(group.TypeColumn).GetString()) : "";
            var quantity = group.QuantityColumn > 0 ? Normalize(row.Cell(group.QuantityColumn).GetString()) : "";
            if (string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(quantity)) continue;

            string? typeKey = null;
            string? quantityKey = null;
            switch (group.Category)
            {
                case "Recuperator":
                    typeKey = RecuperatorTypeKey;
                    quantityKey = RecuperatorQuantityKey;
                    break;
                case "Filter":
                    typeKey = FilterTypeKey;
                    quantityKey = FilterQuantityKey;
                    break;
                case "Humidifier":
                    typeKey = HumidifierTypeKey;
                    quantityKey = HumidifierQuantityKey;
                    break;
                case "Cooler":
                    typeKey = CoolerTypeKey;
                    quantityKey = CoolerQuantityKey;
                    break;
                case "Heater":
                    typeKey = HeaterTypeKey;
                    quantityKey = HeaterQuantityKey;
                    break;
                case "HeatExchanger":
                    break;
                default:
                    continue;
            }

            if (typeKey != null && !string.IsNullOrWhiteSpace(type))
                AppendAttribute(result, typeKey, type);
            if (quantityKey != null && !string.IsNullOrWhiteSpace(quantity))
                AppendAttribute(result, quantityKey, quantity);

            // v58: preserve every physical heat-exchanger group independently.
            // Do not collapse equal quantities such as "1 | 1": two exchanger groups
            // really mean two exchangers even when both quantities are 1.
            if (group.Category == "Heater" ||
                group.Category == "Cooler" ||
                group.Category == "HeatExchanger")
            {
                var function = HeatExchangerParser.InferFunction(group.Category, group.Header, type);
                if (!string.IsNullOrWhiteSpace(function))
                {
                    var itemKey =
                        HeatExchangerParser.RawItemPrefix +
                        row.RowNumber().ToString("D5", CultureInfo.InvariantCulture) + "." +
                        group.StartColumn.ToString("D4", CultureInfo.InvariantCulture);

                    result[itemKey] = HeatExchangerParser.EncodeRawItem(function, type, quantity);

                    // Generic "Теплообменник" groups also feed legacy heating/cooling
                    // fields so older scheme code and old training data stay valid.
                    if (group.Category == "HeatExchanger")
                    {
                        if (string.Equals(function, "Нагрев", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(type)) AppendAttribute(result, HeaterTypeKey, type);
                            if (!string.IsNullOrWhiteSpace(quantity)) AppendAttribute(result, HeaterQuantityKey, quantity);
                        }
                        else if (string.Equals(function, "Охлаждение", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(type)) AppendAttribute(result, CoolerTypeKey, type);
                            if (!string.IsNullOrWhiteSpace(quantity)) AppendAttribute(result, CoolerQuantityKey, quantity);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static Dictionary<string, string> ExtractSummaryFeatures(
        IXLRow row,
        List<SummaryFeatureColumn> columns)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            var value = Normalize(row.Cell(column.Column).GetString());
            if (string.IsNullOrWhiteSpace(value)) continue;
            AppendAttribute(result, "__Summary." + column.Category, value);
        }
        return result;
    }

    private static List<FeatureGroupColumns> DiscoverFeatureGroups(
        IXLWorksheet ws,
        int headerRow,
        int lastHeaderRow,
        int lastColumn)
    {
        var groups = new List<FeatureGroupColumns>();

        for (var row = headerRow; row <= lastHeaderRow; row++)
        {
            for (var col = 1; col <= lastColumn; col++)
            {
                var header = Normalize(ws.Cell(row, col).GetString());
                string category;
                if (!TryClassifyFeatureGroup(header, out category)) continue;
                if (IsInsideSummaryBlock(ws, row, col, headerRow)) continue;

                var groupEndColumn = FindFeatureGroupEndColumn(ws, row, col, lastColumn);
                var typeColumn = FindTypeSubColumn(ws, row + 1, lastHeaderRow, col, groupEndColumn);
                var quantityColumn = FindQuantitySubColumn(ws, row + 1, lastHeaderRow, col, groupEndColumn);

                // If a template has no explicit Type subheader, the leading column
                // itself may contain the type. Quantity, however, is NEVER guessed from
                // the adjacent column: it may be temperature, power or another value.
                if (typeColumn == 0) typeColumn = col;

                if (!groups.Any(x => x.Category == category && x.StartColumn == col && x.HeaderRow == row))
                {
                    groups.Add(new FeatureGroupColumns
                    {
                        Category = category,
                        Header = header,
                        HeaderRow = row,
                        StartColumn = col,
                        EndColumn = groupEndColumn,
                        TypeColumn = typeColumn,
                        QuantityColumn = quantityColumn
                    });
                }
            }
        }

        return groups.OrderBy(x => x.StartColumn).ThenBy(x => x.HeaderRow).ToList();
    }

    private static List<SummaryFeatureColumn> DiscoverSummaryFeatureColumns(
        IXLWorksheet ws,
        int headerRow,
        int lastHeaderRow,
        int lastColumn)
    {
        var result = new List<SummaryFeatureColumn>();

        for (var row = headerRow; row <= lastHeaderRow; row++)
        {
            for (var col = 1; col <= lastColumn; col++)
            {
                var header = Normalize(ws.Cell(row, col).GetString());
                if (string.IsNullOrWhiteSpace(header)) continue;
                if (!IsInsideSummaryBlock(ws, row, col, headerRow)) continue;

                string category;
                if (TryClassifyFeatureGroup(header, out category))
                {
                    if (!result.Any(x => x.Category == category && x.Column == col))
                        result.Add(new SummaryFeatureColumn { Category = category, Header = header, Column = col });
                    continue;
                }

                var n = header.ToLowerInvariant();
                if (n.IndexOf("рекуперац", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddSummary(result, "Recuperator", header, col);
                else if (n.IndexOf("рециркуляц", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddSummary(result, "Recirculation", header, col);
            }
        }

        return result;
    }

    private static void AddSummary(
        List<SummaryFeatureColumn> result,
        string category,
        string header,
        int column)
    {
        if (!result.Any(x => x.Category == category && x.Column == column))
            result.Add(new SummaryFeatureColumn { Category = category, Header = header, Column = column });
    }

    private static bool IsInsideSummaryBlock(
        IXLWorksheet ws,
        int row,
        int col,
        int headerRow)
    {
        for (var probeRow = headerRow; probeRow <= row; probeRow++)
        {
            var firstCol = Math.Max(1, col - 24);
            for (var probeCol = firstCol; probeCol <= col; probeCol++)
            {
                var value = Normalize(ws.Cell(probeRow, probeCol).GetString());
                if (value.IndexOf("состав установок", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    private static bool TryClassifyFeatureGroup(string header, out string category)
    {
        category = "";
        if (string.IsNullOrWhiteSpace(header)) return false;
        var n = header.ToLowerInvariant();

        if (n.IndexOf("рекуператор", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("теплоутилиз", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            category = "Recuperator";
            return true;
        }
        if (n.IndexOf("увлажнител", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            category = "Humidifier";
            return true;
        }
        if (n.IndexOf("фильтр", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            category = "Filter";
            return true;
        }
        if (n.IndexOf("воздухоохлад", StringComparison.OrdinalIgnoreCase) >= 0 || n == "охладитель")
        {
            category = "Cooler";
            return true;
        }
        if (n.IndexOf("воздухонагрев", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("калорифер", StringComparison.OrdinalIgnoreCase) >= 0 || n == "нагреватель")
        {
            category = "Heater";
            return true;
        }
        if (n.IndexOf("теплообменник", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (n.IndexOf("охлажд", StringComparison.OrdinalIgnoreCase) >= 0)
                category = "Cooler";
            else if (n.IndexOf("нагрев", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.IndexOf("подогрев", StringComparison.OrdinalIgnoreCase) >= 0)
                category = "Heater";
            else
                category = "HeatExchanger";
            return true;
        }

        return false;
    }

    private static int FindFeatureGroupEndColumn(
        IXLWorksheet ws,
        int headerRow,
        int startColumn,
        int lastColumn)
    {
        for (var col = startColumn + 1; col <= lastColumn; col++)
        {
            var value = Normalize(ws.Cell(headerRow, col).GetString());
            if (!string.IsNullOrWhiteSpace(value)) return col - 1;
        }
        return lastColumn;
    }

    private static int FindTypeSubColumn(
        IXLWorksheet ws,
        int firstRow,
        int lastRow,
        int firstColumn,
        int lastColumn)
    {
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var col = firstColumn; col <= lastColumn; col++)
            {
                var value = Normalize(ws.Cell(row, col).GetString()).ToLowerInvariant();
                if (value == "тип" ||
                    (value.IndexOf("тип", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     value.IndexOf("наимен", StringComparison.OrdinalIgnoreCase) >= 0))
                    return col;
            }
        }
        return 0;
    }

    private static int FindQuantitySubColumn(
        IXLWorksheet ws,
        int firstRow,
        int lastRow,
        int firstColumn,
        int lastColumn)
    {
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var col = firstColumn; col <= lastColumn; col++)
            {
                var value = Normalize(ws.Cell(row, col).GetString()).ToLowerInvariant();
                if (value == "кол." || value == "кол" || value == "кол-во" ||
                    value.IndexOf("количество", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("кол-во", StringComparison.OrdinalIgnoreCase) >= 0)
                    return col;
            }
        }
        return 0;
    }

    private static bool HasLegacyInstallationTableSignature(
        IXLWorksheet ws,
        int headerRow,
        int lastHeaderRow,
        int lastColumn)
    {
        var hasRoom = false;
        var hasType = false;
        var hasSystemCount = false;
        var hasEquipmentEvidence = false;

        for (var row = headerRow; row <= lastHeaderRow; row++)
        {
            for (var col = 1; col <= lastColumn; col++)
            {
                var header = Normalize(ws.Cell(row, col).GetString()).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(header)) continue;

                if (header.IndexOf("наименование обслуживаемого помещения", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header == "помещение" ||
                    header.IndexOf("помещение расположения", StringComparison.OrdinalIgnoreCase) >= 0)
                    hasRoom = true;

                if (header.IndexOf("тип (наименование)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("тип установки", StringComparison.OrdinalIgnoreCase) >= 0)
                    hasType = true;

                if (header.IndexOf("кол. сис", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("количество систем", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("кол-во систем", StringComparison.OrdinalIgnoreCase) >= 0)
                    hasSystemCount = true;

                if (header.IndexOf("вентилятор", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("воздухонагреватель", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("воздухоохладитель", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("рекуператор", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("фильтр", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("место установки оборудования", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("комплектная автоматика", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    header.IndexOf("комлектная автоматика", StringComparison.OrdinalIgnoreCase) >= 0)
                    hasEquipmentEvidence = true;
            }
        }

        // Do not turn arbitrary tables into HVAC candidates. A reduced HOVS table
        // must still look structurally like an equipment/system schedule.
        return hasType && (hasRoom || hasSystemCount) && hasEquipmentEvidence;
    }

    private static double CalculateLegacyCandidateConfidence(
        string rawType,
        string room,
        string systemCountRaw)
    {
        var evidenceCount = 0;
        if (!string.IsNullOrWhiteSpace(rawType)) evidenceCount++;
        if (!string.IsNullOrWhiteSpace(room)) evidenceCount++;

        double count;
        if (TryParsePositiveNumber(systemCountRaw, out count))
            evidenceCount++;

        // Always below 10% because the mandatory L criterion is not met.
        if (evidenceCount >= 3) return 0.09;
        if (evidenceCount == 2) return 0.08;
        if (evidenceCount == 1) return 0.06;
        return 0.05;
    }

    private static string BuildLegacyCandidateEvidence(
        string rawType,
        string room,
        string systemCountRaw)
    {
        var evidence = new List<string>();

        if (!string.IsNullOrWhiteSpace(rawType))
            evidence.Add("тип/модель='" + Normalize(rawType) + "'");

        if (!string.IsNullOrWhiteSpace(room))
            evidence.Add("помещение='" + Normalize(room) + "'");

        double count;
        if (TryParsePositiveNumber(systemCountRaw, out count))
            evidence.Add("количество систем=" + count.ToString("0.###", CultureInfo.InvariantCulture));

        return evidence.Count == 0
            ? "есть обозначение системы"
            : string.Join(", ", evidence);
    }

    private static List<AirflowColumn> DiscoverAirflowColumns(
        IXLWorksheet ws,
        int headerRow,
        int lastHeaderRow,
        int lastColumn)
    {
        var result = new List<AirflowColumn>();
        for (var row = headerRow; row <= lastHeaderRow; row++)
        {
            for (var col = 1; col <= lastColumn; col++)
            {
                var header = Normalize(ws.Cell(row, col).GetString());
                if (!IsAirflowHeader(header)) continue;

                var rank = 80;
                var n = header.ToLowerInvariant();

                // Base L is authoritative when populated.
                if (Regex.IsMatch(
                    Normalize(header),
                    @"^\s*L\s*[,;]?\s*(?:м3/ч|м³/ч|m3/h)",
                    RegexOptions.IgnoreCase))
                {
                    rank = 100;
                }
                else if (n.IndexOf("формул", StringComparison.OrdinalIgnoreCase) >= 0)
                    rank = 96;
                else if (n.IndexOf("стадии", StringComparison.OrdinalIgnoreCase) >= 0)
                    rank = 95;
                else if (n.IndexOf("воздухообмен", StringComparison.OrdinalIgnoreCase) >= 0)
                    rank = 94;
                else if (n.IndexOf("+10", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("*1,1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("*1.1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("*1,05", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("*1.05", StringComparison.OrdinalIgnoreCase) >= 0)
                    rank = 93;

                if (!result.Any(x => x.Column == col))
                    result.Add(new AirflowColumn { Column = col, Header = header, Rank = rank });
            }
        }

        return result.OrderByDescending(x => x.Rank).ThenBy(x => x.Column).ToList();
    }

    private static bool IsAirflowHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = Normalize(value);
        var n = normalized.ToLowerInvariant()
            .Replace("³", "3")
            .Replace("^", "")
            .Replace(" ", "");

        // These are component/sub-flow fields, not the installation design airflow.
        if (n.IndexOf("наружн", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("рециркуляц", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        var hasLToken =
            Regex.IsMatch(
                normalized,
                @"(^|[^A-Za-zА-Яа-яЁё])L($|[^A-Za-zА-Яа-яЁё])",
                RegexOptions.IgnoreCase);

        if (!hasLToken) return false;

        var hasUnit = n.IndexOf("м3/ч", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("m3/h", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      n.IndexOf("м3ч", StringComparison.OrdinalIgnoreCase) >= 0;

        if (hasUnit) return true;

        // Expert templates often omit the unit in adjacent L-variant columns because
        // the unit is implied by the main L field. These are valid fallback values
        // when the base L cell is empty.
        return n.IndexOf("lизстадии", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("lизвоздухообмена", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("lпоформул", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("lрасчет", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("lрасч", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("lпроект", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("l*1,1", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("l*1.1", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("l*1,05", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("l*1.05", StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("l+10", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static AirflowEvidence ExtractAirflowEvidence(
        IXLWorksheet ws,
        int firstRow,
        int lastRow,
        List<AirflowColumn> columns)
    {
        var evidence = new AirflowEvidence();
        var bestRank = int.MinValue;

        foreach (var column in columns)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                var raw = Normalize(ws.Cell(row, column.Column).GetString());
                double value;
                if (!TryParsePositiveNumber(raw, out value)) continue;

                evidence.All.Add(
                    column.Header + "=" + value.ToString("0.###", CultureInfo.InvariantCulture) +
                    " (row " + row + ", col " + column.Column + ")");

                // Prefer explicitly adjusted/design L columns. If several values have
                // equal priority, retain the larger positive design flow.
                if (column.Rank > bestRank ||
                    (column.Rank == bestRank && value > evidence.Value))
                {
                    bestRank = column.Rank;
                    evidence.Value = value;
                    evidence.Source =
                        "лист '" + ws.Name + "', " + column.Header +
                        ", строка " + row + ", столбец " + column.Column;
                }
            }
        }

        return evidence;
    }

    private static bool TryParsePositiveNumber(string raw, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Replace("\u00A0", " ").Trim();
        var match = Regex.Match(text, @"[-+]?\d[\d\s]*(?:[.,]\d+)?");
        if (!match.Success) return false;

        var number = match.Value.Replace(" ", "").Replace(',', '.');
        double parsed;
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            return false;
        if (parsed <= 0.0) return false;
        value = parsed;
        return true;
    }

    private static IEnumerable<string> ExtractSystemReferences(string? text)
    {
        var result = new List<string>();
        var normalized = NormalizeDesignation(text);
        foreach (Match match in Regex.Matches(
                     normalized,
                     @"(?<![A-Za-zА-Яа-яЁё0-9])(?:ПВ|PV|П|В|P|V)[A-Za-zА-Яа-яЁё0-9]*\d[A-Za-zА-Яа-яЁё0-9]*(?:[.\-_][A-Za-zА-Яа-яЁё0-9]+)*(?![A-Za-zА-Яа-яЁё0-9])",
                     RegexOptions.IgnoreCase))
        {
            var id = NormalizeDesignation(match.Value);
            if (!IsCandidateDesignation(id)) continue;
            if (!result.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                result.Add(id);
        }
        return result;
    }

    private static string BuildFingerprint(
        string id,
        string type,
        string room,
        IDictionary<string, string> attrs)
    {
        return NormalizeDesignation(id).ToUpperInvariant() + "|" +
               Normalize(type).ToUpperInvariant() + "|" +
               Normalize(room).ToUpperInvariant() + "|" +
               Get(attrs, InstallationAirflowKey) + "|" +
               Get(attrs, RecuperatorTypeKey).ToUpperInvariant() + "|" +
               Get(attrs, FilterTypeKey).ToUpperInvariant() + "|" +
               Get(attrs, HeaterTypeKey).ToUpperInvariant() + "|" +
               Get(attrs, CoolerTypeKey).ToUpperInvariant() + "|" +
               HeatExchangerParser.BuildRawFingerprint(attrs).ToUpperInvariant() + "|" +
               Get(attrs, HumidifierTypeKey).ToUpperInvariant();
    }

    private static string Get(IDictionary<string, string> attrs, string key)
    {
        return attrs.TryGetValue(key, out var value)
            ? value ?? ""
            : "";
    }

    private static void AddExplicitRecoveryRelations(
        IReadOnlyList<Equipment> equipment,
        List<Relation> relations)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relation in relations)
            existing.Add(PairKey(relation.SourceId, relation.TargetId, relation.Kind));

        foreach (var item in equipment)
        {
            if (item.Attributes == null ||
                !item.Attributes.TryGetValue(RecuperatorPartnerKey, out var partnerText) ||
                partnerText is null ||
                string.IsNullOrWhiteSpace(partnerText))
                continue;

            var recupType = Get(item.Attributes, RecuperatorTypeKey);
            var kind = RecoveryKindFromText(recupType);
            foreach (var partner in SplitAttributeValues(partnerText))
            {
                if (string.Equals(partner, item.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (!equipment.Any(x => string.Equals(x.Id, partner, StringComparison.OrdinalIgnoreCase))) continue;

                var key = PairKey(item.Id, partner, kind);
                if (existing.Contains(key)) continue;

                relations.Add(new Relation(
                    item.Id,
                    partner,
                    kind,
                    0.995,
                    "связь рекуператора явно указана в исходном XLSX"));
                existing.Add(key);
            }
        }
    }

    private static RelationKind RecoveryKindFromText(string text)
    {
        var n = (text ?? "").ToLowerInvariant();
        if (n.IndexOf("гликол", StringComparison.OrdinalIgnoreCase) >= 0)
            return RelationKind.GlycolicHeatRecovery;
        if (n.IndexOf("пластин", StringComparison.OrdinalIgnoreCase) >= 0)
            return RelationKind.PlateHeatRecovery;
        if (n.IndexOf("ротор", StringComparison.OrdinalIgnoreCase) >= 0)
            return RelationKind.RotaryHeatRecovery;
        return RelationKind.None;
    }

    private static string PairKey(string a, string b, RelationKind kind)
    {
        var left = a ?? "";
        var right = b ?? "";
        if (string.Compare(left, right, StringComparison.OrdinalIgnoreCase) > 0)
        {
            var tmp = left;
            left = right;
            right = tmp;
        }
        return left + "|" + right + "|" + kind;
    }

    private static IEnumerable<string> SplitAttributeValues(string? text)
    {
        return (text ?? "")
            .Split(new[] { '|', ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeDesignation)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static void MergeAttributes(
        Dictionary<string, string> target,
        Dictionary<string, string> source)
    {
        foreach (var pair in source) AppendAttribute(target, pair.Key, pair.Value);
    }

    private static void AppendAttribute(Dictionary<string, string> attrs, string key, string value)
    {
        value = Normalize(value);
        if (string.IsNullOrWhiteSpace(value)) return;

        if (!attrs.TryGetValue(key, out var existing) ||
            existing is null ||
            string.IsNullOrWhiteSpace(existing))
        {
            attrs[key] = value;
            return;
        }

        var parts = existing.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!parts.Any(x => string.Equals(Normalize(x), value, StringComparison.OrdinalIgnoreCase)))
            attrs[key] = existing + " | " + value;
    }

    private static Dictionary<int, string> BuildSourceColumnLabels(
        IXLWorksheet ws,
        int headerRow,
        int lastHeaderRow,
        int lastColumn)
    {
        var result = new Dictionary<int, string>();

        for (var col = 1; col <= lastColumn; col++)
        {
            var parts = new List<string>();

            for (var row = headerRow; row <= lastHeaderRow; row++)
            {
                var value = HovsSchemaAnalyzer.ReadHeaderCellText(ws, row, col);
                if (string.IsNullOrWhiteSpace(value)) continue;

                double number;
                if (double.TryParse(value, out number)) continue;

                if (!parts.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                    parts.Add(value);
            }

            result[col] = parts.Count > 0
                ? string.Join(" / ", parts)
                : "Колонка " + ExcelColumnName(col);
        }

        return result;
    }

    private static void CaptureSourceCells(
        Dictionary<string, string> attrs,
        IXLWorksheet ws,
        int rowNumber,
        Dictionary<int, string> columnLabels,
        int lastColumn)
    {
        for (var col = 1; col <= lastColumn; col++)
        {
            var raw = ws.Cell(rowNumber, col).GetString();
            if (string.IsNullOrWhiteSpace(raw)) continue;

            if (!columnLabels.TryGetValue(col, out var header) ||
                header is null ||
                string.IsNullOrWhiteSpace(header))
            {
                header = "Колонка " + ExcelColumnName(col);
            }

            var key =
                SourceCellPrefix +
                rowNumber.ToString("D6", CultureInfo.InvariantCulture) +
                "." +
                col.ToString("D4", CultureInfo.InvariantCulture);

            // U+241F is XML-safe and avoids ambiguity with common source
            // punctuation. Legacy U+001F is still readable by SourceCellCodec.
            attrs[key] = SourceCellCodec.Encode(header, raw.Trim());
        }
    }

    private static string ExcelColumnName(int column)
    {
        if (column <= 0) return "";

        var result = "";
        var value = column;
        while (value > 0)
        {
            value--;
            result = (char)('A' + (value % 26)) + result;
            value /= 26;
        }

        return result;
    }

    private static Dictionary<string, int> BuildHeaderMap(
        IXLWorksheet ws,
        int headerRow,
        int lastHeaderRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (lastHeaderRow < headerRow) lastHeaderRow = headerRow;

        for (var rowNumber = headerRow; rowNumber <= lastHeaderRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);
            foreach (var cell in row.CellsUsed())
            {
                var key = Normalize(cell.GetString());
                if (string.IsNullOrWhiteSpace(key)) continue;

                double number;
                if (double.TryParse(key, out number)) continue;
                if (!map.ContainsKey(key)) map[key] = cell.Address.ColumnNumber;
            }
        }
        return map;
    }

    private static bool TryFindDesignationHeader(
        IXLWorksheet ws,
        out int headerRow,
        out int idCol)
    {
        headerRow = 0;
        idCol = 0;
        var used = ws.RangeUsed();
        if (used == null) return false;

        var maxRow = Math.Min(used.LastRow().RowNumber(), 60);
        var maxCol = Math.Min(used.LastColumn().ColumnNumber(), 100);
        for (var row = used.FirstRow().RowNumber(); row <= maxRow; row++)
        {
            for (var col = used.FirstColumn().ColumnNumber(); col <= maxCol; col++)
            {
                var value = Normalize(ws.Cell(row, col).GetString());
                if (!IsDesignationHeader(value)) continue;
                headerRow = row;
                idCol = col;
                return true;
            }
        }
        return false;
    }

    private static bool IsDesignationHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.ToLowerInvariant();
        return v == "обозначение" || v == "система" || v == "установка" ||
               v.IndexOf("обозначение системы", StringComparison.OrdinalIgnoreCase) >= 0 ||
               v.IndexOf("обозначение установки", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int FindFirstDataRow(
        IXLWorksheet ws,
        IReadOnlyList<int> idColumns,
        int startRow,
        int endRow)
    {
        if (idColumns == null || idColumns.Count == 0) return 0;
        for (var row = startRow; row <= endRow; row++)
        {
            var value = NormalizeDesignation(
                FirstNonEmptyRawValue(ws, idColumns, row, row));
            if (IsCandidateDesignation(value)) return row;
        }
        return 0;
    }

    private static int FindFirstDataRow(
        IXLWorksheet ws,
        int idCol,
        int startRow,
        int endRow)
    {
        for (var row = startRow; row <= endRow; row++)
        {
            var value = NormalizeDesignation(ws.Cell(row, idCol).GetString());
            if (IsCandidateDesignation(value)) return row;
        }
        return 0;
    }

    private static void AddComponents(
        string equipmentId,
        Dictionary<string, string> attrs,
        List<EquipmentComponent> components)
    {
        foreach (var kv in attrs)
        {
            if (kv.Key.StartsWith("__", StringComparison.Ordinal)) continue;

            var name = kv.Key.ToLowerInvariant();
            string? component = null;
            if (name.IndexOf("вентилятор", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Вентилятор";
            else if (name.IndexOf("рекуператор", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("теплоутилиз", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Рекуператор";
            else if (name.IndexOf("увлажнител", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Увлажнитель";
            else if (name.IndexOf("фильтр", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Фильтр";
            else if (name.IndexOf("охладител", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Охладитель";
            else if (name.IndexOf("теплообмен", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Теплообменник";
            else if (name.IndexOf("нагревател", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("калорифер", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("воздухонагревател", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Нагреватель";
            else if (name.IndexOf("датчик температур", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Датчик температуры";
            else if (name.IndexOf("датчик влажност", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Датчик влажности";
            else if (name.IndexOf("клапан", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Клапан";
            else if (name.IndexOf("насос", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Насос";
            else if (name.IndexOf("оборудован", StringComparison.OrdinalIgnoreCase) >= 0)
                component = "Дополнительное оборудование";

            if (component != null)
                components.Add(new EquipmentComponent(equipmentId, component, kv.Value, null));
        }
    }

    private static void AddCanonicalComponents(
        string equipmentId,
        Dictionary<string, string> attrs,
        List<EquipmentComponent> components)
    {
        AddCanonicalComponent(equipmentId, attrs, RecuperatorTypeKey, "Рекуператор", components);
        AddCanonicalComponent(equipmentId, attrs, FilterTypeKey, "Фильтр", components);
        AddCanonicalComponent(equipmentId, attrs, HumidifierTypeKey, "Увлажнитель", components);

        var hasCanonicalHeatExchangers = attrs.Keys.Any(
            x => x.StartsWith(HeatExchangerParser.RawItemPrefix, StringComparison.OrdinalIgnoreCase));
        if (!hasCanonicalHeatExchangers)
        {
            // Legacy fallback for workbooks where no dedicated exchanger groups were recognized.
            AddCanonicalComponent(equipmentId, attrs, CoolerTypeKey, "Охладитель", components);
            AddCanonicalComponent(equipmentId, attrs, HeaterTypeKey, "Нагреватель", components);
        }

        foreach (var pair in attrs
                     .Where(x => x.Key.StartsWith(HeatExchangerParser.RawItemPrefix, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            HeatExchangerEntry exchanger;
            if (!HeatExchangerParser.TryDecodeRawItem(pair.Value, out exchanger)) continue;
            components.Add(new EquipmentComponent(
                equipmentId,
                "Теплообменник:" + exchanger.Function,
                exchanger.Medium,
                exchanger.Quantity));
        }
    }

    private static void AddCanonicalComponent(
        string equipmentId,
        Dictionary<string, string> attrs,
        string key,
        string componentType,
        List<EquipmentComponent> components)
    {
        if (attrs.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            components.Add(new EquipmentComponent(
                equipmentId,
                componentType,
                value,
                null));
        }
    }

    private static List<string> SplitSystemDesignations(string? value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var part in value!.Split(
                     new[] { '/', '\n', '\r', ';', ',' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var id = NormalizeDesignation(part);
            if (!IsCandidateDesignation(id)) continue;
            if (!result.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                result.Add(id);
        }
        return result;
    }

    private static bool IsCandidateDesignation(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > 120) return false;

        var normalized = NormalizeDesignation(value);
        if (string.IsNullOrWhiteSpace(normalized)) return false;
        if (IsDesignationHeader(normalized)) return false;
        if (Regex.IsMatch(normalized, @"^\d+(?:[.,]\d+)?$")) return false;
        if (!Regex.IsMatch(normalized, @"[A-Za-zА-Яа-яЁё]")) return false;
        if (!Regex.IsMatch(normalized, @"\d")) return false;
        if (normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 6) return false;
        return true;
    }

    private static string NormalizeDesignation(string? value)
    {
        var text = Normalize(value);
        text = Regex.Replace(text, @"\s*\.\s*", ".");
        text = Regex.Replace(text, @"\s*-\s*", "-");
        return text.Trim();
    }

    private static string Normalize(string? value)
    {
        var text = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\u00A0", " ")
            .Trim();
        text = Regex.Replace(text, @"(?<=\p{L})\s*-\s*(?=\p{L})", string.Empty);
        return Regex.Replace(text, @"\s+", " ");
    }

    private static void WriteDiagnostics(ExcelImportDiagnostics diagnostics)
    {
        var folder = Path.Combine(HovsEnvironment.Root, "Logs");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "ExcelImport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");

        var sb = new StringBuilder();
        sb.AppendLine("NGrapfAI XLSX IMPORT DIAGNOSTICS — " + "NGraph — модуль ХОВС (на основе 2.0.37)");
        sb.AppendLine("Timestamp: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Workbook: " + diagnostics.WorkbookPath);
        sb.AppendLine("HOVS schema profile: " + diagnostics.SchemaProfileName);
        sb.AppendLine("HOVS schema confidence: " +
            Math.Round(diagnostics.SchemaProfileConfidence * 100.0).ToString("0") + "%");
        sb.AppendLine("HOVS schema auto-applied: " + diagnostics.SchemaFromProfile);
        sb.AppendLine("Designation columns: " + diagnostics.DesignationColumns);
        sb.AppendLine("Imported sheets: " + diagnostics.SheetName);
        sb.AppendLine("Worksheets scanned: " + diagnostics.WorksheetsScanned);
        sb.AppendLine("Worksheets imported: " + diagnostics.WorksheetsImported);
        sb.AppendLine("Worksheets skipped: " + diagnostics.WorksheetsSkipped);
        sb.AppendLine("Imported source rows: " + diagnostics.ImportedRows);
        sb.AppendLine("Imported installation candidates: " + diagnostics.ImportedInstallations);
        sb.AppendLine("Provisional candidates without L (<10%): " + diagnostics.ProvisionalNoAirflowRows);
        sb.AppendLine("Rejected without positive L, m3/h: " + diagnostics.RejectedNoAirflowRows);
        sb.AppendLine("Continuation rows merged: " + diagnostics.ContinuationRowsMerged);
        sb.AppendLine("Rows with source remarks: " + diagnostics.NotesRowsAnalyzed);
        sb.AppendLine("Rows with reserve equipment in remarks: " + diagnostics.ReserveRowsDetected);
        sb.AppendLine("Rows with additional equipment in remarks: " + diagnostics.AdditionalEquipmentRowsDetected);
        sb.AppendLine("Detected equipment feature groups: " + diagnostics.FeatureGroupsDetected);
        sb.AppendLine("Duplicate designation occurrences: " + diagnostics.DuplicateDesignationRows);
        sb.AppendLine("Exact cross-sheet duplicates removed: " + diagnostics.DuplicateCrossSheetRows);
        sb.AppendLine("Skipped non-empty designation cells: " + diagnostics.SkippedNonEmptyRows);
        sb.AppendLine();

        sb.AppendLine("WORKSHEETS");
        foreach (var item in diagnostics.WorksheetDetails) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("DETECTED DETAILED FEATURE GROUPS");
        foreach (var item in diagnostics.FeatureGroups) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("PROVISIONAL CANDIDATES: SOURCE TABLE HAS NO L COLUMN (<10%, NOT AUTO-SELECTED)");
        foreach (var item in diagnostics.ProvisionalNoAirflow) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("REJECTED: L FIELD EXISTS BUT THIS ROW HAS NO POSITIVE L");
        foreach (var item in diagnostics.RejectedNoAirflow) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("RESERVE / ADDITIONAL EQUIPMENT FROM REMARKS");
        foreach (var item in diagnostics.NotesEquipment) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("IMPORTED");
        foreach (var item in diagnostics.Imported) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("CONTINUATION ROWS MERGED");
        foreach (var item in diagnostics.Continuations) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("DUPLICATES");
        foreach (var item in diagnostics.Duplicates) sb.AppendLine(item);

        sb.AppendLine();
        sb.AppendLine("SKIPPED NON-EMPTY DESIGNATION CELLS");
        foreach (var item in diagnostics.Skipped) sb.AppendLine(item);

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        diagnostics.LogPath = path;
    }
}
