namespace DevTools.Hosting;

public static class HostAppExtensions
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
        if (extension is not { Length: > 0 }) return null;
        return ExtensionMap.TryGetValue(extension, out var app) ? app : null;
    }
}
