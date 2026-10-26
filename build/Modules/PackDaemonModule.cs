using Build.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;
using ModularPipelines.Options;
using Sourcy.DotNet;
using System.Security.Cryptography;

namespace Build.Modules;

/// <summary>
///     Publish DevTools.Daemon and DevTools.TestRunner self-contained into one folder with one shared .NET runtime,
///     and pack it with Velopack (<c>vpk</c> from the repo tool manifest).
///     Output goes straight to <c>output</c>: <c>RevitDevTool-Setup.exe</c>, full and delta packages, the release feed.
/// </summary>
/// <remarks>
///     Runs after <see cref="CompileDaemonModule"/> so the two publishes of the same project do not share <c>obj</c> at once.
///     A release build downloads the previous release first so <c>vpk pack</c> can build a delta.
///     A CI build has no delta, because its version can be lower than a published prerelease.
/// </remarks>
/// <seealso href="../../docs/decisions/0053-velopack-for-daemon-distribution.md">0053</seealso>
[DependsOn<ResolveVersionModule>]
[DependsOn<CompileDaemonModule>]
[DependsOn<CompileTestRunnerModule>]
[UsedImplicitly]
public sealed class PackDaemonModule(
    IOptions<BuildOptions> buildOptions,
    IOptions<BundleOptions> bundleOptions,
    IOptions<UpdateOptions> updateOptions) : Module<string>
{
    private const string DaemonExecutable = "DevTools.Daemon.exe";
    private const string PrereleaseChannel = "prerelease";
    private static readonly string[] VelopackArtifacts = ["*.nupkg", "releases.*.json", "assets.*.json", "RELEASES*", "*-Setup.exe"];

    protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var versioning = versioningResult.ValueOrDefault!;

        var root = context.Git().RootDirectory;
        var outputPath = Directory.CreateDirectory(root.GetFolder(buildOptions.Value.OutputDirectory).Path).FullName;
        var daemonFolder = Path.GetDirectoryName(Projects.DevTools_Daemon.FullName)!;
        var publishPath = Path.Combine(daemonFolder, "bin", "Release", "velopack");
        if (Directory.Exists(publishPath)) Directory.Delete(publishPath, recursive: true);
        DeleteVelopackArtifacts(outputPath);

        var runnerPath = publishPath + "-runner";
        if (Directory.Exists(runnerPath)) Directory.Delete(runnerPath, recursive: true);
        await PublishAsync(context, Projects.DevTools_Daemon.FullName, publishPath, versioning, cancellationToken);
        await PublishAsync(context, Projects.DevTools_TestRunner.FullName, runnerPath, versioning, cancellationToken);
        MergeSharedRuntime(runnerPath, publishPath);

        await RunVpkAsync(context, root.Path, ["tool", "restore"], throwOnError: true, cancellationToken);

        var channel = versioning.IsPrerelease ? PrereleaseChannel : null;
        if (updateOptions.Value.RequireSignature)
            await DownloadPreviousAsync(context, root.Path, outputPath, channel, versioning.IsPrerelease, cancellationToken);

        var icon = Path.Combine(daemonFolder, "..", "DevTools.UI", "Resources", "icons", "DevTools-32-Dark.ico");
        List<string> pack =
        [
            "vpk", "pack",
            "--packId", updateOptions.Value.PackId,
            "--packVersion", versioning.Version,
            "--packDir", publishPath,
            "--mainExe", DaemonExecutable,
            "--packTitle", updateOptions.Value.PackId,
            "--packAuthors", bundleOptions.Value.VendorName ?? updateOptions.Value.PackId,
            "--icon", Path.GetFullPath(icon),
            "--shortcuts", "StartMenuRoot",
            "--noPortable",
            "--outputDir", outputPath
        ];
        if (channel is not null) pack.AddRange(["--channel", channel]);

        await RunVpkAsync(context, root.Path, pack, throwOnError: true, cancellationToken);

        foreach (var assets in Directory.GetFiles(outputPath, "assets.*.json")) File.Delete(assets);
        var setupPath = RenameSetup(outputPath, updateOptions.Value.PackId);

        context.Summary.KeyValue("Artifacts", "Setup", setupPath);
        return setupPath;
    }

    private static Task PublishAsync(IModuleContext context, string project, string output, ResolveVersioningResult versioning, CancellationToken cancellationToken)
    {
        return context.DotNet().Publish(new DotNetPublishOptions
        {
            ProjectSolution = project,
            Configuration = "Release",
            Output = output,
            Properties =
            [
                ("VelopackPublish", "true"),
                ("VersionPrefix", versioning.VersionPrefix),
                ("VersionSuffix", versioning.VersionSuffix!),
                ("BuildProjectReferences", "false")
            ]
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Copies the self-contained TestRunner into the Daemon folder, so both apps use one .NET runtime.
    ///     A file that both apps publish must be byte-identical, else one app would load the wrong version.
    /// </summary>
    private static void MergeSharedRuntime(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            if (File.Exists(destination))
            {
                if (!HaveSameContent(file, destination))
                    throw new InvalidOperationException($"DevTools.Daemon and DevTools.TestRunner publish different '{Path.GetRelativePath(source, file)}'.");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }

        Directory.Delete(source, recursive: true);
    }

    private static bool HaveSameContent(string left, string right)
    {
        if (new FileInfo(left).Length != new FileInfo(right).Length) return false;

        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        return SHA256.HashData(leftStream).AsSpan().SequenceEqual(SHA256.HashData(rightStream));
    }

    /// <summary>
    ///     Clear the previous run's Velopack files and the old Inno <c>Setup.exe</c>, so the feed and the delta base are this run's only.
    /// </summary>
    private static void DeleteVelopackArtifacts(string outputPath)
    {
        foreach (var file in VelopackArtifacts.SelectMany(pattern => Directory.GetFiles(outputPath, pattern)))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    ///     <c>vpk</c> names the installer <c>&lt;packId&gt;-&lt;channel&gt;-Setup.exe</c>. The release keeps one stable name,
    ///     so <c>releases/latest/download/RevitDevTool-Setup.exe</c> always resolves.
    /// </summary>
    private static string RenameSetup(string outputPath, string packId)
    {
        var setup = Directory.GetFiles(outputPath, $"{packId}-*-Setup.exe").Single();
        var target = Path.Combine(outputPath, $"{packId}-Setup.exe");
        File.Move(setup, target, overwrite: true);
        return target;
    }

    private async Task DownloadPreviousAsync(IModuleContext context, string workingDirectory, string outputPath, string? channel, bool prerelease, CancellationToken cancellationToken)
    {
        var repoUrl = bundleOptions.Value.VendorUrl;
        if (string.IsNullOrWhiteSpace(repoUrl)) return;

        List<string> download = ["vpk", "download", "github", "--repoUrl", repoUrl, "--outputDir", outputPath];
        if (channel is not null) download.AddRange(["--channel", channel]);
        if (prerelease) download.Add("--pre");

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token)) download.AddRange(["--token", token]);

        var result = await RunVpkAsync(context, workingDirectory, download, throwOnError: false, cancellationToken);
        if (result.ExitCode != 0)
        {
            context.Logger.LogWarning("No previous Velopack release was downloaded; this release has no delta package");
        }
    }

    private static Task<ModularPipelines.Models.CommandResult> RunVpkAsync(IModuleContext context, string workingDirectory, IEnumerable<string> arguments, bool throwOnError, CancellationToken cancellationToken)
    {
        return context.Shell.Command.ExecuteCommandLineTool(
            new GenericCommandLineToolOptions("dotnet")
            {
                Arguments = arguments.ToArray()
            },
            new CommandExecutionOptions
            {
                WorkingDirectory = workingDirectory,
                ThrowOnNonZeroExitCode = throwOnError
            },
            cancellationToken: cancellationToken);
    }
}
