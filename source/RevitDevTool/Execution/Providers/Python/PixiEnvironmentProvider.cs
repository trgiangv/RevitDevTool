using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using CliWrap;

namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Pixi-based Python environment provider.
/// Uses conda-forge first, PyPI fallback via pixi's embedded uv.
/// On setup, syncs any packages previously installed by the pip fallback provider
/// back into pixi.toml so the manifest stays the source of truth.
/// </summary>
public sealed class PixiEnvironmentProvider : IPythonEnvironmentProvider
{
    public PythonBackend Backend => PythonBackend.Pixi;

    public bool IsEnvironmentReady() => File.Exists(PythonEnvironment.PythonExe);

    public string GetPythonDllPath() => PythonEnvironment.GetPythonDllPath();

    public async Task SetupEnvironmentAsync()
    {
        PythonEmbedded.EnsureExtracted();

        await SyncPixiStateAsync().ConfigureAwait(false);

        Debug.WriteLine("Running pixi install to bootstrap Python environment...");

        var result = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments("install")
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Trace.TraceInformation($"[pixi] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Trace.TraceWarning($"[pixi] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new Exception($"pixi install failed with exit code {result.ExitCode}.");

        Debug.WriteLine("Pixi Python environment ready.");
    }

    public async Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var list = packages.ToList();
        if (list.Count == 0) return;

        var pixi = PythonInstaller.PixiExePath;

        progress.Report($"Trying conda-forge for {list.Count} package(s): {string.Join(", ", list)}");

        var (condaSuccess, condaFailed) = await TryPixiAddBatchAsync(
            pixi, list, pypi: false, progress, cancellationToken).ConfigureAwait(false);

        if (condaFailed.Count == 0)
        {
            progress.Report($"All {list.Count} package(s) installed from conda-forge.");
            return;
        }

        if (condaSuccess.Count > 0)
            progress.Report($"conda-forge: {string.Join(", ", condaSuccess)}");

        progress.Report($"Falling back to PyPI for: {string.Join(", ", condaFailed)}");

        var (pypiSuccess, pypiFailed) = await TryPixiAddBatchAsync(
            pixi, condaFailed, pypi: true, progress, cancellationToken).ConfigureAwait(false);

        if (pypiSuccess.Count > 0)
            progress.Report($"PyPI: {string.Join(", ", pypiSuccess)}");

        if (pypiFailed.Count > 0)
            throw new Exception($"Failed to install the following package(s): {string.Join(", ", pypiFailed)}");

        progress.Report($"All {list.Count} package(s) installed.");
    }

    /// <summary>
    /// When previous sessions used the pip fallback, packages may exist in
    /// site-packages but not in pixi.toml. Sync them into pixi so the manifest stays
    /// the source of truth and pixi install can reconcile the lock file.
    /// </summary>
    private static async Task SyncPixiStateAsync()
    {
        if (!File.Exists(PythonEnvironment.PythonExe)) return;

        HashSet<string> pixiPackages;
        try
        {
            pixiPackages = await RunPixiListAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Pixi] Could not read pixi packages for sync: {ex.Message}");
            return;
        }

        if (pixiPackages.Count == 0) return;

        var tomlPath = Path.Combine(PythonEnvironment.PixiProjectDir, "pixi.toml");
        var tomlContent = File.Exists(tomlPath)
            ? await File.ReadAllTextAsync(tomlPath).ConfigureAwait(false)
            : string.Empty;

        var missing = pixiPackages
            .Where(pkg => tomlContent.IndexOf(pkg, StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();

        if (missing.Count == 0) return;

        Trace.TraceInformation($"[Pixi] Syncing {missing.Count} package(s) into pixi.toml: {string.Join(", ", missing)}");

        foreach (var pkg in missing)
        {
            try
            {
                var result = await Cli.Wrap(PythonInstaller.PixiExePath)
                    .WithArguments(["add", "--pypi", pkg])
                    .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync().ConfigureAwait(false);

                if (result.ExitCode != 0)
                    Trace.TraceWarning($"[Pixi] Failed to sync package '{pkg}' into pixi.toml.");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[Pixi] Error syncing package '{pkg}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Runs <c>pixi list --json</c> and extracts canonical package names.
    /// Each entry has a "name" field.
    /// </summary>
    private static async Task<HashSet<string>> RunPixiListAsync(CancellationToken cancellationToken = default)
    {
        var stdout = new StringBuilder();

        var result = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(["list", "--json"])
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0) return [];

        var json = stdout.ToString().Trim();
        if (string.IsNullOrEmpty(json)) return [];

        using var doc = JsonDocument.Parse(json);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (entry.TryGetProperty("name", out var nameProp))
            {
                var name = nameProp.GetString();
                if (!string.IsNullOrEmpty(name))
                    names.Add(name!.ToLowerInvariant().Replace('_', '-').Replace('.', '-'));
            }
        }
        return names;
    }

    private static async Task<(List<string> Succeeded, List<string> Failed)> TryPixiAddBatchAsync(
        string pixiExe,
        List<string> pkgs,
        bool pypi,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var batchResult = await Cli.Wrap(pixiExe)
            .WithArguments(BuildPixiAddArgs(pkgs, pypi))
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (batchResult.ExitCode == 0)
            return (pkgs, []);

        var succeeded = new List<string>();
        var failed = new List<string>();

        foreach (var pkg in pkgs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var singleResult = await Cli.Wrap(pixiExe)
                .WithArguments(BuildPixiAddArgs([pkg], pypi))
                .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken).ConfigureAwait(false);

            if (singleResult.ExitCode == 0)
                succeeded.Add(pkg);
            else
                failed.Add(pkg);
        }

        return (succeeded, failed);
    }

    private static List<string> BuildPixiAddArgs(IEnumerable<string> pkgs, bool pypi)
    {
        var args = new List<string> { "add" };
        if (pypi) args.Add("--pypi");
        args.AddRange(pkgs);
        return args;
    }
}
