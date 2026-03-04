using System.Diagnostics;
using System.IO;
using CliWrap;
using RevitDevTool.Utils;
namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Manages the Pixi-driven Python environment at %AppData%\RevitDevTool\pixi-env\.
/// Package install strategy: conda-forge first, PyPI fallback via pixi's embedded uv.
/// </summary>
public static class PixiEnvironment
{
    private const string PythonConstraint = "3.13.*";
    private const string PythonDllName = "python313.dll";
    private const string PixiEnvDirName = "pixi-env";
    public static IReadOnlyCollection<string> RequirePackages =>
    [
        "modelcontextprotocol",
        "debugpy",
        "pytest",
        "pywin32",
        "packaging"
    ];
    private const string PixiTomlTemplate = $"""
                                             [workspace]
                                             name = "revitdevtool-runtime"
                                             version = "0.1.0"
                                             channels = ["conda-forge"]
                                             platforms = ["win-64"]

                                             [dependencies]
                                             python = "{PythonConstraint}"
                                             packaging = "*"
                                             debugpy = "*"
                                             pytest = "*"
                                             pywin32 = "*"

                                             [pypi-dependencies]
                                             modelcontextprotocol = "*"
                                             """;
    private const string PixiEnvDir = @".pixi\envs\default";
    public static string PixiProjectDir => Path.Combine(SettingsUtils.GetApplicationDataPath(), PixiEnvDirName);
    public static string PythonHome => Path.Combine(PixiProjectDir, PixiEnvDir);
    public static string PythonExe => Path.Combine(PythonHome, "python.exe");
    public static string ParserScriptPath => Path.Combine(PixiProjectDir, "parser.py");

    public static void EnsureParserScript()
    {
        if (File.Exists(ParserScriptPath)) return;
        ExtractParserScript();
    }

    public static void ExtractParserScript()
    {
        Directory.CreateDirectory(PixiProjectDir);

        var assembly = typeof(PixiEnvironment).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
                               .FirstOrDefault(n => n.EndsWith("Parser.py", StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException("Embedded resource Parser.py not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var file   = File.Create(ParserScriptPath);
        stream.CopyTo(file);
        Debug.WriteLine($"Extracted Parser.py to: {ParserScriptPath}");
    }

    public static bool IsEnvironmentReady() => File.Exists(PythonExe);

    public static Task SetupEnvironmentAsync()
    {
        Directory.CreateDirectory(PixiProjectDir);
        EnsurePixiToml();
        return RunPixiInstallAsync();
    }

    public static string GetPythonDllPath()
    {
        if (!Directory.Exists(PythonHome))
            throw new DirectoryNotFoundException($"Pixi Python env not found at: {PythonHome}");

        var exactPath = Path.Combine(PythonHome, PythonDllName);
        if (File.Exists(exactPath)) return exactPath;

        var dll = Directory.GetFiles(PythonHome, "python*.dll").FirstOrDefault();
        return dll ?? throw new FileNotFoundException("Python DLL not found in pixi env.", PythonHome);
    }

    public static async Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var list = packages.ToList();
        if (list.Count == 0) return;

        var pixi = PixiInstaller.PixiExePath;

        // ── 1. Attempt batch conda-forge install ──────────────────────────────
        progress.Report($"Trying conda-forge for {list.Count} package(s): {string.Join(", ", list)}");

        var (condaSuccess, condaFailed) = await TryPixiAddBatchAsync(
            pixi, list, pypi: false, progress, cancellationToken).ConfigureAwait(false);

        if (condaFailed.Count == 0)
        {
            progress.Report($"✓ All {list.Count} package(s) installed from conda-forge.");
            return;
        }

        if (condaSuccess.Count > 0)
            progress.Report($"✓ conda-forge: {string.Join(", ", condaSuccess)}");

        // ── 2. Fallback: pixi add --pypi  (pixi resolves via embedded uv library)
        //      e.g.  pixi add black --pypi
        progress.Report($"Falling back to PyPI for: {string.Join(", ", condaFailed)}");

        var (pypiSuccess, pypiFailed) = await TryPixiAddBatchAsync(
            pixi, condaFailed, pypi: true, progress, cancellationToken).ConfigureAwait(false);

        if (pypiSuccess.Count > 0)
            progress.Report($"✓ PyPI: {string.Join(", ", pypiSuccess)}");

        if (pypiFailed.Count > 0)
            throw new Exception($"Failed to install the following package(s): {string.Join(", ", pypiFailed)}");

        progress.Report($"✓ All {list.Count} package(s) installed.");
    }

    private static void EnsurePixiToml()
    {
        var tomlPath = Path.Combine(PixiProjectDir, "pixi.toml");
        if (File.Exists(tomlPath)) return;
        File.WriteAllText(tomlPath, PixiTomlTemplate);
        Debug.WriteLine($"Created pixi.toml at: {tomlPath}");
    }

    private static async Task RunPixiInstallAsync()
    {
        Debug.WriteLine("Running pixi install to bootstrap Python environment...");

        var result = await Cli.Wrap(PixiInstaller.PixiExePath)
            .WithArguments("install")
            .WithWorkingDirectory(PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Trace.TraceInformation($"[pixi] {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Trace.TraceWarning($"[pixi] {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync().ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new Exception($"pixi install failed with exit code {result.ExitCode}.");

        Debug.WriteLine("Pixi Python environment ready.");
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
            .WithWorkingDirectory(PixiProjectDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (batchResult.ExitCode == 0)
            return (pkgs, []);
        
        // Batch failed → retry individually to isolate which packages fail
        var succeeded = new List<string>();
        var failed    = new List<string>();

        foreach (var pkg in pkgs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var singleArgs = BuildPixiAddArgs([pkg], pypi);
            var singleResult = await Cli.Wrap(pixiExe)
                .WithArguments(singleArgs)
                .WithWorkingDirectory(PixiProjectDir)
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
