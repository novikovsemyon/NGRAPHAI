using System.IO;
using System.Xml.Linq;

namespace NGraph.Core;

public sealed class HelpSection
{
    public string Title { get; set; } = "";
    public List<string> Paragraphs { get; set; } = new();
}
public sealed class HelpTopic
{
    public string Title { get; set; } = "";
    public string Group { get; set; } = "";
    public string Intro { get; set; } = "";
    public List<HelpSection> Sections { get; set; } = new();
    public string SearchText => Title + " " + Group + " " + Intro + " " + string.Join(" ", Sections.SelectMany(s => s.Paragraphs.Prepend(s.Title)));
}
public static class HelpContent
{
    public static List<HelpTopic> Load()
    {
        using var stream = typeof(HelpContent).Assembly.GetManifestResourceStream("NGraph.Help.Commands.xml")
            ?? throw new FileNotFoundException("Встроенная инструкция не найдена. Переустановите NGraph.");
        return XDocument.Load(stream).Root!.Elements("Topic").Select(t => new HelpTopic
        {
            Title = (string?)t.Attribute("title") ?? "", Group = (string?)t.Attribute("group") ?? "", Intro = (string?)t.Element("Intro") ?? "",
            Sections = t.Elements("Section").Select(s => new HelpSection { Title = (string?)s.Attribute("title") ?? "", Paragraphs = s.Elements("P").Select(p => p.Value).ToList() }).ToList()
        }).ToList();
    }
    public static string ExtractPdf()
    {
        using var stream = typeof(HelpContent).Assembly.GetManifestResourceStream("NGraph.Help.UserGuide.pdf")
            ?? throw new FileNotFoundException("Встроенный PDF не найден. Переустановите NGraph.");
        using var memory = new MemoryStream(); stream.CopyTo(memory); var bytes = memory.ToArray();
        using var sha = System.Security.Cryptography.SHA256.Create();
        var fingerprint = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 12);
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NGraph", "Help");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "NGraph_User_Guide_" + fingerprint + ".pdf");
        if (!File.Exists(file))
        {
            // Новая редакция получает собственное имя; открытый PDF старой версии не блокирует обновление.
            var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, bytes);
                try { File.Move(temp, file); } catch (IOException) when (File.Exists(file)) { }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        return file;
    }
}
