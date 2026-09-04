namespace DevTools.Mcp.Catalog.Tests;

internal static class OptionalArtifact
{
    public static void RequireFile(string path, string hint)
    {
        if (!File.Exists(path))
            Assert.Skip(hint);
    }

    public static void RequireDirectory(string path, string hint)
    {
        if (!Directory.Exists(path))
            Assert.Skip(hint);
    }

    public static string? FirstExistingFile(IEnumerable<string> candidates) =>
        candidates.FirstOrDefault(File.Exists);

    public static string PixiPythonExePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RevitDevTool",
            "pixi-env",
            ".pixi",
            "envs",
            "default",
            "python.exe");

    public const string PixiPythonHint =
        "Pixi Python not found. Install via RevitDevTool setup or run pixi in %APPDATA%\\RevitDevTool\\pixi-env.";

    public const string McpToolsetDemoHint =
        "Build toolset: dotnet build samples/McpToolsetDemo -c Debug.Autodesk.2025 -m:1";

    public const string RevitMcpToolSetHint =
        "Build toolset: dotnet build samples/RevitMcpToolSet -c Debug.Autodesk.2025 -m:1";

    public static string? ResolveMcpToolsetDemoDll(string repositoryRoot) =>
        FirstExistingFile(
        [
            Path.Combine(repositoryRoot, "samples", "McpToolsetDemo", "bin", "Debug.Autodesk.2025", "McpToolsetDemo.dll"),
            Path.Combine(repositoryRoot, "samples", "McpToolsetDemo", "bin", "Release.Autodesk.2025", "McpToolsetDemo.dll"),
            Path.Combine(repositoryRoot, "samples", "McpToolsetDemo", "bin", "Debug", "net8.0", "McpToolsetDemo.dll"),
        ]);

    public static string? ResolveRevitMcpToolSetDll(string repositoryRoot) =>
        FirstExistingFile(
        [
            Path.Combine(repositoryRoot, "samples", "RevitMcpToolSet", "bin", "Debug.Autodesk.2025", "RevitMcpToolSet.dll"),
            Path.Combine(repositoryRoot, "samples", "RevitMcpToolSet", "bin", "Debug.Autodesk.2024", "RevitMcpToolSet.dll"),
            Path.Combine(repositoryRoot, "samples", "RevitMcpToolSet", "bin", "Debug", "net8.0", "RevitMcpToolSet.dll"),
        ]);
}
