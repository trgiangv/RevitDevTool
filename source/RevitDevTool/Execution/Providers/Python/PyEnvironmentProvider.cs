using System.IO;

namespace RevitDevTool.Execution.Providers.Python;

public enum PythonBackend
{
    Pixi,
    Pip
}

/// <summary>
/// Abstract base for Python environment providers.
/// Owns shared state (PythonHome, PythonExe), the DLL-lookup helper,
/// and the list of packages that must always be present.
/// Each backend (Pixi / Pip) implements setup and package installation.
/// </summary>
public abstract class PyEnvironmentProvider
{
    /// <summary>
    /// Packages that must always be present, with pinned version constraints.
    /// Keys are package names (used for display/matching),
    /// values are pixi add specs (name + constraint).
    /// </summary>
    public static IReadOnlyDictionary<string, string> RequirePackages { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mcp"] = "mcp>=1.27,<2",
            ["pytest"] = "pytest>=9.0.3,<10",
            ["debugpy"] = "debugpy>=1.8,<2",
            ["packaging"] = "packaging>=26.0,<27",
        };

    protected string? PythonHomePath;

    public abstract PythonBackend Backend { get; }

    public string PythonHome => PythonHomePath ?? string.Empty;

    public string PythonExe => PythonHomePath is not null
        ? Path.Combine(PythonHomePath, "python.exe")
        : string.Empty;

    public virtual bool IsEnvironmentReady()
        => PythonHomePath is not null && File.Exists(PythonExe);

    public string GetPythonDllPath()
    {
        if (!Directory.Exists(PythonHome))
            throw new DirectoryNotFoundException($"Python env not found at: {PythonHome}");

        var dll = Directory.GetFiles(PythonHome, "python3*.dll")
            .FirstOrDefault(f => !Path.GetFileName(f)
                .Equals("python3.dll", StringComparison.OrdinalIgnoreCase));

        return dll ?? throw new FileNotFoundException("Python DLL not found in env.", PythonHome);
    }

    public abstract Task SetupEnvironmentAsync();

    public abstract Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken);
}
