using System.Text;
using CliWrap;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Services;

internal sealed class PipPackageStore(PythonInitializer initializer) : IPythonPackageStore
{
    public PythonBackend Backend => PythonBackend.Pip;

    public async Task<IReadOnlyList<Package>> ListAsync(CancellationToken cancellationToken)
    {
        var provider = RequireProvider();
        if (provider is null)
            return [];

        var stdout = new StringBuilder();
        var result = await Cli.Wrap(provider.PythonExe)
            .WithArguments(["-m", "pip", "list", "--format=json"])
            .WithWorkingDirectory(provider.PythonHome)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
            return [];

        var json = stdout.ToString().Trim();
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return PyPiPackageList.Parse(json);
        }
        catch
        {
            return [];
        }
    }

    public Task RemoveAsync(Package package, CancellationToken cancellationToken)
        => RemoveAsync(package.PackageId, cancellationToken);

    public async Task UpdateAsync(Package package, CancellationToken cancellationToken)
    {
        var provider = RequireProvider();
        if (provider is null || string.IsNullOrWhiteSpace(package.PackageId))
            return;

        var args = package.IsProtected && !string.IsNullOrWhiteSpace(package.Version)
            ? new[] { "-m", "pip", "install", "--prefer-binary", $"{package.PackageId}=={package.Version}" }
            : new[] { "-m", "pip", "install", "--upgrade", "--prefer-binary", package.PackageId };

        await Cli.Wrap(provider.PythonExe)
            .WithArguments(args)
            .WithWorkingDirectory(provider.PythonHome)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RepairAsync(Package package, CancellationToken cancellationToken)
    {
        await RemoveAsync(package.PackageId, cancellationToken).ConfigureAwait(false);
        await InstallAsync(package.PackageId, package.DeclaredVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task InstallAsync(string packageId, string? declaredVersion, CancellationToken cancellationToken)
    {
        var provider = RequireProvider();
        if (provider is null || string.IsNullOrWhiteSpace(packageId))
            return;

        var spec = string.IsNullOrWhiteSpace(declaredVersion) || declaredVersion!.Trim() == "*"
            ? packageId
            : $"{packageId}=={declaredVersion.Trim()}";

        await Cli.Wrap(provider.PythonExe)
            .WithArguments(["-m", "pip", "install", "--prefer-binary", spec])
            .WithWorkingDirectory(provider.PythonHome)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveAsync(string packageId, CancellationToken cancellationToken)
    {
        var provider = RequireProvider();
        if (provider is null || string.IsNullOrWhiteSpace(packageId))
            return;

        await Cli.Wrap(provider.PythonExe)
            .WithArguments(["-m", "pip", "uninstall", "-y", packageId])
            .WithWorkingDirectory(provider.PythonHome)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private PyEnvironmentProvider? RequireProvider()
    {
        var provider = initializer.Provider;
        return provider is { Backend: PythonBackend.Pip } && provider.IsEnvironmentReady()
            ? provider
            : null;
    }
}
