using DevTools.Hosting;
using DevTools.Update.Autodesk;

namespace DevTools.Update.Engine;

/// <summary>
///     One row of the Updates tab.
/// </summary>
public sealed record TargetOption(AddinComponent Component, IReadOnlyList<HostApp> Hosts, bool Selected, string? Installed, string? Pending, string Latest)
{
    public bool OnComputer => Hosts.Count > 0;
}

public sealed record SelectionResult(string Version, IReadOnlyList<AddinTarget> Applied, IReadOnlyList<AddinTarget> Pending, IReadOnlyList<AddinTarget> Unavailable);
