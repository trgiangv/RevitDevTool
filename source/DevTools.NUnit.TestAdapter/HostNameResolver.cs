namespace DevTools.NUnit.TestAdapter;

internal static class HostNameResolver
{
    private static readonly Dictionary<string, string> KnownHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["revit"] = "Revit",
        ["autocad"] = "AutoCad",
        ["civil3d"] = "Civil3D",
        ["plant3d"] = "Plant3D",
        ["acadarch"] = "AcadArch",
        ["acadmech"] = "AcadMech",
        ["acadmep"] = "AcadMep",
        ["acadelec"] = "AcadElec",
        ["acadmap3d"] = "AcadMap3D",
        ["navisworks"] = "Navisworks",
        ["rhino"] = "Rhino",
        ["tekla"] = "Tekla",
    };

    public static string Resolve(string? hostName)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new ArgumentException("Host name is required.", nameof(hostName));
        }

        var trimmed = hostName!.Trim();
        return KnownHosts.TryGetValue(trimmed, out var mapped) ? mapped : trimmed;
    }
}
