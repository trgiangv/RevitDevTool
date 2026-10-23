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

namespace Build.Modules;

/// <summary>
///     Publish the add-in to GitHub.
/// </summary>
[SkipIfNoGitHubToken]
[DependsOn<ResolveVersionModule>]
[DependsOn<GenerateChangelogModule>]
[DependsOn<CreateBundleModule>(Optional = true)]
[DependsOn<CreateInstallerModule>(Optional = true)]
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
        var targetFiles = outputFolder.ListFiles().ToArray();
        targetFiles.ShouldNotBeEmpty("No artifacts were found to create the Release");

        var repositoryInfo = context.GitHub().RepositoryInfo;
        var newRelease = new NewRelease(versioning.Version)
        {
            Name = versioning.Version,
            Body = changelog,
            TargetCommitish = context.Git().Information.LastCommitSha,
            Prerelease = versioning.IsPrerelease
        };

        var release = await context.GitHub().Client.Repository.Release.Create(repositoryInfo.Owner, repositoryInfo.RepositoryName, newRelease);
        await targetFiles.ForEachAsync(async file =>
        {
            var asset = new ReleaseAssetUpload
            {
                ContentType = "application/x-binary",
                FileName = file.Name,
                RawData = file.GetStream()
            };

            context.Logger.LogInformation("Uploading asset: {Asset}", asset.FileName);

            await context.GitHub().Client.Repository.Release.UploadAsset(release, asset, cancellationToken);
        }, cancellationToken).ProcessInParallel();

        context.Summary.KeyValue("Deployment", "GitHub", release.HtmlUrl);
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
