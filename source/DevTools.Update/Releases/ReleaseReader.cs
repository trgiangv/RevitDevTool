using System.Security.Cryptography;

namespace DevTools.Update.Releases;

/// <summary>
///     Downloads release assets. A feed (the update manifest, the Velopack feed) is returned only if its signature is correct.
/// </summary>
public sealed class ReleaseReader(HttpClient http, ReleaseSignature signature)
{
    public async Task<byte[]> ReadSignedAsync(Release release, string assetName, CancellationToken cancellationToken)
    {
        var data = await http.GetByteArrayAsync(release.Asset(assetName), cancellationToken).ConfigureAwait(false);
        if (signature.IsRequired)
        {
            var sig = await http.GetByteArrayAsync(release.Asset(assetName + ReleaseSignature.Extension), cancellationToken).ConfigureAwait(false);
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
    public async Task<string> DownloadAsync(Uri uri, string path, Action<int>? progress, CancellationToken cancellationToken)
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
    }

    private static async Task CopyAsync(Stream source, Stream target, long? total, Action<int>? progress, CancellationToken cancellationToken)
    {
        if (progress is null || total is not > 0)
        {
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            return;
        }

        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            copied += read;
            progress((int)(copied * 100 / total.Value));
        }
    }
}
