using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using NGraph.Core.ModelSchemes;

namespace NGraph.ViewModels;

/// <summary>Один экземпляр оборудования и его фактическое соответствие из INI.</summary>
public sealed class SchemeSymbolRow : INotifyPropertyChanged
{
    public SchemeSourceElement Source { get; }
    public string Id => Source.Id;
    public string PanelName => Source.PanelName;
    public string Position => Source.Position;
    public string IniGroup => Source.IniGroup;
    public string IniSection => "[" + Position + "]";
    public string IniPath { get; set; } = "";
    public string IniFileName => IniPath.Length == 0 ? IniGroup + ".ini" : Path.GetFileName(IniPath);
    public string IniValues { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string MappingError { get; set; } = "";
    public bool IsValid => MappingError.Length == 0 && Symbol.Length > 0;
    public string MappingStatus => MappingError.Length == 0 ? "Соответствие найдено" : MappingError;
    public ImageSource? Preview { get; private set; }
    public string PreviewStatus { get; private set; } = "Загрузка изображения…";
    public bool PreviewAttempted { get; private set; }
    public SchemeSymbolRow(SchemeSourceElement source) { Source = source; }
    public void SetPreview(ImageSource? image, string status)
    {
        Preview = image; PreviewStatus = status; PreviewAttempted = true; NotifyPreview();
    }
    public void ResetPreview()
    {
        Preview = null; PreviewAttempted = false; PreviewStatus = "Загрузка изображения…"; NotifyPreview();
    }
    private void NotifyPreview()
    {
        foreach (var name in new[] { nameof(Preview), nameof(PreviewStatus), nameof(PreviewAttempted) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
