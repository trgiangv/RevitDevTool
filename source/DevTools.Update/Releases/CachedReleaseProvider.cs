using System.Collections.Concurrent;

namespace DevTools.Update.Releases;

/// <summary>
///     Keeps each provider answer for <see cref="Lifetime"/>. One check from the Updates tab reads the releases a few times,
///     and with the cache it sends each request one time only. A failed request is not kept.
/// </summary>
public sealed class CachedReleaseProvider(IReleaseProvider inner, TimeProvider time) : IReleaseProvider
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, (DateTimeOffset Time, Release? Release)> _answers = new(StringComparer.Ordinal);

    public Task<Release?> GetLatestAsync(bool prerelease, CancellationToken cancellationToken) =>
        GetAsync($"latest:{prerelease}", () => inner.GetLatestAsync(prerelease, cancellationToken));

    public Task<Release?> GetAsync(string version, CancellationToken cancellationToken) =>
        GetAsync($"version:{version}", () => inner.GetAsync(version, cancellationToken));

    private async Task<Release?> GetAsync(string key, Func<Task<Release?>> read)
    {
        if (_answers.TryGetValue(key, out var answer) && time.GetUtcNow() - answer.Time < Lifetime) return answer.Release;

        var release = await read().ConfigureAwait(false);
        _answers[key] = (time.GetUtcNow(), release);
        return release;
    }
}
