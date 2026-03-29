using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace RevitDevTool.Execution.Services;

/// <summary>
/// Centralized network operations with retry, exponential backoff, and transient error handling.
/// All HTTP and network-dependent CLI operations should go through this service.
/// </summary>
public static class NetworkService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "RevitDevTool" } }
    };

    private const int DefaultMaxRetries = 3;
    private const int DefaultBaseDelayMs = 2000;

    /// <summary>
    /// Downloads a string from the given URL with retry.
    /// </summary>
    public static async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads raw bytes from the given URL with retry.
    /// </summary>
    public static async Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches JSON from the given URL and returns the raw <see cref="JsonDocument"/> with retry.
    /// Returns <c>null</c> on non-success status codes.
    /// Caller is responsible for disposing the returned document.
    /// </summary>
    public static async Task<JsonDocument?> GetJsonDocumentAsync(string url, CancellationToken cancellationToken = default)
    {
        return await WithRetryAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonDocument.Parse(payload);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an async operation with retry and exponential backoff for transient errors.
    /// </summary>
    public static async Task<T> WithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = DefaultMaxRetries,
        int baseDelayMs = DefaultBaseDelayMs)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries - 1 && IsTransient(ex))
            {
                var delay = baseDelayMs * (1 << attempt);
                Trace.TraceWarning(
                    $"[Network] Transient error (attempt {attempt + 1}/{maxRetries}): {ex.Message}. " +
                    $"Retrying in {delay}ms...");
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Executes an async action (no return value) with retry and exponential backoff.
    /// </summary>
    public static async Task WithRetryAsync(
        Func<Task> operation,
        int maxRetries = DefaultMaxRetries,
        int baseDelayMs = DefaultBaseDelayMs)
    {
        await WithRetryAsync(async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }, maxRetries, baseDelayMs).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether an exception represents a transient network error worth retrying.
    /// </summary>
    private static bool IsTransient(Exception ex)
    {
        return ex is HttpRequestException
            or TaskCanceledException
            or IOException;
    }
}
