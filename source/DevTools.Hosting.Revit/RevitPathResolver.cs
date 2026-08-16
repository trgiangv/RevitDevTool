using Microsoft.Win32;

namespace DevTools.Hosting.Revit;

public sealed class RevitPathResolver : IHostPathResolver
{
    public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;

    public string? FindExecutable(HostApp hostApp, string version)
    {
        if (!Supports(hostApp))
            return null;

        var registryPath = FindFromRegistry(version);
        if (!string.IsNullOrWhiteSpace(registryPath))
            return registryPath;

        var defaultPath = $@"C:\Program Files\Autodesk\Revit {version}\Revit.exe";
        if (File.Exists(defaultPath))
            return defaultPath;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var autodeskDir = Path.Combine(programFiles, "Autodesk");
        return !Directory.Exists(autodeskDir)
            ? null
            : Directory.GetDirectories(autodeskDir, $"Revit {version}*")
                .Select(dir => Path.Combine(dir, "Revit.exe"))
                .FirstOrDefault(File.Exists);
    }

    public IReadOnlyList<string> GetInstalledVersions(HostApp hostApp)
    {
        if (!Supports(hostApp))
            return [];

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var autodeskDir = Path.Combine(programFiles, "Autodesk");
        if (!Directory.Exists(autodeskDir))
            return [];

        return Directory.GetDirectories(autodeskDir, "Revit *")
            .Select(dir => Path.GetFileName(dir).Replace("Revit ", ""))
            .Where(v => int.TryParse(v, out var year) && year >= 2022)
            .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? FindFromRegistry(string version)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Autodesk\Revit\Autodesk Revit {version}");
            if (key?.GetValue("InstallationLocation") is string installDir)
            {
                var exePath = Path.Combine(installDir, "Revit.exe");
                if (File.Exists(exePath))
                    return exePath;
            }
        }
        catch
        {
            // Registry access may fail.
        }

        return null;
    }
}
