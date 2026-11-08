namespace DevTools.Update.Releases;

/// <summary>
///     Keeps the channel index for <see cref="Lifetime"/>. One check from the Updates tab reads the index a few times,
///     and with the cache it sends the request one time only. A failed request is not kept.
/// </summary>
public sealed class CachedChannelIndexSource(IChannelIndexSource inner, TimeProvider time) : IChannelIndexSource
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private (DateTimeOffset Time, ChannelIndex Index)? _answer;

    public async Task<ChannelIndex> GetAsync(CancellationToken cancellationToken)
    {
        if (_answer is { } answer && time.GetUtcNow() - answer.Time < Lifetime) return answer.Index;

        var index = await inner.GetAsync(cancellationToken).ConfigureAwait(false);
        _answer = (time.GetUtcNow(), index);
        return index;
    }
}
