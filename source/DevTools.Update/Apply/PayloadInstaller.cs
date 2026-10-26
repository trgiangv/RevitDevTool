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

public sealed record PayloadProgress(string HostVersion, PayloadStage Stage, int? Percent);

/// <summary>
///     Stages and swaps one payload folder (0049). Staging verifies the zip and extracts it to
///     <c>.updates\&lt;version&gt;\&lt;host version&gt;</c>. The swap renames it to <c>Contents\&lt;host version&gt;</c>.
///     It keeps the previous folder as the only rollback copy.
/// </summary>
public sealed class PayloadInstaller(AutodeskBundle bundle, IFolderLock folderLock, ReleaseReader reader)
{
    public string StagedPath(string version, string hostVersion) => Path.Combine(bundle.StagingPath(version), hostVersion);

    public bool IsStaged(string version, string hostVersion) => Directory.Exists(StagedPath(version, hostVersion));

    public async Task<string> StageAsync(Release release, ManifestPayload payload, IProgress<PayloadProgress>? progress, CancellationToken cancellationToken)
    {
        var target = StagedPath(release.Version, payload.HostVersion);
        if (Directory.Exists(target)) return target;

        var zipPath = Path.Combine(bundle.StagingPath(release.Version), payload.Asset);
        try
        {
            progress?.Report(new PayloadProgress(payload.HostVersion, PayloadStage.Downloading, 0));
            var sha256 = await reader.DownloadAsync(release.Asset(payload.Asset), zipPath,
                percent => progress?.Report(new PayloadProgress(payload.HostVersion, PayloadStage.Downloading, percent)), cancellationToken).ConfigureAwait(false);
            if (sha256 != payload.Sha256 || new FileInfo(zipPath).Length != payload.Size)
                throw new UpdateIntegrityException($"{payload.Asset} does not match the update manifest.");

            progress?.Report(new PayloadProgress(payload.HostVersion, PayloadStage.Installing, null));
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
    ///     Stages a payload from the offline <c>RevitDevTool.bundle.zip</c>.
    /// </summary>
    public string StageFromBundle(string bundleZip, string hostVersion, string version)
    {
        var target = StagedPath(version, hostVersion);
        var prefix = $"{AutodeskBundle.BundleName}/Contents/{hostVersion}/";
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(bundleZip);
        var entries = archive.Entries.Where(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal) && entry.Name.Length > 0).ToArray();
        if (entries.Length == 0) throw new FileNotFoundException($"{bundleZip} has no Contents/{hostVersion}.");

        foreach (var entry in entries)
        {
            var destination = Path.GetFullPath(Path.Combine(root, entry.FullName[prefix.Length..]));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UpdateIntegrityException($"The zip entry {entry.FullName} is outside the payload folder.");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        return target;
    }

    /// <summary>
    ///     Gets the release version from <c>AppVersion</c> of the <c>PackageContents.xml</c> in the offline bundle.
    /// </summary>
    public static string ReadBundleVersion(string bundleZip)
    {
        using var archive = ZipFile.OpenRead(bundleZip);
        var entry = archive.GetEntry($"{AutodeskBundle.BundleName}/PackageContents.xml")
                    ?? throw new FileNotFoundException($"{bundleZip} has no PackageContents.xml.");
        using var stream = entry.Open();
        return PackageContents.ReadVersion(stream);
    }

    /// <summary>
    ///     Swaps the staged folder in. If the folder is busy, it returns the lockers and changes nothing.
    /// </summary>
    public IReadOnlyList<LockingProcess> TryApply(string hostVersion, string stagedPath)
    {
        var current = bundle.PayloadPath(hostVersion);
        var lockers = folderLock.GetLockers(current);
        if (lockers.Count > 0) return lockers;

        var rollback = bundle.RollbackPath(hostVersion);
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
    public bool TryRollback(string hostVersion)
    {
        var rollback = bundle.RollbackPath(hostVersion);
        var current = bundle.PayloadPath(hostVersion);
        if (!Directory.Exists(rollback) || folderLock.GetLockers(current).Count > 0) return false;

        var discarded = rollback + ".discarded";
        if (Directory.Exists(current)) Directory.Move(current, discarded);
        Directory.Move(rollback, current);
        DeleteIfExists(discarded);
        return true;
    }

    public bool TryRemove(string hostVersion)
    {
        var current = bundle.PayloadPath(hostVersion);
        if (folderLock.GetLockers(current).Count > 0) return false;

        DeleteIfExists(current);
        DeleteIfExists(bundle.RollbackPath(hostVersion));
        return true;
    }

    /// <summary>
    ///     Deletes the staging folders of all versions that are not in <paramref name="keep"/>.
    /// </summary>
    public void CleanStaging(IEnumerable<string> keep)
    {
        if (!Directory.Exists(bundle.Updates)) return;

        var kept = keep.Append(Path.GetFileName(bundle.Rollback)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in Directory.GetDirectories(bundle.Updates).Where(folder => !kept.Contains(Path.GetFileName(folder))))
            Directory.Delete(folder, recursive: true);
    }

    private static void DeleteIfExists(string folder)
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
