using DevTools.Logging;

namespace DevTools.Daemon.Mcp.Tools;

internal static class HostAppExtensions
{
    private static readonly Dictionary<string, HostApp> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".rvt"] = HostApp.Revit,
        [".rfa"] = HostApp.Revit,
        [".rft"] = HostApp.Revit,
        [".rte"] = HostApp.Revit,
        [".dwg"] = HostApp.AutoCad,
        [".dxf"] = HostApp.AutoCad,
        [".dwf"] = HostApp.AutoCad,
        [".dwt"] = HostApp.AutoCad,
        [".nwd"] = HostApp.Navisworks,
        [".nwc"] = HostApp.Navisworks,
        [".nwf"] = HostApp.Navisworks,
    };

    private static readonly HashSet<HostApp> AcadFamily =
    [
        HostApp.AutoCad, HostApp.Civil3D, HostApp.Plant3D,
        HostApp.AcadArch, HostApp.AcadMech, HostApp.AcadElec,
        HostApp.AcadMep, HostApp.AcadMap3D
    ];

    public static bool IsAcadFamily(this HostApp app) => AcadFamily.Contains(app);

    public static HostApp? FromExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;
        return ExtensionMap.TryGetValue(extension, out var app) ? app : null;
    }

    public static HostApp? FromPipeName(string pipeName)
    {
        var firstUnderscore = pipeName.IndexOf('_');
        if (firstUnderscore <= 0) return null;
        var hostSegment = pipeName[..firstUnderscore];
        return Enum.TryParse<HostApp>(hostSegment, ignoreCase: true, out var app) ? app : null;
    }

    public static HostApp? ParseHostApp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<HostApp>(value, ignoreCase: true, out var app) ? app : null;
    }

    public static string[] SupportedExtensions(HostApp app)
    {
        if (app == HostApp.Revit) return [".rvt", ".rfa", ".rft", ".rte"];
        if (app.IsAcadFamily()) return [".dwg", ".dxf", ".dwf", ".dwt"];
        if (app == HostApp.Navisworks) return [".nwd", ".nwc", ".nwf"];
        return [];
    }
}
