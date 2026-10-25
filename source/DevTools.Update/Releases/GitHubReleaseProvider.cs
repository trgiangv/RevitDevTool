using Octokit;

namespace DevTools.Update.Releases;

/// <summary>
///     Reads releases with the GitHub REST API (Octokit). One check uses one request.
///     Without a token, GitHub allows 60 requests each hour for each IP address.
/// </summary>
public sealed class GitHubReleaseProvider(IGitHubClient client, string owner, string name) : IReleaseProvider
{
    private const int PrereleasePageSize = 10;

    public GitHubReleaseProvider(string repository, string? token = null)
        : this(CreateClient(token), repository.Split('/')[0], repository.Split('/')[1])
    {
    }

    private static GitHubClient CreateClient(string? token)
    {
        var client = new GitHubClient(new ProductHeaderValue("RevitDevTool-Updater"));
        if (!string.IsNullOrWhiteSpace(token)) client.Credentials = new Credentials(token);
        return client;
    }

    public Task<Release?> GetLatestAsync(bool prerelease, CancellationToken cancellationToken) =>
        prerelease ? GetLatestPrereleaseAsync() : CallAsync(() => client.Repository.Release.GetLatest(owner, name));

    public Task<Release?> GetAsync(string version, CancellationToken cancellationToken) =>
        CallAsync(() => client.Repository.Release.Get(owner, name, version));

    private Task<Release?> GetLatestPrereleaseAsync() => CallAsync(async () =>
    {
        var releases = await client.Repository.Release.GetAll(owner, name, new ApiOptions { PageSize = PrereleasePageSize, PageCount = 1 }).ConfigureAwait(false);
        return releases.FirstOrDefault(release => !release.Draft);
    });

    private static async Task<Release?> CallAsync(Func<Task<Octokit.Release?>> call)
    {
        try
        {
            var release = await call().ConfigureAwait(false);
            return release is null ? null : ToRelease(release);
        }
        catch (NotFoundException)
        {
            return null;
        }
        catch (RateLimitExceededException ex)
        {
            throw new UpdateThrottledException(ex.Reset - DateTimeOffset.UtcNow, ex);
        }
    }

    private static Release ToRelease(Octokit.Release release) => new(
        release.TagName,
        release.Prerelease,
        release.Assets.ToDictionary(asset => asset.Name, asset => new Uri(asset.BrowserDownloadUrl), StringComparer.Ordinal));
}
