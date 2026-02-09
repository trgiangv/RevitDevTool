using System.Diagnostics;
using System.IO;
using CliWrap;
using CliWrap.Buffered;
using Python.Included;
using RevitDevTool.Utils;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Manages Python package dependencies using uv for fast, reliable batch installation.
/// </summary>
public static class PythonDependencyManager
{
    /// <summary>
    /// Installs all dependencies in a single batch using uv.
    /// </summary>
    public static async Task InstallDependenciesAsync(
        IEnumerable<string> dependencies, 
        IProgress<string> progress, 
        CancellationToken cancellationToken)
    {
        var depsList = dependencies.ToList();
        if (depsList.Count == 0)
            return;

        progress.Report("Verifying environment...");
        
        var pythonExe = ValidatePythonEnvironment();
        var uvPath = GetUvPath();
        
        progress.Report($"Installing {depsList.Count} package(s) with uv...");
        
        await InstallPackagesBatchAsync(uvPath, pythonExe, depsList, progress, cancellationToken).ConfigureAwait(true);

        progress.Report($"All {depsList.Count} package(s) installed successfully.");
    }

    /// <summary>
    /// Gets the path to the uv executable.
    /// </summary>
    private static string GetUvPath()
    {
        var uvPath = Path.Combine(SettingsUtils.GetApplicationDataPath(), "bin", "uv.exe");
        if (!File.Exists(uvPath))
        {
            throw new FileNotFoundException(
                "uv.exe not found. Please ensure Python runtime is initialized first.", 
                uvPath);
        }
        return uvPath;
    }

    /// <summary>
    /// Validates that the Python environment is properly set up.
    /// </summary>
    private static string ValidatePythonEnvironment()
    {
        var pythonHome = Installer.EmbeddedPythonHome;
        if (string.IsNullOrEmpty(pythonHome) || !Directory.Exists(pythonHome))
        {
            throw new DirectoryNotFoundException(
                $"Python environment not found at {pythonHome}. Please initialize Python runtime first.");
        }

        var pythonExe = Path.Combine(pythonHome, "python.exe");
        return !File.Exists(pythonExe) ? throw new FileNotFoundException("Python executable not found.", pythonExe) : pythonExe;
    }

    /// <summary>
    /// Installs all packages in a single batch using uv pip install.
    /// uv resolves all dependencies together using CDCL SAT solver for optimal conflict resolution.
    /// </summary>
    private static async Task InstallPackagesBatchAsync(
        string uvPath,
        string pythonExe, 
        List<string> packages, 
        IProgress<string> progress, 
        CancellationToken cancellationToken)
    {
        try
        {
            var args = new List<string> { "pip", "install", "--python", pythonExe };
            args.AddRange(packages);

            var result = await Cli.Wrap(uvPath)
                .WithArguments(args)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(progress.Report))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => progress.Report($"  {line}")))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken).ConfigureAwait(true);

            if (result.ExitCode != 0)
            {
                throw new Exception($"uv pip install failed with exit code {result.ExitCode}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            progress.Report($"✗ Error installing packages: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Silent check to determine if packages need installation or upgrade.
    /// Uses uv pip install --dry-run to check version constraints and dependencies.
    /// </summary>
    public static async Task<bool> NeedsInstallationAsync(
        IEnumerable<string> dependencies, 
        CancellationToken cancellationToken = default)
    {
        var depsList = dependencies.ToList();
        if (depsList.Count == 0)
            return false;

        try
        {
            var pythonExe = ValidatePythonEnvironment();
            var uvPath = GetUvPath();
            
            var args = new List<string> { "pip", "install", "--dry-run", "--python", pythonExe };
            args.AddRange(depsList);
            
            var result = await Cli.Wrap(uvPath)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken).ConfigureAwait(true);

            // If exit code != 0, something needs to be installed or there's an error
            if (result.ExitCode != 0)
                return true;

            // Check if uv reports any packages would be installed
            var output = result.StandardOutput + result.StandardError;
            return output.Contains("Would install", StringComparison.OrdinalIgnoreCase) ||
                   output.Contains("Resolved", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Error checking packages with uv: {ex.Message}");
            return true;
        }
    }
}
