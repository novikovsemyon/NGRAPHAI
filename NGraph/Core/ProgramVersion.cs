using System.Reflection;

namespace NGraph.Core;

/// <summary>Показывает версию загруженного add-in, отделяя служебный Git-хеш от номера сборки.</summary>
public static class ProgramVersion
{
    public static string Current => Format(typeof(ProgramVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(ProgramVersion).Assembly.GetName().Version);
    public static string Full => typeof(ProgramVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Current;
    internal static string Format(string? informational, Version? fallback)
    {
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var version = informational!.Split('+')[0].Trim();
            if (version.Length > 0) return version;
        }
        return fallback == null ? "не определена" : fallback.ToString(fallback.Revision > 0 ? 4 : 3);
    }
}
