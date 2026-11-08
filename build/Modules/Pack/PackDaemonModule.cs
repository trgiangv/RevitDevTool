using Build.Options;
using Build.Pipeline;
using Build.Releases;
using Build.Tools;
using Build.Trains;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Modules;
using Sourcy.DotNet;

namespace Build.Modules;

/// <summary>
///     Publish DevTools.Daemon self-contained and pack it with Velopack (<c>vpk</c> from the repo tool manifest).
///     The TestRunner ships in RevitDevTool.TestAdapter, not here.
///     Output goes to <c>output/daemon</c>: <c>RevitDevTool-Setup.exe</c>, the full and delta packages of this version, the release feed.
/// </summary>
/// <remarks>
///     <c>vpk pack</c> builds a delta from the newest full package in the folder and lists the packages it finds there.
///     A release clears the folder and downloads the feed and full package of the previous release of its channel, so the feed
///     names this version and one delta base. A local <c>pack</c> keeps the folder, so the next local pack builds a delta.
/// </remarks>
/// <seealso href="../../docs/decisions/0053-velopack-for-daemon-distribution.md">0053</seealso>
[DependsOn<CompileTrainModule>]
[DependsOn<TestTrainModule>(Optional = true)]
[UsedImplicitly]
public sealed class PackDaemonModule(
    BuildRun run,
    IOptions<BundleOptions> bundleOptions,
    IOptions<UpdateOptions> updateOptions) : Module<string>
{
    private const string DaemonExecutable = "DevTools.Daemon.exe";
    private const string NonReleaseCore = "0.0.0-";
    private const string VelopackLowestCore = "0.0.1-";
    private static readonly string[] VelopackArtifacts = ["*.nupkg", "releases.*.json", "assets.*.json", "RELEASES*", "*-Setup.exe"];

    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioning = run.Version;
        var version = VelopackVersion(versioning.Version);
        var packId = updateOptions.Value.PackId;
        var channel = ReleaseAssets.VelopackChannel(versioning.IsPrerelease);
        var outputPath = Directory.CreateDirectory(run.Output).FullName;
        var daemonFolder = Path.GetDirectoryName(Projects.DevTools_Daemon.FullName)!;
        var publishPath = Path.Combine(daemonFolder, "bin", "Release", "velopack");
        if (Directory.Exists(publishPath)) Directory.Delete(publishPath, recursive: true);

        if (run.Target == BuildTarget.Release)
        {
            DeleteVelopackArtifacts(outputPath);
            DownloadDeltaBase(context, outputPath, channel);
        }

        await PublishAsync(context, Projects.DevTools_Daemon.FullName, publishPath, version, cancellationToken);

        await ModuleShell.DotNetAsync(context, run.Root, ["tool", "restore"], cancellationToken);

        var icon = Path.Combine(daemonFolder, "..", "DevTools.UI", "Resources", "icons", "DevTools-32-Dark.ico");
        await ModuleShell.DotNetAsync(context, run.Root,
        [
            "vpk", "pack",
            "--packId", packId,
            "--packVersion", version,
            "--packDir", publishPath,
            "--mainExe", DaemonExecutable,
            "--packTitle", packId,
            "--packAuthors", bundleOptions.Value.VendorName ?? packId,
            "--icon", Path.GetFullPath(icon),
            "--shortcuts", "StartMenuRoot",
            "--noPortable",
            "--channel", channel,
            "--outputDir", outputPath
        ], cancellationToken);

        KeepThisVersion(outputPath, packId, version);
        var setupPath = RenameSetup(outputPath, packId);

        context.Summary.KeyValue("Artifacts", "Setup", setupPath);
        return setupPath;
    }

    private static Task PublishAsync(IModuleContext context, string project, string output, string version, CancellationToken cancellationToken)
    {
        return context.DotNet().Publish(new DotNetPublishOptions
        {
            ProjectSolution = project,
            Configuration = "Release",
            Output = output,
            Properties =
            [
                ("VelopackPublish", "true"),
                ("Version", version),
                ("BuildProjectReferences", "false")
            ]
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     <c>vpk</c> refuses versions below <c>0.0.1</c>, so a <c>0.0.0-ci</c> or <c>0.0.0-local</c> pack becomes <c>0.0.1-ci</c> or <c>0.0.1-local</c>.
    /// </summary>
    private static string VelopackVersion(string version) =>
        version.StartsWith(NonReleaseCore, StringComparison.Ordinal) ? VelopackLowestCore + version[NonReleaseCore.Length..] : version;

    private static void DeleteVelopackArtifacts(string outputPath)
    {
        foreach (var file in VelopackArtifacts.SelectMany(pattern => Directory.GetFiles(outputPath, pattern)))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    ///     No previous release in the channel means no delta. <c>vpk download github</c> is not used: it reads the repository's latest
    ///     release, which can belong to another train.
    /// </summary>
    private void DownloadDeltaBase(IModuleContext context, string outputPath, string channel)
    {
        var previous = TrainVersion.PickSameChannel(run.Train, Git.Tags(run.Root, run.Train), run.Version.Version);
        if (previous is null)
        {
            context.Logger.LogInformation("No earlier {Channel} release; this release has no delta package", channel);
            return;
        }

        Gh.Download(run.Root, run.Train.Tag(previous), outputPath, $"releases.{channel}.json", "*-full.nupkg");
    }

    /// <summary>
    ///     The release carries only this version's packages; the delta base stays named in the feed.
    ///     The legacy <c>RELEASES</c> file is dropped: no client reads it.
    /// </summary>
    private static void KeepThisVersion(string outputPath, string packId, string version)
    {
        var stale = Directory.GetFiles(outputPath, "*.nupkg")
            .Where(file => !Path.GetFileName(file).StartsWith($"{packId}-{version}-", StringComparison.Ordinal))
            .Concat(Directory.GetFiles(outputPath).Where(file => Path.GetFileName(file).StartsWith("RELEASES", StringComparison.Ordinal)));
        foreach (var file in stale) File.Delete(file);
    }

    /// <summary>
    ///     <c>vpk</c> names the installer <c>&lt;packId&gt;-&lt;channel&gt;-Setup.exe</c>. The release keeps one stable name,
    ///     so <c>releases/latest/download/RevitDevTool-Setup.exe</c> always resolves.
    /// </summary>
    private static string RenameSetup(string outputPath, string packId)
    {
        var setup = Directory.GetFiles(outputPath, $"{packId}-*-Setup.exe").Single();
        var target = Path.Combine(outputPath, ReleaseAssets.SetupName);
        File.Move(setup, target, overwrite: true);
        return target;
    }
}
