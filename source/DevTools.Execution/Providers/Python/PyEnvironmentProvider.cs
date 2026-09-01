using System.IO;
using System.Text.Json;
using CliWrap;
using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

/// <summary>Process-scoped Python home, DLL lookup, and require-package specs.</summary>
public abstract class PyEnvironmentProvider
{
    /// <summary>Pinned require specs (name → PEP 508 constraint).</summary>
    public static IReadOnlyDictionary<string, string> RequirePackages { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mcp"] = "mcp>=2.1.1,<3",
            ["pytest"] = "pytest>=9.1.1,<10",
            ["debugpy"] = "debugpy>=1.8.21,<2",
            ["packaging"] = "packaging>=26.3,<27",
        };

    private string? _pythonHome;

    public abstract PythonBackend Backend { get; }

    public string PythonHome => _pythonHome ?? string.Empty;

    public virtual string PythonExe => _pythonHome is not null
        ? Path.Combine(_pythonHome, "python.exe")
        : string.Empty;

    public string SitePackagesDir => string.IsNullOrEmpty(PythonHome)
        ? string.Empty
        : Path.Combine(PythonHome, "Lib", "site-packages");

    /// <summary>Sidecar CPython <c>Lib</c> (uv <c>pyvenv.cfg</c> home, else prefix <c>Lib</c>).</summary>
    public string StdlibLibDir
    {
        get
        {
            if (string.IsNullOrEmpty(PythonHome))
                return string.Empty;

            var cfg = Path.Combine(PythonHome, "pyvenv.cfg");
            if (TryReadPyvenvHome(cfg, out var home))
            {
                var lib = Path.Combine(home, "Lib");
                if (Directory.Exists(lib))
                    return lib;
            }

            var prefixLib = Path.Combine(PythonHome, "Lib");
            return Directory.Exists(prefixLib) ? prefixLib : string.Empty;
        }
    }

    internal static bool TryReadPyvenvHome(string cfgPath, out string home)
    {
        home = string.Empty;
        if (!File.Exists(cfgPath))
            return false;

        foreach (var raw in File.ReadLines(cfgPath))
        {
            var line = raw.Trim();
            if (line.Length < 6 || !line.StartsWith("home", StringComparison.OrdinalIgnoreCase))
                continue;

            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;

            var value = line[(eq + 1)..].Trim().Trim('"');
            if (value.Length == 0)
                continue;

            home = value;
            return true;
        }

        return false;
    }

    public virtual bool IsEnvironmentReady() => File.Exists(PythonExe);

    /// <summary>Host already owns CPython. uv/pip snapshot version from this DLL and attach as sidecar.</summary>
    internal virtual void AttachHostInterpreter(string hostDll) { }

    /// <summary>Manager CLI (pixi.exe / uv.exe). Pip has none.</summary>
    protected virtual string? ManagerExePath => null;

    protected async Task VerifyRunnableAsync(ILogger? logger = null)
    {
        var exePath = ManagerExePath
            ?? throw new InvalidOperationException($"{Backend} has no manager executable to verify.");

        var name = Path.GetFileName(exePath);
        var result = await Cli.Wrap(exePath)
            .WithArguments("--version")
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync()
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{name} --version failed with exit code {result.ExitCode}.");
        }

#if DEBUG
        logger?.ZLogDebug($"{name} runtime verified (exit {result.ExitCode}).");
#endif
    }

    protected abstract Task<string> ResolvePythonHomeAsync();

    protected async Task EnsurePythonHomeAsync()
    {
        if (_pythonHome is not null) return;

        var home = await ResolvePythonHomeAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(home))
            throw new InvalidOperationException("Python home path was not resolved.");

        _pythonHome = home;
    }

    public virtual string GetPythonDllPath()
    {
        if (!Directory.Exists(PythonHome))
            throw new DirectoryNotFoundException($"Python env not found at: {PythonHome}");

        var dll = Directory.GetFiles(PythonHome, "python3*.dll")
            .FirstOrDefault(f => !PythonNativeEnvironment.IsStableAbiForwarder(f));

        return dll ?? throw new FileNotFoundException("Python DLL not found in env.", PythonHome);
    }

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

    public abstract Task<string> GetListJsonAsync(CancellationToken cancellationToken = default);

    public abstract Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken);

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
