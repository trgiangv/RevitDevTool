using System.Reflection;

namespace DevTools.Utilities;

/// <summary>
/// The version of the release train this process belongs to, as SemVer with its prerelease suffix and without
/// build metadata (<c>4.0.0-beta.8</c>). It reads the add-in assembly the host add-in registers at startup,
/// else the entry assembly of the Daemon or the TestRunner, else the assembly that holds this type
/// (the TestAdapter package merges it, so there it is the package version).
/// </summary>
public static class AppVersion
{
    private static readonly string[] TrainEntries = ["DevTools.Daemon", "DevTools.TestRunner"];
    private static Assembly? _addin;

    public static string Current => Read(_addin ?? TrainEntry() ?? typeof(AppVersion).Assembly);

    public static void UseAddin(Assembly addin) => _addin = addin;

    private static Assembly? TrainEntry() =>
        Assembly.GetEntryAssembly() is { } entry && TrainEntries.Contains(entry.GetName().Name) ? entry : null;

    private static string Read(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
}
