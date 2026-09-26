using System.IO;
using System.Windows.Media.Imaging;
using NGraph.ViewModels;

namespace NGraph.Core.ModelSchemes;

/// <summary>Изображения реальных экземпляров УГО. Временные виды и экземпляры всегда откатываются.</summary>
internal sealed class SchemeSymbolPreviewService
{
    private readonly Document _document;
    private readonly string _iniDirectory;
    public SchemeSymbolPreviewService(Document document, string iniDirectory) { _document = document; _iniDirectory = iniDirectory; }

    public IReadOnlyList<SchemeSymbolRow> ReadMappings(IReadOnlyList<SchemeSourceElement> elements)
    {
        var rows = elements.Select(e => new SchemeSymbolRow(e)).ToList();
        SchemeIniCatalog catalog; SchemeSymbolResolver resolver;
        try { catalog = new SchemeIniCatalog(_iniDirectory); resolver = new SchemeSymbolResolver(_document); }
        catch (Exception ex) { foreach (var row in rows) row.MappingError = ex.Message; return rows; }
        foreach (var row in rows)
        {
            try
            {
                row.IniPath = catalog.FindFile(row.IniGroup);
                var values = catalog.ReadSection(row.IniGroup, row.Position);
                row.IniValues = string.Join(Environment.NewLine, values.Select(v => v.Key + " = " + v.Value));
                row.Symbol = catalog.Resolve(row.IniGroup, row.Position);
                row.FamilyName = resolver.Resolve(row.Symbol).FamilyName;
            }
            catch (Exception ex) { row.MappingError = ex.Message; }
        }
        return rows;
    }

    public void Render(SchemeSymbolRow row, string viewName)
    {
        if (!row.IsValid) { row.SetPreview(null, row.MappingError); return; }
        try { row.SetPreview(RenderInstance(row, viewName), "УГО с параметрами выбранного оборудования, масштаб вида 1:1"); }
        catch (Exception ex) { row.SetPreview(null, "Изображение недоступно: " + ex.Message); }
    }

    private BitmapSource RenderInstance(SchemeSymbolRow row, string requestedName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "NGraph-Preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // Экспорт идёт после внутреннего Commit, но внутри группы, которую никогда не сохраняем.
            using var group = new TransactionGroup(_document, "Предпросмотр УГО NGraph");
            if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("Не удалось начать предпросмотр.");
            try
            {
                var symbol = new SchemeSymbolResolver(_document).Resolve(row.Symbol);
                var viewType = new FilteredElementCollector(_document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                    .FirstOrDefault(v => v.ViewFamily == ViewFamily.Drafting)
                    ?? throw new InvalidOperationException("Нет типа чертёжного вида.");
                var name = ModelSchemeBuilder.GetViewName(_document, requestedName);
                ViewDrafting view; double width, height;
                using (var tx = new Transaction(_document, "Временное УГО"))
                {
                    tx.Start();
                    var failures = new PreviewFailures();
                    tx.SetFailureHandlingOptions(tx.GetFailureHandlingOptions().SetClearAfterRollback(true).SetFailuresPreprocessor(failures));
                    view = ViewDrafting.Create(_document, viewType.Id); view.Name = name; view.Scale = 1;
                    if (!symbol.IsActive) symbol.Activate();
                    _document.Regenerate();
                    var instance = _document.Create.NewFamilyInstance(XYZ.Zero, symbol, view);
                    // Тот же метод, что заполняет NS_, ADSK_ и CJ_ при окончательном построении.
                    ModelSchemeBuilder.SetEquipmentParameters(instance, row.Source, name);
                    _document.Regenerate();
                    var bounds = instance.get_BoundingBox(view) ?? throw new InvalidOperationException("УГО не имеет видимой геометрии.");
                    width = bounds.Max.X - bounds.Min.X; height = bounds.Max.Y - bounds.Min.Y;
                    if (tx.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit отменил временное размещение. " + failures.Message);
                }
                using var options = new ImageExportOptions {
                    ExportRange = ExportRange.SetOfViews, FilePath = Path.Combine(directory, "ugo"),
                    ZoomType = ZoomFitType.FitToPage, PixelSize = 384,
                    FitDirection = width >= height ? FitDirectionType.Horizontal : FitDirectionType.Vertical,
                    HLRandWFViewsFileType = ImageFileType.PNG, ShadowViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_96 };
                options.SetViewsAndSheets(new List<ElementId> { view.Id });
                _document.ExportImage(options);
                var file = Directory.GetFiles(directory, "*.png").SingleOrDefault()
                    ?? throw new InvalidOperationException("Revit не создал изображение УГО.");
                // OnLoad освобождает PNG до удаления временной папки; Freeze позволяет безопасно хранить снимок в WPF.
                var image = new BitmapImage();
                using (var stream = File.OpenRead(file))
                {
                    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit();
                }
                image.Freeze();
                return image;
            }
            finally
            {
                if (group.GetStatus() == TransactionStatus.Started && group.RollBack() != TransactionStatus.RolledBack)
                    throw new InvalidOperationException("Не удалось откатить временный предпросмотр.");
            }
        }
        finally
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class PreviewFailures : IFailuresPreprocessor
    {
        public string Message { get; private set; } = "";
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var messages = failuresAccessor.GetFailureMessages();
            if (messages.Count == 0) return FailureProcessingResult.Continue;
            Message = string.Join("; ", messages.Select(m => m.GetDescriptionText()));
            return FailureProcessingResult.ProceedWithRollBack;
        }
    }
}
