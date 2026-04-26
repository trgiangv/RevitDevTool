using Microsoft.Win32;

namespace DevTools.McpServer.Tools.Utils;

internal static class RevitPathResolver
{
    public static string? FindRevitPath(string version)
    {
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

    public static List<string> GetInstalledVersions()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var autodeskDir = Path.Combine(programFiles, "Autodesk");

        if (!Directory.Exists(autodeskDir))
            return [];

        return Directory.GetDirectories(autodeskDir, "Revit *")
            .Select(dir => Path.GetFileName(dir).Replace("Revit ", ""))
            .Where(v => int.TryParse(v, out var year) && year >= 2022) // RevitDevTool supports 2022 and later
            .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
