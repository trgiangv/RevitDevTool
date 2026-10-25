using Build.Options;
using EnumerableAsyncProcessor.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Git.Options;
using ModularPipelines.GitHub.Attributes;
using ModularPipelines.GitHub.Extensions;
using ModularPipelines.Modules;
using Octokit;
using Shouldly;
using System.Text;
using ReleaseSignature = DevTools.Update.Releases.ReleaseSignature;
using UpdateManifest = DevTools.Update.Releases.UpdateManifest;

namespace Build.Modules;

/// <summary>
///     Publish the add-in to GitHub.
/// </summary>
[SkipIfNoGitHubToken]
[DependsOn<ResolveVersionModule>]
[DependsOn<GenerateChangelogModule>]
[DependsOn<CreateBundleModule>(Optional = true)]
[DependsOn<GenerateUpdateManifestModule>(Optional = true)]
[DependsOn<PackDaemonModule>(Optional = true)]
[DependsOn<SignUpdateFeedsModule>(Optional = true)]
[DependsOn<VerifyReleaseModule>(Optional = true)]
[UsedImplicitly]
public sealed class PublishGithubModule(IOptions<BuildOptions> buildOptions) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var changelogResult = await context.GetModule<GenerateChangelogModule>();
        var versioning = versioningResult.ValueOrDefault!;
        var changelog = AppendGitHubCompareUrl(context, changelogResult.ValueOrDefault!, versioning);

        var outputFolder = context.Git().RootDirectory.GetFolder(buildOptions.Value.OutputDirectory);
        var targetFiles = CollectAssets(outputFolder, versioning.Version);
        targetFiles.ShouldNotBeEmpty("No artifacts were found to create the Release");

        var feeds = targetFiles.Where(IsFeed).ToArray();
        var payloads = targetFiles.Except(feeds).ToArray();

        var repositoryInfo = context.GitHub().RepositoryInfo;
        var client = context.GitHub().Client.Repository.Release;
        var newRelease = new NewRelease(versioning.Version)
        {
            Name = versioning.Version,
            Body = changelog,
            TargetCommitish = context.Git().Information.LastCommitSha,
            Prerelease = versioning.IsPrerelease,
            Draft = true
        };

        var release = await client.Create(repositoryInfo.Owner, repositoryInfo.RepositoryName, newRelease);
        await payloads.ForEachAsync(file => UploadAsync(context, release, file, cancellationToken), cancellationToken).ProcessInParallel();
        foreach (var feed in feeds)
        {
            await UploadAsync(context, release, feed, cancellationToken);
        }

        release = await client.Edit(repositoryInfo.Owner, repositoryInfo.RepositoryName, release.Id, new ReleaseUpdate { Draft = false });
        context.Summary.KeyValue("Deployment", "GitHub", release.HtmlUrl);
    }

    /// <summary>
    ///     Top-level files in <c>output</c>. The previous release's full package that <see cref="PackDaemonModule"/>
    ///     downloads for deltas is left out.
    /// </summary>
    private static ModularPipelines.FileSystem.File[] CollectAssets(ModularPipelines.FileSystem.Folder outputFolder, string version)
    {
        return outputFolder.ListFiles()
            .Where(file => !file.Name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) ||
                           file.Name.Contains($"-{version}-", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    ///     Feeds are uploaded after every payload, so a client never reads a feed that names a missing asset.
    /// </summary>
    private static bool IsFeed(ModularPipelines.FileSystem.File file)
    {
        return file.Name == UpdateManifest.FileName ||
               file.Name.EndsWith(ReleaseSignature.Extension, StringComparison.OrdinalIgnoreCase) ||
               file.Name.StartsWith("releases.", StringComparison.OrdinalIgnoreCase) ||
               file.Name.StartsWith("RELEASES", StringComparison.Ordinal);
    }

    private static async Task UploadAsync(IModuleContext context, Release release, ModularPipelines.FileSystem.File file, CancellationToken cancellationToken)
    {
        var asset = new ReleaseAssetUpload
        {
            ContentType = "application/x-binary",
            FileName = file.Name,
            RawData = file.GetStream()
        };

        context.Logger.LogInformation("Uploading asset: {Asset}", asset.FileName);
        await context.GitHub().Client.Repository.Release.UploadAsset(release, asset, cancellationToken);
    }

    private static string AppendGitHubCompareUrl(IModuleContext context, string changelog, ResolveVersioningResult versioning)
    {
        if (changelog.Contains("Full changelog", StringComparison.OrdinalIgnoreCase))
        {
            return changelog;
        }

        var repositoryInfo = context.GitHub().RepositoryInfo;
        var builder = new StringBuilder(changelog);
        builder.AppendLine().AppendLine().Append("**Full changelog**: ");
        builder.Append("https://github.com/").Append(repositoryInfo.Identifier);
        builder.Append("/compare/").Append(versioning.PreviousVersion).Append("...").AppendLine(versioning.Version);
        return builder.ToString();
    }

    protected override async Task OnFailedAsync(IModuleContext context, Exception exception, CancellationToken cancellationToken)
    {
        var versioningResult = await context.GetModule<ResolveVersionModule>();
        var versioning = versioningResult.ValueOrDefault!;

        await context.Git().Commands.Push(new GitPushOptions
        {
            Delete = true,
            Arguments = ["origin", versioning.Version]
        }, token: cancellationToken);
    }
}
