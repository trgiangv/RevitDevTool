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
    /// For Pixi: Parser.py receives pixi.toml via stdin and filters internally.
    /// For Pip: stdin is empty, all declared deps are returned — pip install is idempotent.
    /// </summary>
    public static async Task<List<string>> ResolveDependenciesAsync(
        IPythonEnvironmentProvider provider,
        string scriptPath,
        CancellationToken cancellationToken = default)
    {
        var pixiToml = provider.Backend == PythonBackend.Pixi
            ? await ReadPixiTomlAsync(cancellationToken).ConfigureAwait(false)
            : string.Empty;

        var result = await RunParserAsync(scriptPath, pixiToml, cancellationToken).ConfigureAwait(false);
        return result.ToInstall;
    }

    /// <summary>
    /// Installs <paramref name="dependencies"/> into the Python env via the given provider.
    /// </summary>
    public static async Task InstallDependenciesAsync(
        IPythonEnvironmentProvider provider,
        IEnumerable<string> dependencies,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var depList = dependencies.ToList();
        if (depList.Count == 0) return;

        progress.Report($"Installing {depList.Count} package(s)...");

        if (!PythonEnvironment.IsEnvironmentReady())
            throw new DirectoryNotFoundException(
                $"Python environment is not ready at {PythonEnvironment.PythonHome}.");

        await provider.InstallPackagesAsync(depList, progress, cancellationToken).ConfigureAwait(false);

        progress.Report($"All {depList.Count} package(s) installed.");
    }

    private static async Task<string> ReadPixiTomlAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(PythonEnvironment.PixiProjectDir, "pixi.toml");
        if (!File.Exists(path)) return string.Empty;
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ParseResult> RunParserAsync(
        string scriptPath,
        string stdinContent,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var cmd = await Cli.Wrap(PythonEnvironment.PythonExe)
            .WithArguments([PythonEmbedded.ParserScriptPath, scriptPath])
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
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
