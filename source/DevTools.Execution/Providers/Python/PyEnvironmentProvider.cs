using System.IO;
using System.Text.Json;

namespace DevTools.Execution.Providers.Python;

public enum PythonBackend
{
    Pixi,
    Pip
}

/// <summary>
/// Abstract base for Python environment providers.
/// Owns process-scoped <see cref="PythonHome"/>, DLL lookup, and require-package specs.
/// Each backend implements <see cref="ResolvePythonHomeAsync"/>; base assigns it once.
/// </summary>
public abstract class PyEnvironmentProvider
{
    /// <summary>
    /// Packages that must always be present, with pinned version constraints.
    /// Keys are package names (used for display/matching),
    /// values are pixi/pip specs (name + constraint).
    /// </summary>
    public static IReadOnlyDictionary<string, string> RequirePackages { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mcp"] = "mcp>=2.0,<3",
            ["pytest"] = "pytest>=9.0.3,<10",
            ["debugpy"] = "debugpy>=1.8,<2",
            ["packaging"] = "packaging>=26.0,<27",
        };

    private string? _pythonHome;

    public abstract PythonBackend Backend { get; }

    public string PythonHome => _pythonHome ?? string.Empty;

    public string PythonExe => _pythonHome is not null
        ? Path.Combine(_pythonHome, "python.exe")
        : string.Empty;

    /// <summary>Read-only: home is set and <c>python.exe</c> exists.</summary>
    public bool IsEnvironmentReady()
        => _pythonHome is not null && File.Exists(PythonExe);

    /// <summary>Backend-specific home (Pixi AppData path / Pip pyRevit cengines).</summary>
    protected abstract Task<string> ResolvePythonHomeAsync();

    /// <summary>Assign <see cref="PythonHome"/> once per process via <see cref="ResolvePythonHomeAsync"/>.</summary>
    protected async Task EnsurePythonHomeAsync()
    {
        if (_pythonHome is not null) return;

        var home = await ResolvePythonHomeAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(home))
            throw new InvalidOperationException("Python home path was not resolved.");

        _pythonHome = home;
    }

    public string GetPythonDllPath()
    {
        if (!Directory.Exists(PythonHome))
            throw new DirectoryNotFoundException($"Python env not found at: {PythonHome}");

        var dll = Directory.GetFiles(PythonHome, "python3*.dll")
            .FirstOrDefault(f => !Path.GetFileName(f)
                .Equals("python3.dll", StringComparison.OrdinalIgnoreCase));

        return dll ?? throw new FileNotFoundException("Python DLL not found in env.", PythonHome);
    }

    /// <summary>PEP 508 / requirement spec → distribution name (strip version/extras).</summary>
    public static string ExtractPackageName(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return string.Empty;

        spec = spec.Trim();
        var i = 0;
        while (i < spec.Length)
        {
            var c = spec[i];
            if (c is '>' or '<' or '=' or '!' or '~' or '[' or ';' or ' ')
                break;
            i++;
        }

        return i == 0 ? string.Empty : spec[..i];
    }

    public abstract Task SetupEnvironmentAsync();

    /// <summary>
    /// Installed-package JSON for PEP 723 Parser stdin and skip-if-listed
    /// (<c>pixi list --json</c> or <c>pip list --format=json</c>).
    /// </summary>
    public abstract Task<string> GetListJsonAsync(CancellationToken cancellationToken = default);

    public abstract Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken);

    /// <summary>Package names from <see cref="GetListJsonAsync"/> (empty on failure).</summary>
    protected async Task<HashSet<string>> GetInstalledNamesAsync(CancellationToken cancellationToken = default)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var json = await GetListJsonAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return names;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return names;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var nameProp)
                    && nameProp.ValueKind == JsonValueKind.String
                    && nameProp.GetString() is { Length: > 0 } name)
                {
                    names.Add(name);
                }
            }
        }
        catch (JsonException)
        {
            // empty → callers install
        }

        return names;
    }
}
