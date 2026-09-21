using System.Runtime.InteropServices;
using System.Text;

namespace NGraph.Core;

public sealed class INIManager
{
    private const int Size = 1024;

    public string Path { get; set; }

    public INIManager(string path)
    {
        Path = path ?? string.Empty;
    }

    public INIManager() : this(string.Empty)
    {
    }

    public string GetPrivateString(string section, string key)
    {
        var buffer = new StringBuilder(Size);
        GetPrivateProfileString(section, key, null, buffer, Size, Path);
        return buffer.ToString();
    }

    public void WritePrivateString(string section, string key, string value)
    {
        WritePrivateProfileString(section, key, value, Path);
    }

    [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileString", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileString(
        string section,
        string key,
        string? defaultValue,
        StringBuilder buffer,
        int size,
        string path);

    [DllImport("kernel32.dll", EntryPoint = "WritePrivateProfileString", CharSet = CharSet.Unicode)]
    private static extern int WritePrivateProfileString(
        string section,
        string key,
        string value,
        string path);
}
