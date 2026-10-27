using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;

namespace DevTools.Update.Releases;

/// <summary>
///     Downloads release assets. A feed (the update manifest, the Velopack feed) is returned only if its signature is correct.
///     A download that fails because of the network starts again, at most <see cref="Attempts"/> times. Asset downloads are not GitHub API requests.
/// </summary>
public sealed class ReleaseReader(HttpClient http, ReleaseSignature signature, ILogger<ReleaseReader>? logger = null)
{
    public const int Attempts = 3;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public async Task<byte[]> ReadSignedAsync(Release release, string assetName, CancellationToken cancellationToken)
    {
        var data = await ReadAsync(release.Asset(assetName), cancellationToken).ConfigureAwait(false);
        if (signature.IsRequired)
        {
            var sig = await ReadAsync(release.Asset(assetName + ReleaseSignature.Extension), cancellationToken).ConfigureAwait(false);
            signature.Verify(assetName, data, sig);
        }

        return data;
    }

    public async Task<UpdateManifest> ReadManifestAsync(Release release, CancellationToken cancellationToken)
    {
        return UpdateManifest.FromJson(await ReadSignedAsync(release, UpdateManifest.FileName, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    ///     Downloads to <paramref name="path"/> and returns the lower-case hex SHA-256 of the file.
    /// </summary>
    public Task<string> DownloadAsync(Uri uri, string path, Action<int>? progress, CancellationToken cancellationToken) =>
        RetryAsync(uri, async () =>
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = File.Create(path))
            {
                await CopyAsync(source, target, response.Content.Headers.ContentLength, progress, cancellationToken).ConfigureAwait(false);
            }

            await using var written = File.OpenRead(path);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(written, cancellationToken).ConfigureAwait(false));
        }, cancellationToken);

    private Task<byte[]> ReadAsync(Uri uri, CancellationToken cancellationToken) =>
        RetryAsync(uri, () => http.GetByteArrayAsync(uri, cancellationToken), cancellationToken);

    private async Task<T> RetryAsync<T>(Uri uri, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < Attempts && !cancellationToken.IsCancellationRequested && IsTransient(ex))
            {
                var delay = RetryDelay * Math.Pow(2, attempt - 1);
                logger?.LogWarning("The download of {Uri} failed ({Error}). Attempt {Attempt} of {Attempts} starts in {Delay}",
                    uri.GetLeftPart(UriPartial.Path), ex.Message, attempt + 1, Attempts, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: { } status } => status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500,
        IOException or TaskCanceledException => true,
        _ => false
    };

    private static async Task CopyAsync(Stream source, Stream target, long? total, Action<int>? progress, CancellationToken cancellationToken)
    {
        if (progress is null || total is not > 0)
        {
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            return;
        }

        var buffer = new byte[81920];
        long copied = 0;
        var reported = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            copied += read;
            var percent = (int)(copied * 100 / total.Value);
            if (percent == reported) continue;

            reported = percent;
            progress(percent);
        }
    }
}
