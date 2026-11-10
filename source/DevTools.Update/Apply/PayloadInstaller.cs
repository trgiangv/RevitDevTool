using DevTools.Update.Autodesk;
using DevTools.Update.Releases;
using System.IO.Compression;

namespace DevTools.Update.Apply;

public enum PayloadStage
{
    Waiting,
    Downloading,
    Installing
}

public sealed record PayloadProgress(AddinTarget Target, PayloadStage Stage, int? Percent);

/// <summary>
///     An offline <c>&lt;bundle&gt;.zip</c> and the release version in its <c>PackageContents.xml</c>.
/// </summary>
public sealed record OfflineBundle(string Path, string Bundle, string Version)
{
    public static OfflineBundle Read(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(item => item.FullName.Count(c => c == '/') == 1 && item.Name == "PackageContents.xml")
                    ?? throw new FileNotFoundException($"{zipPath} has no <bundle>/PackageContents.xml.");
        using var stream = entry.Open();
        return new OfflineBundle(zipPath, entry.FullName[..entry.FullName.IndexOf('/')], PackageContents.ReadVersion(stream));
    }
}

/// <summary>
///     Stages and swaps one payload folder (0049). Staging verifies the zip and extracts it to
///     <c>&lt;bundle&gt;\.updates\&lt;version&gt;\&lt;host version&gt;</c>. The swap renames it to <c>&lt;bundle&gt;\Contents\&lt;host version&gt;</c>.
///     It keeps the previous folder as the only rollback copy.
/// </summary>
public sealed class PayloadInstaller(AutodeskBundles bundles, IFolderLock folderLock, ReleaseReader reader)
{
    public string StagedPath(string version, AddinTarget target) => Path.Combine(bundles.For(target).StagingPath(version), target.HostVersion);

    public bool IsStaged(string version, AddinTarget target) => Directory.Exists(StagedPath(version, target));

    public async Task<string> StageAsync(Release release, ManifestPayload payload, IProgress<PayloadProgress>? progress, CancellationToken cancellationToken)
    {
        var target = StagedPath(release.Version, payload.Target);
        if (Directory.Exists(target)) return target;

        var zipPath = Path.Combine(bundles.For(payload.Target).StagingPath(release.Version), payload.Asset);
        try
        {
            progress?.Report(new PayloadProgress(payload.Target, PayloadStage.Downloading, 0));
            var sha256 = await reader.DownloadAsync(release.Asset(payload.Asset), zipPath,
                percent => progress?.Report(new PayloadProgress(payload.Target, PayloadStage.Downloading, percent)), cancellationToken).ConfigureAwait(false);
            if (sha256 != payload.Sha256 || new FileInfo(zipPath).Length != payload.Size)
                throw new UpdateIntegrityException($"{payload.Asset} does not match the update manifest.");

            progress?.Report(new PayloadProgress(payload.Target, PayloadStage.Installing, null));
            await ZipFile.ExtractToDirectoryAsync(zipPath, target + ".partial", overwriteFiles: true, cancellationToken: cancellationToken);
            Directory.Move(target + ".partial", target);
            return target;
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    ///     Stages a payload from an offline bundle zip of the target's bundle.
    /// </summary>
    public string StageFromBundle(OfflineBundle offline, AddinTarget target)
    {
        var staged = StagedPath(offline.Version, target);
        var prefix = $"{offline.Bundle}/Contents/{target.HostVersion}/";
        var root = Path.GetFullPath(staged) + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(offline.Path);
        var entries = archive.Entries.Where(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal) && entry.Name.Length > 0).ToArray();
        if (entries.Length == 0) throw new FileNotFoundException($"{offline.Path} has no {prefix}.");

        foreach (var entry in entries)
        {
            var destination = Path.GetFullPath(Path.Combine(root, entry.FullName[prefix.Length..]));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UpdateIntegrityException($"The zip entry {entry.FullName} is outside the payload folder.");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        return staged;
    }

    /// <summary>
    ///     Swaps the staged folder in. If the folder is busy, it returns the lockers and changes nothing.
    /// </summary>
    public IReadOnlyList<LockingProcess> TryApply(AddinTarget target, string stagedPath)
    {
        var bundle = bundles.For(target);
        var current = bundle.PayloadPath(target.HostVersion);
        var lockers = folderLock.GetLockers(current);
        if (lockers.Count > 0) return lockers;

        var rollback = bundle.RollbackPath(target.HostVersion);
        DeleteIfExists(rollback);
        Directory.CreateDirectory(bundle.Rollback);
        Directory.CreateDirectory(bundle.Contents);

        if (Directory.Exists(current))
        {
            try
            {
                Directory.Move(current, rollback);
            }
            catch (IOException) when (folderLock.GetLockers(current) is { Count: > 0 } lateLockers)
            {
                return lateLockers;
            }
        }

        try
        {
            Directory.Move(stagedPath, current);
        }
        catch
        {
            if (Directory.Exists(rollback)) Directory.Move(rollback, current);
            throw;
        }

        return [];
    }

    /// <summary>
    ///     Swaps the rollback copy back in. Returns false if there is no copy or the folder is busy.
    /// </summary>
    public bool TryRollback(AddinTarget target)
    {
        var bundle = bundles.For(target);
        var rollback = bundle.RollbackPath(target.HostVersion);
        var current = bundle.PayloadPath(target.HostVersion);
        if (!Directory.Exists(rollback) || folderLock.GetLockers(current).Count > 0) return false;

        var discarded = rollback + ".discarded";
        if (Directory.Exists(current)) Directory.Move(current, discarded);
        Directory.Move(rollback, current);
        DeleteIfExists(discarded);
        return true;
    }

    public bool TryRemove(AddinTarget target)
    {
        var bundle = bundles.For(target);
        var current = bundle.PayloadPath(target.HostVersion);
        if (folderLock.GetLockers(current).Count > 0) return false;

        DeleteIfExists(current);
        DeleteIfExists(bundle.RollbackPath(target.HostVersion));
        return true;
    }

    /// <summary>
    ///     Deletes the staging folders of all versions that are not in <paramref name="keep"/>, in every bundle.
    /// </summary>
    public void CleanStaging(IEnumerable<string> keep)
    {
        var kept = keep.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var bundle in bundles.All.Where(bundle => Directory.Exists(bundle.Updates)))
        {
            foreach (var folder in Directory.GetDirectories(bundle.Updates))
            {
                var name = Path.GetFileName(folder);
                if (!kept.Contains(name) && !string.Equals(name, Path.GetFileName(bundle.Rollback), StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static void DeleteIfExists(string folder)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
