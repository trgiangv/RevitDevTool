using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
namespace RevitDevTool.Execution.Providers.Python;

public static class PythonDepsManager
{
    private sealed record ParseResult(
        [property: JsonPropertyName("requires_python")] string       RequiresPython,
        [property: JsonPropertyName("to_install")]      List<string> ToInstall)
    {
        public static readonly ParseResult Empty = new(string.Empty, []);
    }

    /// <summary>
    /// Resolves which packages from the PEP 723 metadata in <paramref name="scriptPath"/>
    /// are missing or version-mismatched in the current pixi environment.
    /// Returns empty list when no PEP 723 block is found or everything is up-to-date.
    /// </summary>
    public static async Task<List<string>> ResolveDependenciesAsync(
        string scriptPath,
        CancellationToken cancellationToken = default)
    {
        // 1. Read explicitly managed packages from pixi.toml (not 'pixi list' — avoids transitive deps noise)
        var pixiTomlContent = await ReadPixiTomlAsync(cancellationToken).ConfigureAwait(false);

        // 2. Run Parser.py: parse PEP 723 + compare against pixi.toml declarations (Python-side, correct canonicalisation)
        var result = await RunParserAsync(scriptPath, pixiTomlContent, cancellationToken).ConfigureAwait(false);

        return result.ToInstall;
    }

    /// <summary>
    /// Installs <paramref name="dependencies"/> into the pixi-managed Python env,
    /// reporting progress through <paramref name="progress"/>.
    /// </summary>
    public static async Task InstallDependenciesAsync(
        IEnumerable<string> dependencies,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var depList = dependencies.ToList();
        if (depList.Count == 0) return;

        progress.Report($"Installing {depList.Count} package(s)...");

        EnsureEnvironmentReady();

        await PythonEnvironment.InstallPackagesAsync(depList, progress, cancellationToken).ConfigureAwait(false);

        progress.Report($"All {depList.Count} package(s) installed.");
    }

    /// <summary>
    /// Reads pixi.toml as text. Returns empty string when not yet created.
    /// Only explicitly declared packages appear here — no transitive dependencies.
    /// </summary>
    private static async Task<string> ReadPixiTomlAsync(CancellationToken cancellationToken)
    {
        var tomlPath = PythonEnvironment.PixiTomlPath;
        if (!File.Exists(tomlPath)) return string.Empty;
#if NET
        return await File.ReadAllTextAsync(tomlPath, cancellationToken).ConfigureAwait(false);
#else
        return await Task.Run(() => File.ReadAllText(tomlPath), cancellationToken).ConfigureAwait(false);
#endif
    }

    /// <summary>
    /// Runs Parser.py with <paramref name="pixiTomlContent"/> piped to stdin.
    /// Python handles PEP 723 parsing + package name canonicalisation.
    /// </summary>
    private static async Task<ParseResult> RunParserAsync(
        string scriptPath,
        string pixiTomlContent,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var cmd = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(["run", "python", PythonEnvironment.ParserScriptPath, scriptPath])
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithStandardInputPipe(PipeSource.FromString(pixiTomlContent))
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

    private static void EnsureEnvironmentReady()
    {
        if (!PythonInstaller.IsPixiInstalled())
            throw new FileNotFoundException(
                "pixi.exe not found. Python runtime must be initialised before installing packages.",
                PythonInstaller.PixiExePath);

        if (!PythonEnvironment.IsEnvironmentReady())
            throw new DirectoryNotFoundException(
                $"Pixi Python environment is not ready at {PythonEnvironment.PythonHome}. " +
                "Call PythonInitializer.InitializeAsync() first.");

        Trace.TraceInformation($"Pixi environment ready: {PythonEnvironment.PythonExe}");
    }
}
