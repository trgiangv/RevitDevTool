using Build.Pipeline;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Pack RevitDevTool.TestAdapter into <c>output/nettest</c> at the product version, passed to restore and pack
///     as a global property so every assembly in the package carries it. The csproj's PackRuntime target
///     restores and builds the testhost siblings and framework runtimes itself, and its PackRunner target
///     publishes the TestRunner into the package. A <c>0.0.0-local</c> version is new on every pack,
///     so a consumer restore never reuses an older extraction.
/// </summary>
[DependsOn<TestProductModule>(Optional = true)]
[UsedImplicitly]
public sealed class PackTestAdapterModule(BuildRun run) : Module<string>
{
    private const string PackageId = "RevitDevTool.TestAdapter";

    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var adapterProject = Projects.DevTools_TestAdapter.FullName;
        var version = run.Version.Version;
        var outputDir = Directory.CreateDirectory(run.Output).FullName;
        foreach (var stale in Directory.GetFiles(outputDir, $"{PackageId}.*.nupkg")) File.Delete(stale);

        await context.DotNet().Restore(new DotNetRestoreOptions
        {
            ProjectSolution = adapterProject,
            Properties = [("Version", version)]
        }, cancellationToken: cancellationToken);

        await context.DotNet().Pack(new DotNetPackOptions
        {
            ProjectSolution = adapterProject,
            Configuration = "Release",
            NoRestore = true,
            Output = outputDir,
            Properties = [("Version", version)]
        }, cancellationToken: cancellationToken);

        var package = Path.Combine(outputDir, $"{PackageId}.{version}.nupkg");
        if (!File.Exists(package))
        {
            throw new InvalidOperationException($"Expected '{package}'.");
        }

        context.Summary.KeyValue("Artifacts", "TestAdapter", package);
        return package;
    }
}
