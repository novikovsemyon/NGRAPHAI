using Autodesk.Revit.DB.Mechanical;
using NGraph.Core.FunctionalScheme;
using NGraph.ViewModels;

namespace NGraph.Core.CableRouting;

/// <summary>Проверяет снимок до записи и изменяет маркировку и длины одной транзакцией.</summary>
internal sealed class RevitCableTransfer
{
    private readonly Document _document;
    private readonly RevitCableSource _source;
    private readonly Dictionary<long, string> _lengthValues = new();
    public string ViewKey => _document.ProjectInformation.UniqueId + "|" + _source.View.UniqueId;

    public RevitCableTransfer(Document document, RevitCableSource source) { _document = document; _source = source; }

    public DuctCableViewModel CreateViewModel(string savedParameter)
    {
        var rows = new List<CableRouteRow>();
        foreach (var connection in _source.Connections)
        {
            CablePath? path = null; var error = connection.Error; var previous = "";
            if (error.Length == 0)
            {
                try
                {
                    var cable = RequireElement(connection.Cable.Id);
                    var length = RequireLength(cable); previous = length.AsValueString() ?? Value(length);
                    _lengthValues[connection.Cable.Id] = Value(length);
                    if (RequireElement(connection.BeginId).ViewSpecific || RequireElement(connection.EndId).ViewSpecific)
                        throw new InvalidOperationException("NS_ElementId должен указывать на оборудование модели, а не на элемент чертёжного вида.");
                    path = _source.Graph.Find(connection.BeginId, connection.EndId);
                }
                catch (Exception ex) { error = ex.Message; }
            }
            rows.Add(new CableRouteRow(connection, path, previous, error));
        }
        var snapshots = new List<CableDuctSnapshot>(); var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in _source.Ducts)
        {
            var snapshot = new CableDuctSnapshot { Id = pair.Key, System = _source.DuctSystems[pair.Key], SystemId = _source.DuctSystemIds[pair.Key] };
            foreach (var p in TextParameters(pair.Value))
            { var key = p.Id.ToString(); snapshot.Values[key] = p.AsString() ?? ""; definitions[key] = p.Definition.Name; }
            snapshots.Add(snapshot);
        }
        var choices = definitions.OrderBy(p => p.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new CableChoice(p.Key, p.Value, $"{p.Value} · ID {p.Key} · {snapshots.Count(s => s.Values.ContainsKey(p.Key))}/{snapshots.Count}" )).ToList();
        var views = new FilteredElementCollector(_document).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan ||
                v.ViewType == ViewType.EngineeringPlan || v.ViewType == ViewType.Section || v.ViewType == ViewType.Elevation))
            .OrderBy(v => v.Name).Select(v => new CableChoice(v.Id.ToString(), v.Name)).ToList();
        var types = new FilteredElementCollector(_document).OfCategory(BuiltInCategory.OST_DuctTags).WhereElementIsElementType()
            .OfType<FamilySymbol>().OrderBy(s => s.FamilyName).ThenBy(s => s.Name)
            .Select(s => new CableChoice(s.Id.ToString(), s.FamilyName + " : " + s.Name)).ToList();
        return new DuctCableViewModel(_source.View.Name, rows, snapshots, choices, views, types, savedParameter);
    }

    public string Apply(DuctCableViewModel vm)
    {
        vm.Refresh();
        if (!vm.CanWrite || vm.Parameter == null) throw new InvalidOperationException(vm.Error);
        var ductWrites = vm.Writes.Select(w => (Write: w, Parameter: RequireText(RequireElement(w.Id), vm.Parameter.Key))).ToList();
        foreach (var pair in ductWrites)
            if ((pair.Parameter.AsString() ?? "") != pair.Write.OldValue) throw new InvalidOperationException($"Значение воздуховода {pair.Write.Id} изменилось. Заново откройте команду.");
        var cableWrites = vm.Routes.Where(r => r.Included).Select(r => (Row: r, Parameter: RequireLength(RequireElement(r.Id)))).ToList();
        foreach (var pair in cableWrites)
            if (Value(pair.Parameter) != _lengthValues[pair.Row.Id]) throw new InvalidOperationException($"Длина кабеля {pair.Row.Number} изменена. Заново откройте команду.");

        using var transaction = new Transaction(_document, "NGraph — Кабель по воздуховодам");
        transaction.Start();
        var failures = new TransferFailures();
        transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions().SetFailuresPreprocessor(failures).SetClearAfterRollback(true));
        var tags = 0; var skipped = 0;
        try
        {
            foreach (var pair in ductWrites.Where(p => p.Write.OldValue != p.Write.NewValue))
                if (!pair.Parameter.Set(pair.Write.NewValue)) throw new InvalidOperationException($"Не записан параметр воздуховода {pair.Write.Id}.");
            foreach (var pair in cableWrites) SetLength(pair.Parameter, CableWritePlanner.RoundedMeters(pair.Row.Path!.Meters, vm.RoundingStep));
            if (vm.CreateTags) (tags, skipped) = CreateTags(vm);
            if (transaction.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException("Запись отменена Revit. " + failures.Message);
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            throw;
        }
        return $"Обновлено кабелей: {cableWrites.Count}.\nВоздуховодов с изменениями: {ductWrites.Count(p => p.Write.OldValue != p.Write.NewValue)}.\n" +
            (vm.CreateTags ? $"Добавлено марок: {tags}. Уже маркированы или не видны на выбранном виде: {skipped}." : "");
    }

    private (int Created, int Skipped) CreateTags(DuctCableViewModel vm)
    {
        var view = (View)RequireElement(long.Parse(vm.TagView!.Key));
        var symbol = (FamilySymbol)RequireElement(long.Parse(vm.TagType!.Key));
        if (!symbol.IsActive) { symbol.Activate(); _document.Regenerate(); }
        var visible = new HashSet<ElementId>(new FilteredElementCollector(_document, view.Id).OfClass(typeof(Duct)).ToElementIds());
        var tagged = new HashSet<ElementId>();
        foreach (var tag in new FilteredElementCollector(_document, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
                     .Where(t => t.GetTypeId() == symbol.Id))
        {
#if REVIT2022_OR_GREATER
            foreach (var id in tag.GetTaggedLocalElementIds()) tagged.Add(id);
#else
            tagged.Add(tag.TaggedLocalElementId);
#endif
        }
        int created = 0, skipped = 0;
        foreach (var write in vm.Writes.Where(w => w.HasRoute))
        {
            var element = RequireElement(write.Id);
            if (!visible.Contains(element.Id) || tagged.Contains(element.Id) || element.Location is not LocationCurve location)
            { skipped++; continue; }
            var point = location.Curve.Evaluate(0.5, true);
            IndependentTag.Create(_document, symbol.Id, view.Id, new Reference(element), false, TagOrientation.Horizontal, point);
            created++;
        }
        return (created, skipped);
    }

    private Element RequireElement(long id) => _document.GetElement(RevitElementAccess.CreateId(id))
        ?? throw new InvalidOperationException($"Элемент {id} отсутствует в текущем документе. Проверьте NS_ElementId.");
    private static IEnumerable<Parameter> TextParameters(Element e) => e.Parameters.Cast<Parameter>()
        .Where(p => !p.IsReadOnly && p.StorageType == StorageType.String);
    private static Parameter RequireText(Element e, string key) => TextParameters(e).SingleOrDefault(p => p.Id.ToString() == key)
        ?? throw new InvalidOperationException($"У воздуховода {e.Id} выбранный параметр недоступен для записи.");
    private static Parameter RequireLength(Element e)
    {
        var parameters = e.GetParameters(Const.Param_CJ_Lenght);
        if (parameters.Count != 1 || parameters[0].IsReadOnly) throw new InvalidOperationException($"У зелёной точки {e.Id} недоступен однозначный параметр {Const.Param_CJ_Lenght}.");
        var p = parameters[0];
        if (p.StorageType != StorageType.Integer && p.StorageType != StorageType.String &&
            !(p.StorageType == StorageType.Double && (IsLength(p) || IsNumber(p))))
            throw new InvalidOperationException($"У зелёной точки {e.Id} неподдерживаемый тип параметра длины.");
        return p;
    }
    private static bool IsLength(Parameter p)
    {
#if REVIT2022_OR_GREATER
        return p.Definition.GetDataType() == SpecTypeId.Length;
#else
        return p.Definition.ParameterType == ParameterType.Length;
#endif
    }
    private static bool IsNumber(Parameter p)
    {
#if REVIT2022_OR_GREATER
        return p.Definition.GetDataType() == SpecTypeId.Number;
#else
        return p.Definition.ParameterType == ParameterType.Number;
#endif
    }
    private static string Value(Parameter p) => p.StorageType == StorageType.String ? p.AsString() ?? "" :
        p.StorageType == StorageType.Integer ? p.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture) :
        p.AsDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    private static void SetLength(Parameter p, int meters)
    {
        bool success;
        if (p.StorageType == StorageType.Integer) { if (p.AsInteger() == meters) return; success = p.Set(meters); }
        else if (p.StorageType == StorageType.String)
        { var value = meters.ToString(System.Globalization.CultureInfo.InvariantCulture); if (p.AsString() == value) return; success = p.Set(value); }
        else { var value = IsLength(p) ? meters/0.3048 : meters; if (Math.Abs(p.AsDouble()-value) < 1e-9) return; success = p.Set(value); }
        if (!success) throw new InvalidOperationException($"Не записана длина зелёной точки {p.Element.Id}.");
    }

    private sealed class TransferFailures : IFailuresPreprocessor
    {
        public string Message { get; private set; } = "";
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            var messages = accessor.GetFailureMessages();
            if (messages.Count == 0) return FailureProcessingResult.Continue;
            Message = string.Join("\n", messages.Select(m => m.GetDescriptionText()).Distinct());
            return FailureProcessingResult.ProceedWithRollBack;
        }
    }
}
