using System.IO;
using RevitDevTool.Utils;

namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Shared path constants and utilities for the Python environment.
/// Environment setup and package installation are handled by <see cref="IPythonEnvironmentProvider"/> implementations.
/// </summary>
public static class PythonEnvironment
{
    private const string PythonDllName = "python313.dll";
    private const string PixiEnvDirName = "pixi-env";
    private const string PixiEnvDir = @".pixi\envs\default";

    public static IReadOnlyCollection<string> RequirePackages =>
    [
        "mcp",
        "debugpy",
        "packaging"
    ];

    public static readonly string PixiProjectDir = Path.Combine(SettingsUtils.GetApplicationDataPath(), PixiEnvDirName);
    public static readonly string PythonHome = Path.Combine(PixiProjectDir, PixiEnvDir);
    public static readonly string PythonExe = Path.Combine(PythonHome, "python.exe");

    public static bool IsEnvironmentReady() => File.Exists(PythonExe);

    public static string GetPythonDllPath()
    {
        if (!Directory.Exists(PythonHome))
            throw new DirectoryNotFoundException($"Python env not found at: {PythonHome}");

        var exactPath = Path.Combine(PythonHome, PythonDllName);
        if (File.Exists(exactPath)) return exactPath;

        var dll = Directory.GetFiles(PythonHome, "python*.dll").FirstOrDefault();
        return dll ?? throw new FileNotFoundException("Python DLL not found in env.", PythonHome);
    }
}
