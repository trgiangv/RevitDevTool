using System.Text;
using System.Text.Json;
using CliWrap;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.Python;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Execution.Services;

internal static class PipPackageHelper
{
    public static async Task<IReadOnlyList<Package>> ListPackagesAsync(PyEnvironmentProvider provider, CancellationToken cancellationToken)
    {
        if (!provider.IsEnvironmentReady())
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
            return ParseList(json);
        }
        catch
        {
            return [];
        }
    }

    public static async Task InstallAsync(PyEnvironmentProvider provider, string packageId, string? declaredVersion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !provider.IsEnvironmentReady())
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

    public static async Task RemoveAsync(PyEnvironmentProvider provider, string packageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !provider.IsEnvironmentReady())
            return;

        await Cli.Wrap(provider.PythonExe)
            .WithArguments(["-m", "pip", "uninstall", "-y", packageId])
            .WithWorkingDirectory(provider.PythonHome)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static List<Package> ParseList(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var packages = new List<Package>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!TryGetString(item, "name", out var name))
                continue;

            var version = TryGetString(item, "version", out var v) ? v : null;

            packages.Add(new Package(
                Marketplace.PyPi,
                name,
                version,
                version,
                PyEnvironmentProvider.RequirePackages.Contains(name, StringComparer.OrdinalIgnoreCase)));
        }
        return packages;
    }

    private static bool TryGetString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        value = text!;
        return true;
    }
}
