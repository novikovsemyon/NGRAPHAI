using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using HOVS.Model;

namespace HOVS.Plugin;

public partial class HovsSchemaWizardWindow : Window
{
    private readonly XLWorkbook _workbook;
    private readonly string _workbookPath;
    private bool _initializing;
    private readonly ObservableCollection<HovsColumnMappingRow> _rows =
        new ObservableCollection<HovsColumnMappingRow>();

    public HovsSchema? ResultSchema { get; private set; }

    public HovsSchemaWizardWindow(
        XLWorkbook workbook,
        HovsSchema suggested,
        string workbookPath)
    {
        _workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
        _workbookPath = workbookPath ?? "";

        NGraph.Views.DialogTheme.Prepare(this);
        InitializeComponent();

        SemanticColumn.ItemsSource = BuildSemanticOptions();
        ColumnsGrid.ItemsSource = _rows;

        _initializing = true;
        SheetComboBox.ItemsSource = _workbook.Worksheets.Select(x => x.Name).ToList();
        SheetComboBox.SelectedItem = _workbook.Worksheets.Any(x =>
            string.Equals(x.Name, suggested.WorksheetName, StringComparison.OrdinalIgnoreCase))
            ? suggested.WorksheetName
            : _workbook.Worksheets.First().Name;
        _initializing = false;

        ProfileNameTextBox.Text = string.IsNullOrWhiteSpace(suggested.ProfileName)
            ? BuildDefaultProfileName((string?)SheetComboBox.SelectedItem)
            : suggested.ProfileName;

        LoadSchema(suggested);
    }

    private void SheetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        var sheetName = SheetComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(sheetName)) return;

        var analysis = HovsSchemaAnalyzer.AnalyzeSheet(_workbook.Worksheet(sheetName));
        ProfileNameTextBox.Text = BuildDefaultProfileName(sheetName);
        LoadSchema(analysis.Schema);
    }

    private void Rebuild_Click(object sender, RoutedEventArgs e)
    {
        var sheetName = SheetComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(sheetName)) return;

        int headerRow;
        int lastHeaderRow;
        int firstDataRow;
        if (!TryReadRows(out headerRow, out lastHeaderRow, out firstDataRow)) return;

        var schema = BuildSchemaForRows(
            _workbook.Worksheet(sheetName),
            headerRow,
            lastHeaderRow,
            firstDataRow);
        LoadSchema(schema);
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        var sheetName = (SheetComboBox.SelectedItem as string ?? "").Trim();
        if (sheetName.Length == 0)
        {
            MessageBox.Show("Выберите лист ХОВС.", "NGrapfAI — Структура ХОВС", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int headerRow;
        int lastHeaderRow;
        int firstDataRow;
        if (!TryReadRows(out headerRow, out lastHeaderRow, out firstDataRow)) return;

        var schema = new HovsSchema
        {
            WorksheetName = sheetName,
            HeaderRow = headerRow,
            LastHeaderRow = lastHeaderRow,
            FirstDataRow = firstDataRow,
            ProfileName = string.IsNullOrWhiteSpace(ProfileNameTextBox.Text)
                ? BuildDefaultProfileName(sheetName)
                : ProfileNameTextBox.Text.Trim(),
            ProfileConfidence = 1.0,
            FromProfile = false
        };

        foreach (var row in _rows)
        {
            ColumnSemanticKind kind;
            int slot;
            ParseSemantic(row.SelectedSemantic, out kind, out slot);
            schema.Columns.Add(new ColumnSemantic
            {
                ColumnNumber = row.ColumnNumber,
                Header = row.Header ?? "",
                Kind = kind,
                Slot = slot,
                Role = row.Role ?? ""
            });
        }

        if (!schema.Columns.Any(x => x.Kind == ColumnSemanticKind.InstallationName))
        {
            MessageBox.Show(
                "Укажите хотя бы один столбец «Обозначение установки».",
                "NGrapfAI — Структура ХОВС",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!schema.Columns.Any(x => x.Kind == ColumnSemanticKind.AirFlow))
        {
            var answer = MessageBox.Show(
                "Столбец «Расход L» не размечен. По принятому правилу такие строки смогут быть только кандидатами с вероятностью менее 10% и не будут подтверждёнными установками.\n\nСохранить профиль без L?",
                "NGrapfAI — Структура ХОВС",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
        }

        ResultSchema = schema;
        DialogResult = true;
    }

    private void LoadSchema(HovsSchema schema)
    {
        var sheetName = schema.WorksheetName;
        if (string.IsNullOrWhiteSpace(sheetName) ||
            !_workbook.Worksheets.Any(x => string.Equals(x.Name, sheetName, StringComparison.OrdinalIgnoreCase)))
            return;

        _initializing = true;
        SheetComboBox.SelectedItem = sheetName;
        _initializing = false;

        HeaderRowTextBox.Text = Math.Max(1, schema.HeaderRow).ToString(CultureInfo.InvariantCulture);
        LastHeaderRowTextBox.Text = Math.Max(Math.Max(1, schema.HeaderRow), schema.LastHeaderRow).ToString(CultureInfo.InvariantCulture);
        FirstDataRowTextBox.Text = Math.Max(Math.Max(1, schema.LastHeaderRow) + 1, schema.FirstDataRow).ToString(CultureInfo.InvariantCulture);

        _rows.Clear();
        var worksheet = _workbook.Worksheet(sheetName);
        var used = worksheet.RangeUsed();
        if (used == null) return;

        var byColumn = schema.Columns.ToDictionary(x => x.ColumnNumber, x => x);
        var firstData = Math.Max(1, schema.FirstDataRow);
        for (var column = used.FirstColumn().ColumnNumber(); column <= used.LastColumn().ColumnNumber(); column++)
        {
            ColumnSemantic? mapping;
            byColumn.TryGetValue(column, out mapping);

            var header = mapping?.Header;
            if (string.IsNullOrWhiteSpace(header))
                header = HovsSchemaAnalyzer.BuildCompositeHeader(
                    worksheet,
                    Math.Max(1, schema.HeaderRow),
                    Math.Max(Math.Max(1, schema.HeaderRow), schema.LastHeaderRow),
                    column);

            var kind = mapping?.Kind ?? ColumnSemanticKind.Ignore;
            var slot = mapping?.Slot ?? 0;

            _rows.Add(new HovsColumnMappingRow
            {
                ColumnNumber = column,
                ColumnLabel = ExcelColumnName(column) + " (" + column + ")",
                Header = header ?? "",
                Samples = BuildSamples(worksheet, column, firstData),
                SelectedSemantic = FormatSemantic(kind, slot),
                Role = mapping?.Role ?? HovsSchemaAnalyzer.InferRole(header ?? "")
            });
        }

        UpdateStatus();
    }

    private HovsSchema BuildSchemaForRows(
        IXLWorksheet worksheet,
        int headerRow,
        int lastHeaderRow,
        int firstDataRow)
    {
        var schema = new HovsSchema
        {
            WorksheetName = worksheet.Name,
            HeaderRow = headerRow,
            LastHeaderRow = lastHeaderRow,
            FirstDataRow = firstDataRow
        };

        var used = worksheet.RangeUsed();
        if (used == null) return schema;

        var nextSlots = new Dictionary<ColumnSemanticKind, int>();
        var repeatedGroupSlots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = used.FirstColumn().ColumnNumber(); column <= used.LastColumn().ColumnNumber(); column++)
        {
            var header = HovsSchemaAnalyzer.BuildCompositeHeader(
                worksheet,
                headerRow,
                lastHeaderRow,
                column);
            int slot;
            var kind = HovsSchemaAnalyzer.GuessKind(header, out slot);
            if (slot <= 0 && IsRepeated(kind))
            {
                var groupKey = HovsSchemaAnalyzer.BuildRepeatedGroupKey(kind, header);
                if (!string.IsNullOrWhiteSpace(groupKey) &&
                    repeatedGroupSlots.TryGetValue(groupKey, out var existingSlot))
                {
                    slot = existingSlot;
                }
                else
                {
                    int previous;
                    nextSlots.TryGetValue(kind, out previous);
                    slot = previous + 1;
                    nextSlots[kind] = slot;
                    if (!string.IsNullOrWhiteSpace(groupKey))
                        repeatedGroupSlots[groupKey] = slot;
                }
            }

            schema.Columns.Add(new ColumnSemantic
            {
                ColumnNumber = column,
                Header = header,
                Kind = kind,
                Slot = slot,
                Role = HovsSchemaAnalyzer.InferRole(header)
            });
        }

        return schema;
    }

    private bool TryReadRows(out int headerRow, out int lastHeaderRow, out int firstDataRow)
    {
        headerRow = 0;
        lastHeaderRow = 0;
        firstDataRow = 0;

        if (!int.TryParse(HeaderRowTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out headerRow) || headerRow <= 0 ||
            !int.TryParse(LastHeaderRowTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out lastHeaderRow) || lastHeaderRow < headerRow ||
            !int.TryParse(FirstDataRowTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out firstDataRow) || firstDataRow <= lastHeaderRow)
        {
            MessageBox.Show(
                "Проверьте номера строк: начало заголовка > 0, конец заголовка не раньше начала, первая строка данных должна идти после заголовков.",
                "NGrapfAI — Структура ХОВС",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private void UpdateStatus()
    {
        var mapped = _rows.Count(x => x.SelectedSemantic != "Не использовать");
        var profileHint = "";
        if (ResultSchema != null && ResultSchema.ProfileConfidence > 0.0)
            profileHint = " | профиль " + Math.Round(ResultSchema.ProfileConfidence * 100.0).ToString("0") + "%";

        StatusText.Text =
            "Столбцов на листе: " + _rows.Count +
            " | предварительно размечено: " + mapped + profileHint;
    }

    private string BuildDefaultProfileName(string? worksheetName)
    {
        var file = Path.GetFileNameWithoutExtension(_workbookPath);
        if (string.IsNullOrWhiteSpace(file)) file = "ХОВС";
        return file + " — " + (string.IsNullOrWhiteSpace(worksheetName) ? "Лист" : worksheetName);
    }

    private static string BuildSamples(IXLWorksheet worksheet, int column, int firstDataRow)
    {
        var used = worksheet.RangeUsed();
        if (used == null) return "";

        var samples = new List<string>();
        var lastRow = Math.Min(used.LastRow().RowNumber(), Math.Max(firstDataRow, 1) + 80);
        for (var row = Math.Max(1, firstDataRow); row <= lastRow && samples.Count < 3; row++)
        {
            var value = (worksheet.Cell(row, column).GetString() ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (value.Length > 70) value = value.Substring(0, 67) + "...";
            if (!samples.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                samples.Add(value);
        }

        return string.Join("  •  ", samples);
    }

    private static List<string> BuildSemanticOptions()
    {
        var result = new List<string>
        {
            "Не использовать",
            "Обозначение установки",
            "Тип установки",
            "Помещение",
            "Количество систем",
            "Расход L",
            "L наружный",
            "L рециркуляционный"
        };

        AddSlots(result, "ТО", 12);
        AddSlots(result, "Фильтр", 8);
        AddSlots(result, "Вентилятор", 8);
        AddSlots(result, "Рекуператор", 4);
        AddSlots(result, "Увлажнитель", 4);
        result.Add("Примечание");
        AddSlots(result, "Другое оборудование", 12);
        return result;
    }

    private static void AddSlots(List<string> values, string prefix, int count)
    {
        for (var i = 1; i <= count; i++) values.Add(prefix + i);
    }

    private static string FormatSemantic(ColumnSemanticKind kind, int slot)
    {
        switch (kind)
        {
            case ColumnSemanticKind.InstallationName: return "Обозначение установки";
            case ColumnSemanticKind.InstallationType: return "Тип установки";
            case ColumnSemanticKind.Room: return "Помещение";
            case ColumnSemanticKind.SystemCount: return "Количество систем";
            case ColumnSemanticKind.AirFlow: return "Расход L";
            case ColumnSemanticKind.OutdoorAirFlow: return "L наружный";
            case ColumnSemanticKind.RecirculationAirFlow: return "L рециркуляционный";
            case ColumnSemanticKind.HeatExchanger: return "ТО" + Math.Max(1, slot);
            case ColumnSemanticKind.Filter: return "Фильтр" + Math.Max(1, slot);
            case ColumnSemanticKind.Fan: return "Вентилятор" + Math.Max(1, slot);
            case ColumnSemanticKind.Recuperator: return "Рекуператор" + Math.Max(1, slot);
            case ColumnSemanticKind.Humidifier: return "Увлажнитель" + Math.Max(1, slot);
            case ColumnSemanticKind.Notes: return "Примечание";
            case ColumnSemanticKind.OtherEquipment: return "Другое оборудование" + Math.Max(1, slot);
            default: return "Не использовать";
        }
    }

    private static void ParseSemantic(string? value, out ColumnSemanticKind kind, out int slot)
    {
        kind = ColumnSemanticKind.Ignore;
        slot = 0;
        var text = (value ?? "").Trim();

        if (text == "Обозначение установки") kind = ColumnSemanticKind.InstallationName;
        else if (text == "Тип установки") kind = ColumnSemanticKind.InstallationType;
        else if (text == "Помещение") kind = ColumnSemanticKind.Room;
        else if (text == "Количество систем") kind = ColumnSemanticKind.SystemCount;
        else if (text == "Расход L") kind = ColumnSemanticKind.AirFlow;
        else if (text == "L наружный") kind = ColumnSemanticKind.OutdoorAirFlow;
        else if (text == "L рециркуляционный") kind = ColumnSemanticKind.RecirculationAirFlow;
        else if (text == "Примечание") kind = ColumnSemanticKind.Notes;
        else if (TryParseSlot(text, "ТО", out slot)) kind = ColumnSemanticKind.HeatExchanger;
        else if (TryParseSlot(text, "Фильтр", out slot)) kind = ColumnSemanticKind.Filter;
        else if (TryParseSlot(text, "Вентилятор", out slot)) kind = ColumnSemanticKind.Fan;
        else if (TryParseSlot(text, "Рекуператор", out slot)) kind = ColumnSemanticKind.Recuperator;
        else if (TryParseSlot(text, "Увлажнитель", out slot)) kind = ColumnSemanticKind.Humidifier;
        else if (TryParseSlot(text, "Другое оборудование", out slot)) kind = ColumnSemanticKind.OtherEquipment;
    }

    private static bool TryParseSlot(string value, string prefix, out int slot)
    {
        slot = 0;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return int.TryParse(
            value.Substring(prefix.Length).Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out slot) && slot > 0;
    }

    private static bool IsRepeated(ColumnSemanticKind kind)
    {
        return kind == ColumnSemanticKind.HeatExchanger ||
               kind == ColumnSemanticKind.Filter ||
               kind == ColumnSemanticKind.Fan ||
               kind == ColumnSemanticKind.Recuperator ||
               kind == ColumnSemanticKind.Humidifier ||
               kind == ColumnSemanticKind.OtherEquipment;
    }

    private static string ExcelColumnName(int columnNumber)
    {
        var n = columnNumber;
        var name = "";
        while (n > 0)
        {
            n--;
            name = (char)('A' + (n % 26)) + name;
            n /= 26;
        }
        return name;
    }
}

public sealed class HovsColumnMappingRow
{
    public int ColumnNumber { get; set; }
    public string ColumnLabel { get; set; } = "";
    public string Header { get; set; } = "";
    public string Samples { get; set; } = "";
    public string SelectedSemantic { get; set; } = "Не использовать";
    public string Role { get; set; } = "";
}
