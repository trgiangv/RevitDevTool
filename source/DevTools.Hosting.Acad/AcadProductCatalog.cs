namespace DevTools.Hosting.Acad;

/// <summary>
/// Shared AutoCAD-family install names: one <see cref="ExeFileName"/>, verticals
/// selected by <see cref="ProductCodes"/> (<c>/product</c> and
/// <c>InstalledProducts</c> subkeys).
/// </summary>
public static class AcadProductCatalog
{
    public const string ExeFileName = "acad.exe";
    public const string CivilDbxFileName = "AecBase.dbx";
    public const string RegistryRoot = @"SOFTWARE\Autodesk\AutoCAD";
    public const string InstalledProductsKeyName = "InstalledProducts";

    /// <summary><c>/product</c> switch and <c>InstalledProducts</c> subkey.</summary>
    public static IReadOnlyDictionary<HostApp, string> ProductCodes { get; } =
        new Dictionary<HostApp, string>
        {
            [HostApp.AutoCad] = "ACAD",
            [HostApp.Civil3D] = "C3D",
            [HostApp.Plant3D] = "PLNT3D",
            [HostApp.AcadMap3D] = "MAP",
            [HostApp.AcadArch] = "ACA",
            [HostApp.AcadMech] = "ACADM",
            [HostApp.AcadMep] = "MEP",
            [HostApp.AcadElec] = "ACADE",
        };

    /// <summary>
    /// <c>ACAD-xxxx</c> product-id digits. Mirrors in-host <c>AcadProductDetector</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, HostApp> ProductIdMap { get; } =
        new Dictionary<string, HostApp>(StringComparer.OrdinalIgnoreCase)
        {
            ["00"] = HostApp.Civil3D,
            ["01"] = HostApp.AutoCad,
            ["02"] = HostApp.AcadMap3D,
            ["04"] = HostApp.AcadArch,
            ["05"] = HostApp.AcadMech,
            ["06"] = HostApp.AcadMep,
            ["07"] = HostApp.AcadElec,
            ["17"] = HostApp.Plant3D,
        };

    public static bool TryGetHost(string productCode, out HostApp hostApp)
    {
        foreach (var pair in ProductCodes)
        {
            if (!string.Equals(pair.Value, productCode, StringComparison.OrdinalIgnoreCase)) continue;
            hostApp = pair.Key;
            return true;
        }

        hostApp = default;
        return false;
    }
}
