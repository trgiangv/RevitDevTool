namespace RevitDevTool.Execution.Providers.Python;

public enum PythonBackend
{
    Pixi,
    Pip
}

/// <summary>
/// Abstracts Python environment setup and package installation.
/// Two backends: Pixi (conda-forge + PyPI) and Pip (PyPI).
/// Provider is selected once at init and locked for the session.
/// </summary>
public interface IPythonEnvironmentProvider
{
    PythonBackend Backend { get; }
    bool IsEnvironmentReady();
    string GetPythonDllPath();
    Task SetupEnvironmentAsync();

    Task InstallPackagesAsync(
        IEnumerable<string> packages,
        IProgress<string> progress,
        CancellationToken cancellationToken);
}
