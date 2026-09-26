using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using NGraph.Core.ModelSchemes;
using NGraph.ViewModels;

namespace NGraph.Views;

public sealed partial class ParameterSchemeView
{
    private readonly ParameterSchemeViewModel _viewModel;
    private readonly Action<SchemeSymbolRow, string>? _renderPreview;
    private readonly Queue<SchemeSymbolRow> _previewQueue = new();
    private readonly HashSet<SchemeSymbolRow> _queued = new();
    private readonly DispatcherTimer _previewTimer;
    private bool _closed;
    private bool _rendering;
    private string _previewViewName;
    public ParameterSchemeView(ParameterSchemeViewModel viewModel, Action<SchemeSymbolRow, string>? renderPreview = null)
    {
        _viewModel = viewModel; _renderPreview = renderPreview; _previewViewName = viewModel.ViewName.Trim();
        _previewTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _previewTimer.Tick += RenderNext;
        DataContext = viewModel;
        DialogTheme.Prepare(this);
        InitializeComponent();
        Closed += (_, _) => { _closed = true; _previewTimer.Stop(); _previewQueue.Clear(); _queued.Clear(); };
    }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ReloadSymbols();
        if (_viewModel.CanBuild) DialogResult = true;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Equipment_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is SchemeSymbolRow row) QueuePreview(row);
    }
    private void QueuePreview(SchemeSymbolRow row)
    {
        if (_renderPreview == null || _closed || row.PreviewAttempted || !_queued.Add(row)) return;
        _previewQueue.Enqueue(row); _previewTimer.Start();
    }
    private void QueueVisiblePreviews()
    {
        if (_closed) return;
        foreach (var row in _viewModel.Symbols)
            if (EquipmentPreview.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow) QueuePreview(row);
    }
    private void RenderNext(object? sender, EventArgs e)
    {
        if (_rendering) return;
        if (_closed || !IsVisible || _previewQueue.Count == 0) { _previewTimer.Stop(); return; }
        var row = _previewQueue.Dequeue(); _queued.Remove(row);
        if (!_viewModel.Symbols.Contains(row) || row.PreviewAttempted || string.IsNullOrWhiteSpace(_viewModel.ViewName)
            || EquipmentPreview.ItemContainerGenerator.ContainerFromItem(row) == null) return;
        // Только UI-поток Revit и только видимые строки: не запускаем Revit API в Task.Run.
        _previewTimer.Stop(); _rendering = true;
        try { IsEnabled = false; Cursor = Cursors.Wait; _renderPreview?.Invoke(row, _viewModel.ViewName.Trim()); }
        catch (Exception ex) { row.SetPreview(null, ex.Message); }
        finally { Cursor = null; IsEnabled = true; _rendering = false; if (!_closed && _previewQueue.Count > 0) _previewTimer.Start(); }
    }
    private void ReloadIni_Click(object sender, RoutedEventArgs e) => ReloadIni();
    private void ReloadIni()
    {
        _previewTimer.Stop(); _previewQueue.Clear(); _queued.Clear();
        try { _viewModel.ReloadSymbols(); IniActionStatus.Text = "INI перечитаны."; }
        catch (Exception ex) { IniActionStatus.Text = ex.Message; }
        Dispatcher.BeginInvoke(new Action(QueueVisiblePreviews), DispatcherPriority.Loaded);
    }
    private void ViewName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_previewViewName == _viewModel.ViewName.Trim()) return;
        _previewViewName = _viewModel.ViewName.Trim();
        _viewModel.InvalidateImages(); QueueVisiblePreviews();
    }
    private void OpenIniFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_viewModel.IniDirectory);
            Process.Start(new ProcessStartInfo(_viewModel.IniDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) { IniActionStatus.Text = ex.Message; }
    }
    private void EditIni_Click(object sender, RoutedEventArgs e)
    {
        _previewTimer.Stop();
        try
        {
            var selected = EquipmentPreview.SelectedItem as SchemeSymbolRow;
            var path = selected?.IniPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                var picker = new OpenFileDialog { Title = "Выберите INI для редактирования", Filter = "Настройки INI (*.ini)|*.ini", CheckFileExists = true,
                    InitialDirectory = Directory.Exists(_viewModel.IniDirectory) ? _viewModel.IniDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                if (picker.ShowDialog(this) != true) return;
                path = picker.FileName;
            }
            var editor = new SchemeIniEditorView(SchemeIniFile.Open(path)) { Owner = this };
            if (editor.ShowDialog() == true) ReloadIni();
        }
        catch (Exception ex) { IniActionStatus.Text = ex.Message; }
        finally { if (_previewQueue.Count > 0) _previewTimer.Start(); QueueVisiblePreviews(); }
    }
}
