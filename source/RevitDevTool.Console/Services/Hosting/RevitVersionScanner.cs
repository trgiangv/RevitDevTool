using RevitDevTool.Bridge.Abstractions;

namespace RevitDevTool.Console.Services.Hosting;

/// <summary>
/// Scans <c>Program Files\Autodesk</c> for installed Revit versions.
/// </summary>
public sealed class RevitVersionScanner : IVersionScanner
{
    public string AppId => "revit";

    public List<string> GetInstalledVersions()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var autodeskDir = Path.Combine(programFiles, "Autodesk");

        if (!Directory.Exists(autodeskDir))
            return [];

        return Directory.GetDirectories(autodeskDir, "Revit *")
            .Select(dir => Path.GetFileName(dir).Replace("Revit ", ""))
            .Where(v => int.TryParse(v, out var year) && year >= 2022)
            .OrderBy(v => v)
            .ToList();
    }
}
