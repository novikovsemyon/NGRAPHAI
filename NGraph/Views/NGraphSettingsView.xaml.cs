using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NGraph.Core;
using NGraph.ViewModels;

namespace NGraph.Views;

/// <summary>Редактирует копию настроек: отмена и закрытие окна не меняют сохранённые значения.</summary>
public sealed partial class NGraphSettingsView
{
    private readonly List<string> _viewNames;

    public NGraphSettingsView(NGraphSettingsViewModel viewModel)
    {
        NGraph.Views.DialogTheme.Prepare(this);
        InitializeComponent();
        // Только чтение Revit API, пока команда находится в допустимом API-контексте.
        _viewNames = new FilteredElementCollector(viewModel.Doc)
            .OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>()
            .Where(view => !view.IsTemplate).Select(view => view.Name).OrderBy(name => name).ToList();
        ElementsView.ItemsSource = FsaView.ItemsSource = EquipmentView.ItemsSource = _viewNames;
        ProjectName.Text = "Проект: " + viewModel.Doc.Title;
        SettingsPath.Text = UserSettings.FilePath;
        // Обходим только области и типоразмеры: параметры каталога не требуют сканирования всей модели.
        var elements = new FilteredElementCollector(viewModel.Doc).OfClass(typeof(FilledRegion)).ToElements()
            .Concat(new FilteredElementCollector(viewModel.Doc).OfClass(typeof(FilledRegionType)).ToElements());
        var names = elements.SelectMany(x => x.Parameters.Cast<Parameter>()).Where(x => x.StorageType == StorageType.String)
            .Select(x => x.Definition.Name).Distinct().OrderBy(x => x).ToList();
        RegionNameParameter.ItemsSource = RegionGroupParameter.ItemsSource = RegionCodeParameter.ItemsSource = names;
        RegionType.ItemsSource = new FilteredElementCollector(viewModel.Doc).OfClass(typeof(FilledRegionType)).Select(x => x.Name).OrderBy(x => x).ToList();
        var types = new FilteredElementCollector(viewModel.Doc).OfClass(typeof(FamilySymbol)).Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
        EquipmentType.ItemsSource = SignalType.ItemsSource = SignalWithoutTagType.ItemsSource = types;
        Fill(UserSettings.Load(out var warning));
        Status.Text = string.IsNullOrEmpty(warning) ? "Изменения применятся после сохранения при следующем запуске команды." : warning;
    }

    private void Fill(UserSettings settings)
    {
        RegionType.Text = settings.RegionType;
        RegionNameParameter.Text = settings.RegionNameParameter;
        RegionGroupParameter.Text = settings.RegionGroupParameter;
        RegionCodeParameter.Text = settings.RegionCodeParameter;
        EquipmentType.Text = settings.EquipmentType;
        SignalType.Text = settings.SignalType;
        SignalWithoutTagType.Text = settings.SignalWithoutTagType;
        HovsFolder.Text = settings.HovsFolder;
        ElementsView.Text = settings.ElementsView;
        FsaView.Text = settings.FsaView;
        EquipmentView.Text = settings.EquipmentView;
        IniDirectory.Text = settings.IniDirectory;
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        var missing = new[] { ElementsView.Text, FsaView.Text, EquipmentView.Text }
            .Select(name => name.Trim()).Where(name => !_viewNames.Contains(name)).ToList();
        Status.Text = missing.Count == 0 ? "Все три вида найдены в текущем проекте." :
            "Не найдены виды: " + string.Join("; ", missing) + ". Можно сохранить имена для другого проекта.";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Выберите INI-файл в нужной папке", Filter = "Настройки INI (*.ini)|*.ini", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) IniDirectory.Text = Path.GetDirectoryName(dialog.FileName);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = new UserSettings { ElementsView = ElementsView.Text.Trim(), FsaView = FsaView.Text.Trim(),
            EquipmentView = EquipmentView.Text.Trim(), IniDirectory = IniDirectory.Text.Trim() };
        settings.RegionType = RegionType.Text.Trim();
        settings.RegionNameParameter = RegionNameParameter.Text.Trim();
        settings.RegionGroupParameter = RegionGroupParameter.Text.Trim();
        settings.RegionCodeParameter = RegionCodeParameter.Text.Trim();
        settings.EquipmentType = EquipmentType.Text.Trim();
        settings.SignalType = SignalType.Text.Trim();
        settings.SignalWithoutTagType = SignalWithoutTagType.Text.Trim();
        settings.HovsFolder = HovsFolder.Text.Trim();
        if (new[] { settings.ElementsView, settings.FsaView, settings.EquipmentView, settings.RegionType, settings.RegionNameParameter, settings.RegionGroupParameter, settings.RegionCodeParameter, settings.EquipmentType, settings.SignalType, settings.SignalWithoutTagType }.Any(string.IsNullOrWhiteSpace))
        {
            Status.Text = "Заполните имена видов, параметров и типов базы данных.";
            return;
        }
        try
        {
            if (!Path.IsPathRooted(settings.IniDirectory)) throw new IOException("Укажите полный путь к папке INI.");
            // Стандартная папка может отсутствовать, если INI ещё не используются.
            if (!Directory.Exists(settings.IniDirectory) && settings.IniDirectory != new UserSettings().IniDirectory)
                throw new IOException("Папка INI не найдена. Проверьте путь.");
            if (!Path.IsPathRooted(settings.HovsFolder)) throw new IOException("Укажите полный путь к базе ХОВС.");
            Directory.CreateDirectory(settings.HovsFolder);
            settings.Save();
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
        {
            Status.Text = "Настройки не сохранены. " + ex.Message;
        }
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        Fill(new UserSettings());
        Status.Text = "Стандартные значения восстановлены в окне. Нажмите «Сохранить», чтобы применить.";
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // ESC не закрывает окно случайно и не приводит к потере введённых данных.
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) e.Handled = true;
    }
}
