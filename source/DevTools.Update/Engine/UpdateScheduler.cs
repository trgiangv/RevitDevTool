using DevTools.Update.Installations;
using DevTools.Update.Releases;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevTools.Update.Engine;

/// <summary>
///     Runs the update cycle one time after <see cref="UpdateEngineOptions.StartupDelay"/>, then at each interval.
///     The default is 60 min and the minimum is 15 min, with ±20 % jitter. After a failure, the delay doubles to a maximum of 24 h (0048).
/// </summary>
public sealed class UpdateScheduler(UpdateEngine engine, IOptions<UpdateEngineOptions> options, ILogger<UpdateScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim _wake = new(0, 1);
    private UpdateTrigger _nextTrigger = UpdateTrigger.Scheduled;

    public void Trigger(UpdateTrigger trigger = UpdateTrigger.Manual)
    {
        _nextTrigger = trigger;
        if (_wake.CurrentCount == 0) _wake.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;
        var delay = options.Value.StartupDelay;
        while (await WaitAsync(delay, stoppingToken).ConfigureAwait(false))
        {
            var trigger = _nextTrigger;
            _nextTrigger = UpdateTrigger.Scheduled;
            var interval = engine.Store.Load()?.Policy.Interval ?? new UpdatePolicy().Interval;
            try
            {
                var result = await engine.CheckAsync(trigger, stoppingToken).ConfigureAwait(false);
                failures = 0;
                delay = Jitter(interval, Random.Shared.NextDouble());
                logger.LogInformation("The {Trigger} update check ended with {Status} ({Version}). The next check is in {Delay}",
                    trigger, result.Status, result.Version, delay);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                delay = Backoff(interval, ++failures, (ex as UpdateThrottledException)?.RetryAfter);
                logger.LogWarning(ex, "The update check failed. The next check is in {Delay}", delay);
            }
        }
    }

    internal static TimeSpan Jitter(TimeSpan interval, double sample) => interval * (0.8 + 0.4 * sample);

    internal static TimeSpan Backoff(TimeSpan interval, int failures, TimeSpan? retryAfter = null)
    {
        var next = interval * Math.Pow(2, Math.Min(failures, 10));
        if (retryAfter > next) next = retryAfter.Value;
        return next > MaxBackoff ? MaxBackoff : next;
    }

    /// <summary>
    ///     Waits for the delay or a trigger. Returns false when the host stops.
    /// </summary>
    private async Task<bool> WaitAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await _wake.WaitAsync(delay, stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public override void Dispose()
    {
        _wake.Dispose();
        base.Dispose();
    }
}
