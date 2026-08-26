using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DevTools.Hosting;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool.Adapters;

public sealed class AcadHostAppInfo : IHostAppInfo
{
    public HostApp Host => AcadProductDetector.Detect();
    public string VersionNumber => AcadProductDetector.GetVersionNumber();
    public string VersionBuild => AcadApp.Version.ToString();
    public int ProcessId => Environment.ProcessId;
}

// ReSharper disable once PartialTypeWithSinglePart
public static partial class AcadProductDetector
{
    private const string AcadProductKeyPattern = @"ACAD-[0-9A-F]\d(?<productId>\d{2}):[0-9A-F]{3,4}";
#if NET7_0_OR_GREATER
    [GeneratedRegex(AcadProductKeyPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex ProductKey();
    private static readonly Regex ProductKeyRegex = ProductKey();
#else
    private static readonly Regex ProductKeyRegex = new(AcadProductKeyPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
#endif

    private static readonly IReadOnlyDictionary<string, HostApp> ProductMap =
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

    public static HostApp Detect()
    {
        var userRegistryProductRootKey = HostApplicationServices.Current?.UserRegistryProductRootKey;
        if (string.IsNullOrWhiteSpace(userRegistryProductRootKey))
            return HostApp.AutoCad;

        var match = ProductKeyRegex.Match(userRegistryProductRootKey);
        if (!match.Success)
            return HostApp.AutoCad;

        var productId = match.Groups["productId"].Value;
        return ProductMap.GetValueOrDefault(productId, HostApp.AutoCad);
    }
    
    /// <summary>
    /// "Software\Autodesk\AutoCAD\R25.1\ACAD-9100:409" -> "2026"
    /// </summary>
    public static string GetVersionNumber()
    {
        var regPath = HostApplicationServices.Current?.UserRegistryProductRootKey;
        if (regPath is null) return "Unknown";
        using var key = Registry.LocalMachine.OpenSubKey(regPath);
        return key?.GetValue("UPIRELEASE") as string ?? "Unknown";
    }
}