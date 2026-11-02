using System.Reflection;

namespace DevTools.Utilities;

/// <summary>
/// The version of the product this process runs, as SemVer with its prerelease suffix and without build
/// metadata (<c>4.0.0-beta.4</c>). The build stamps one version on every assembly of a release, and the
/// TestAdapter package merges this assembly, so there it is the package version.
/// </summary>
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .Split('+')[0];
}
