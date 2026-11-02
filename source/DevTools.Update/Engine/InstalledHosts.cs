using DevTools.Hosting;
using DevTools.Update.Autodesk;

namespace DevTools.Update.Engine;

/// <summary>
///     The Revit and AutoCAD-family hosts installed on this computer, as add-in targets.
/// </summary>
internal static class InstalledHosts
{
    private static readonly (HostApp App, string Platform)[] Platforms = Enum.GetValues<HostApp>()
        .Where(app => app == HostApp.Revit || app.IsAcadFamily())
        .Select(app => (app, app == HostApp.Revit ? PackageContents.RevitPlatform : PackageContents.AutoCadPlatform))
        .ToArray();

    public static Dictionary<AddinTarget, HostApp[]> Detect(IEnumerable<IHostPathResolver> resolvers) => Platforms
        .SelectMany(platform => resolvers
            .Where(resolver => resolver.Supports(platform.App))
            .SelectMany(resolver => resolver.GetInstalledVersions(platform.App))
            .Select(hostVersion => (Target: new AddinTarget(platform.Platform, hostVersion), platform.App)))
        .GroupBy(host => host.Target, host => host.App)
        .ToDictionary(group => group.Key, group => group.Distinct().ToArray());
}
