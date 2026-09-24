using System.IO;
using System.Xml.Linq;
using HOVS.Model;

namespace HOVS.Plugin;

public sealed class HovsProject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
public sealed class HovsRevision
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Created { get; set; } = "";
    public string CreatedLocal => DateTimeOffset.TryParse(Created, out var date) ? date.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : Created;
    public string SourcePath { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
}

/// <summary>Локальная файловая база: отдельные объекты и неизменяемые ревизии с копиями XLSX.
/// Финальное перемещение каталога публикует ревизию только после успешной записи всех файлов.</summary>
public sealed class HovsRepository
{
    private readonly string _root;
    public HovsRepository(string root) { _root = Path.GetFullPath(Path.Combine(root, "Projects")); Directory.CreateDirectory(_root); }
    public IReadOnlyList<HovsProject> Projects() => Directory.GetDirectories(_root)
        .Where(d => Guid.TryParseExact(Path.GetFileName(d), "N", out _) && File.Exists(Path.Combine(d, "project.xml")))
        .Select(d => new HovsProject { Id = Path.GetFileName(d), Name = (string?)XDocument.Load(Path.Combine(d, "project.xml")).Root?.Attribute("name") ?? Path.GetFileName(d) })
        .OrderBy(p => p.Name).ToList();
    public HovsProject Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Введите имя объекта.");
        var p = new HovsProject { Id = Guid.NewGuid().ToString("N"), Name = name.Trim() };
        var d = Path.Combine(_root, p.Id); Directory.CreateDirectory(d);
        new XDocument(new XElement("Project", new XAttribute("name", p.Name))).Save(Path.Combine(d, "project.xml"));
        return p;
    }
    public IReadOnlyList<HovsRevision> Revisions(HovsProject project) => Directory.GetDirectories(ProjectPath(project))
        .Where(d => Guid.TryParseExact(Path.GetFileName(d), "N", out _) && File.Exists(Path.Combine(d, "model.xml")))
        .Select(d => { var x = XDocument.Load(Path.Combine(d, "model.xml")).Root!; return new HovsRevision {
            Id = Path.GetFileName(d), Name = (string?)x.Attribute("name") ?? "Ревизия", Created = (string?)x.Attribute("created") ?? "",
            SourcePath = Path.Combine(d, "source.xlsx"), DirectoryPath = d }; }).OrderByDescending(x => x.Created).ToList();
    private string ProjectPath(HovsProject p)
    {
        if (!Guid.TryParseExact(p.Id, "N", out _)) throw new ArgumentException("Неверный идентификатор объекта.");
        var path = Path.Combine(_root, p.Id);
        CheckDirectory(path);
        if (!File.Exists(Path.Combine(path, "project.xml"))) throw new DirectoryNotFoundException("Объект больше не существует.");
        return path;
    }
    // Проверка принадлежности выполняется до удаления; путь из интерфейса не является доверенным.
    private void CheckDirectory(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Каталог базы не найден.");
        if ((File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Каталог базы не должен быть ссылкой на другой каталог.");
    }
    private string RevisionPath(HovsRevision revision)
    {
        if (!Guid.TryParseExact(revision.Id, "N", out _)) throw new ArgumentException("Неверный идентификатор ревизии.");
        var path = Path.GetFullPath(revision.DirectoryPath);
        var parent = Path.GetDirectoryName(path) ?? "";
        var project = new HovsProject { Id = Path.GetFileName(parent) };
        var expected = Path.Combine(ProjectPath(project), revision.Id);
        if (!string.Equals(path, expected, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Ревизия не принадлежит этой базе.");
        CheckDirectory(path);
        return path;
    }
    public void Rename(HovsProject project, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Введите имя объекта.");
        var file = Path.Combine(ProjectPath(project), "project.xml");
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            new XDocument(new XElement("Project", new XAttribute("name", name.Trim()))).Save(temp);
            File.Replace(temp, file, null);
            project.Name = name.Trim();
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void DeleteProject(HovsProject project) => Directory.Delete(ProjectPath(project), true);
    public void DeleteRevision(HovsProject project, HovsRevision revision)
    {
        var projectPath = ProjectPath(project);
        var path = RevisionPath(revision);
        if (!string.Equals(Path.GetDirectoryName(path), projectPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ревизия принадлежит другому объекту.");
        Directory.Delete(path, true);
    }
    public HovsRevision Save(HovsProject project, HovsModel model, string source, string name)
    {
        var id = Guid.NewGuid().ToString("N");
        var folder = Path.Combine(ProjectPath(project), id);
        var temp = Path.Combine(ProjectPath(project), "." + id);
        Directory.CreateDirectory(temp);
        try
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Исходный XLSX не найден.", source);
            File.Copy(source, Path.Combine(temp, "source.xlsx"));
            var root = new XElement("Revision", new XAttribute("name", name), new XAttribute("created", DateTime.UtcNow.ToString("O")),
                new XElement("Equipment", model.Equipment.Select(e => new XElement("Item",
                    new XAttribute("id", e.Id), new XAttribute("name", e.Name), new XAttribute("type", e.Type), new XAttribute("room", e.Room),
                    e.Attributes.Select(a => new XElement("Attribute", new XAttribute("key", a.Key), SourceCellCodec.MakeXmlSafe(a.Value)))))),
                new XElement("Components", model.Components.Select(c => new XElement("Item", new XAttribute("id", c.EquipmentId), new XAttribute("type", c.ComponentType),
                    new XAttribute("quantity", c.Quantity?.ToString() ?? ""), SourceCellCodec.MakeXmlSafe(c.Value)))),
                new XElement("Relations", model.Relations.Select(c => new XElement("Item", new XAttribute("source", c.SourceId), new XAttribute("target", c.TargetId),
                    new XAttribute("kind", c.Kind), new XAttribute("confidence", c.Confidence), c.Reason))));
            new XDocument(root).Save(Path.Combine(temp, "model.xml"));
            Directory.Move(temp, folder);
            return Revisions(project).Single(x => x.Id == id);
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }
    public HovsModel Load(HovsRevision revision)
    {
        var root = XDocument.Load(Path.Combine(RevisionPath(revision), "model.xml")).Root ?? throw new InvalidDataException("Пустая ревизия.");
        string A(XElement x, string key) => (string?)x.Attribute(key) ?? "";
        var equipment = root.Element("Equipment")!.Elements().Select(x => new Equipment(A(x,"id"),A(x,"name"),A(x,"type"),A(x,"room"),
            x.Elements("Attribute").ToDictionary(a => A(a,"key"), a => a.Value))).ToList();
        var components = root.Element("Components")!.Elements().Select(x => new EquipmentComponent(A(x,"id"), A(x,"type"),x.Value,
            int.TryParse(A(x,"quantity"),out var n) ? n : (int?)null)).ToList();
        var relations = root.Element("Relations")!.Elements().Select(x => new Relation(A(x,"source"),A(x,"target"),
            (RelationKind)Enum.Parse(typeof(RelationKind),A(x,"kind")),(double)x.Attribute("confidence")!,x.Value)).ToList();
        return new HovsModel(equipment, components, relations);
    }
}
