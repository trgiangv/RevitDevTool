using System.Text;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Services;

internal sealed class UvPackageStore(PythonInitializer initializer) : IPythonPackageStore
{
    public PythonBackend Backend => PythonBackend.Uv;

    public async Task<IReadOnlyList<Package>> ListAsync(CancellationToken cancellationToken)
    {
        var provider = RequireProvider();
        if (provider is null || !UvInstaller.IsUvInstalled())
            return [];

        var stdout = new StringBuilder();
        var exit = await UvEnvironmentProvider.RunUvAsync(
                UvEnvironmentProvider.UvArgs.PipListJson(provider.PythonExe),
                line => stdout.AppendLine(line),
                onStderr: null,
                cancellationToken)
            .ConfigureAwait(false);

        if (exit != 0)
            return [];

        var json = stdout.ToString().Trim();
        return string.IsNullOrEmpty(json) ? [] : PyPiPackageList.Parse(json);
    }

    public Task RemoveAsync(Package package, CancellationToken cancellationToken)
        => RemoveAsync(package.PackageId, cancellationToken);

    public async Task UpdateAsync(Package package, CancellationToken cancellationToken)
    {
        var provider = RequireProvider();
        if (provider is null || string.IsNullOrWhiteSpace(package.PackageId))
            return;

        var upgrade = !(package.IsProtected && !string.IsNullOrWhiteSpace(package.Version));
        var spec = upgrade
            ? package.PackageId
            : $"{package.PackageId}=={package.Version}";

        await UvEnvironmentProvider.RunUvAsync(
                UvEnvironmentProvider.UvArgs.PipInstall(provider.PythonExe, [spec], upgrade),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
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

        await UvEnvironmentProvider.RunUvAsync(
                UvEnvironmentProvider.UvArgs.PipInstall(provider.PythonExe, [spec]),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RemoveAsync(string packageId, CancellationToken cancellationToken)
    {
        var provider = RequireProvider();
        if (provider is null || string.IsNullOrWhiteSpace(packageId))
            return;

        await UvEnvironmentProvider.RunUvAsync(
                UvEnvironmentProvider.UvArgs.PipUninstall(provider.PythonExe, packageId),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private PyEnvironmentProvider? RequireProvider()
    {
        var provider = initializer.Provider;
        return provider is { Backend: PythonBackend.Uv } && provider.IsEnvironmentReady()
            ? provider
            : null;
    }
}
