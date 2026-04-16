using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
namespace RevitDevTool.Execution.Providers.Python;

public static class PythonDepsManager
{
    private sealed record ParseResult(
        [property: JsonPropertyName("requires_python")] string RequiresPython, 
        [property: JsonPropertyName("to_install")] List<string> ToInstall)
    {
        public static readonly ParseResult Empty = new(string.Empty, []);
    }

    /// <summary>
    /// Resolves which packages from the PEP 723 metadata in <paramref name="scriptPath"/>
    /// need to be installed.
    /// Both backends pass their installed-package state via stdin so Parser.py
    /// can filter locally: Pixi sends pixi.toml (TOML), Pip sends pip list JSON.
    /// </summary>
    public static async Task<List<string>> ResolveDependenciesAsync(
        PyEnvironmentProvider provider,
        string scriptPath,
        CancellationToken cancellationToken = default)
    {
        var stdinContent = provider.Backend == PythonBackend.Pixi
            ? await RunPixiListAsync(cancellationToken).ConfigureAwait(false)
            : await RunPipListAsync(provider, cancellationToken).ConfigureAwait(false);

        var result = await RunParserAsync(provider, scriptPath, stdinContent, cancellationToken).ConfigureAwait(false);
        return result.ToInstall;
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

    private static async Task<string> RunPipListAsync(
        PyEnvironmentProvider provider,
        CancellationToken cancellationToken)
    {
        if (!provider.IsEnvironmentReady()) return string.Empty;

        var stdout = new StringBuilder();
        var result = await Cli.Wrap(provider.PythonExe)
            .WithArguments(["-m", "pip", "list", "--format=json"])
            .WithWorkingDirectory(provider.PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0 ? stdout.ToString().Trim() : string.Empty;
    }

    private static async Task<string> RunPixiListAsync(CancellationToken cancellationToken)
    {
        var path = PythonEmbedded.PixiTomlPath;
        if (!File.Exists(path)) return string.Empty;
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
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
}
