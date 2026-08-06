using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using Python.Runtime;

namespace DevTools.Execution.Providers.Python;

public static class PythonDepsManager
{
    private sealed record ParseResult(
        [property: JsonPropertyName("requires_python")] string RequiresPython, 
        [property: JsonPropertyName("to_install")] List<string> ToInstall)
    {
        public static readonly ParseResult Empty = new(string.Empty, []);
    }

    /// <summary>
    /// Resolves PEP 723 dependencies from a script file path or inline code string.
    /// If <paramref name="scriptPathOrCode"/> is an existing file, uses it directly.
    /// Otherwise treats it as inline code: skips if no PEP 723 marker present,
    /// writes a temp file for the parser when needed, and cleans up after.
    /// </summary>
    public static async Task<List<string>> ResolveDependenciesAsync(
        PyEnvironmentProvider provider,
        string scriptPathOrCode,
        CancellationToken cancellationToken = default)
    {
        string actualPath;
        var isTemp = false;

        if (File.Exists(scriptPathOrCode))
        {
            actualPath = scriptPathOrCode;
        }
        else
        {
            if (!scriptPathOrCode.Contains("# /// script"))
                return [];

            actualPath = Path.Combine(Path.GetTempPath(), $"pep723_{Guid.NewGuid():N}.py");
            await File.WriteAllTextAsync(actualPath, scriptPathOrCode, cancellationToken).ConfigureAwait(false);
            isTemp = true;
        }

        try
        {
            var stdinContent = await provider.GetListJsonAsync(cancellationToken).ConfigureAwait(false);
            var result = await RunParserAsync(provider, actualPath, stdinContent, cancellationToken).ConfigureAwait(false);
            return result.ToInstall;
        }
        finally
        {
            if (isTemp)
            {
                try { File.Delete(actualPath); }
                catch { /* best-effort */ }
            }
        }
    }

    /// <summary>
    /// Installs <paramref name="dependencies"/> into the Python env via the given provider.
    /// </summary>
    public static async Task InstallDependenciesAsync(
        PyEnvironmentProvider provider,
        IEnumerable<string> dependencies,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var depList = dependencies.ToList();
        if (depList.Count == 0) return;

        progress.Report($"Installing {depList.Count} package(s)...");

        if (!provider.IsEnvironmentReady())
            throw new DirectoryNotFoundException(
                $"Python environment is not ready at {provider.PythonHome}.");

        await provider.InstallPackagesAsync(depList, progress, cancellationToken).ConfigureAwait(false);

        progress.Report($"All {depList.Count} package(s) installed.");
    }

    private static async Task<ParseResult> RunParserAsync(
        PyEnvironmentProvider provider,
        string scriptPath,
        string stdinContent,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var cmd = await Cli.Wrap(provider.PythonExe)
            .WithArguments([PythonEmbedded.ParserScriptPath, scriptPath])
            .WithWorkingDirectory(provider.PythonHome)
            .WithStandardInputPipe(PipeSource.FromString(stdinContent))
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cmd.ExitCode != 0)
        {
            var err = stderr.ToString().Trim();
            throw new InvalidOperationException($"Parser.py failed (exit {cmd.ExitCode}): {err}");
        }

        var json = stdout.ToString().Trim();
        if (string.IsNullOrEmpty(json)) return ParseResult.Empty;

        return JsonSerializer.Deserialize<ParseResult>(json) ?? ParseResult.Empty;
    }

    /// <summary>
    /// Invalidate Python import caches and add site-packages to sys.path so newly
    /// installed packages are importable in the current process.
    /// </summary>
    public static void RefreshImportCache(PythonInitializer initializer)
    {
        if (!initializer.IsInitialized) return;

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Exec("""
                import importlib, os, sys
                importlib.invalidate_caches()
                site_packages = os.path.join(sys.prefix, "Lib", "site-packages")
                if os.path.isdir(site_packages) and site_packages not in sys.path:
                    sys.path.insert(0, site_packages)
                """);
        }
    }
}
