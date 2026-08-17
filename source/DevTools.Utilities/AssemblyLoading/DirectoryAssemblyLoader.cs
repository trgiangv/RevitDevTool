using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Scoped resolve helper for <em>test / probe directories only</em> (e.g. sample test output).
/// Never use this for the add-in deploy folder — that path belongs to <c>AssemblyLoader</c>
/// (<see cref="Assembly.LoadFrom"/> / ALC <c>LoadFromAssemblyPath</c>).
/// </summary>
/// <remarks>
/// Probe files are shadow-copied to a stamp-keyed temp path then loaded with
/// <see cref="Assembly.LoadFile"/>. That avoids locking the build output and still
/// yields a fresh assembly identity when the source DLL changes (byte-load of the
/// same AssemblyName cannot reload new IL on .NET Framework).
/// </remarks>
public static class DirectoryAssemblyLoad
{
    private static readonly ConcurrentDictionary<string, CacheEntry> LoadedBySourcePath =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed record CacheEntry(long Stamp, string ShadowPath, Assembly Assembly);

    /// <summary>
    /// Shadow-copies <paramref name="assemblyPath"/> when its stamp changed, then
    /// <see cref="Assembly.LoadFile"/> from the shadow path (no lock on the source file).
    /// </summary>
    public static Assembly LoadPath(string assemblyPath)
    {
        var sourcePath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Assembly not found.", sourcePath);

        var stamp = GetStamp(sourcePath);
        if (LoadedBySourcePath.TryGetValue(sourcePath, out var cached) && cached.Stamp == stamp)
            return cached.Assembly;

        var shadowPath = GetShadowPath(sourcePath, stamp);
        Directory.CreateDirectory(Path.GetDirectoryName(shadowPath)!);

        // Copy with share-friendly read of the source; overwrite shadow if present.
        File.Copy(sourcePath, shadowPath, overwrite: true);
        var pdbSource = Path.ChangeExtension(sourcePath, ".pdb");
        if (File.Exists(pdbSource))
            File.Copy(pdbSource, Path.ChangeExtension(shadowPath, ".pdb"), overwrite: true);

        var loaded = Assembly.LoadFile(shadowPath);

        if (LoadedBySourcePath.TryGetValue(sourcePath, out var previous) && previous.Stamp != stamp)
            TryDeleteShadowDirectory(previous.ShadowPath);

        LoadedBySourcePath[sourcePath] = new CacheEntry(stamp, shadowPath, loaded);
        return loaded;
    }

    /// <summary>
    /// Resolves <paramref name="assemblyName"/> for a scoped resolve handler:
    /// 1. host/shared assembly → return null (let host/default binder resolve)
    /// 2. <c>{Name}.dll</c> in <paramref name="directory"/> → <see cref="LoadPath"/>
    /// 3. already loaded assembly whose <see cref="Assembly.Location"/> is under
    ///    <paramref name="directory"/> → reuse (non-shadow loads only)
    /// </summary>
    public static Assembly? TryLoad(string directory, AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name;
        if (string.IsNullOrWhiteSpace(simpleName) || string.IsNullOrWhiteSpace(directory))
            return null;

        if (HostSharedAssemblies.IsShared(simpleName))
            return null;

        var fullDirectory = Path.GetFullPath(directory);
        var candidate = Path.Combine(fullDirectory, simpleName + ".dll");
        if (!File.Exists(candidate))
        {
            // Fall back to Location-based reuse for assemblies already loaded from this dir.
            return FindLoadedFromDirectory(fullDirectory, simpleName);
        }

        try
        {
            return LoadPath(candidate);
        }
        catch
        {
            return null;
        }
    }

    private static long GetStamp(string fullPath)
    {
        var info = new FileInfo(fullPath);
        return info.LastWriteTimeUtc.Ticks ^ info.Length;
    }

    private static string GetShadowPath(string sourcePath, long stamp)
    {
        var key = HashPathKey(sourcePath);
        var root = Path.Combine(Path.GetTempPath(), "DevTools", "AssemblyProbe", key, stamp.ToString("x"));
        return Path.Combine(root, Path.GetFileName(sourcePath));
    }

    private static string HashPathKey(string sourcePath)
    {
        var bytes = Encoding.UTF8.GetBytes(sourcePath.ToUpperInvariant());
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        var sb = new StringBuilder(16);
        for (var i = 0; i < 8; i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }

    private static void TryDeleteShadowDirectory(string shadowPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(shadowPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch
        {
            // Shadow may still be locked by LoadFile; leave for OS temp cleanup.
        }
    }

    private static Assembly? FindLoadedFromDirectory(string fullDirectory, string simpleName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string location;
            try
            {
                location = assembly.Location;
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(location))
                continue;

            var loaded = assembly.GetName();
            if (!string.Equals(loaded.Name, simpleName, StringComparison.OrdinalIgnoreCase))
                continue;

            var loadedDir = Path.GetDirectoryName(Path.GetFullPath(location));
            if (string.Equals(loadedDir, fullDirectory, StringComparison.OrdinalIgnoreCase))
                return assembly;
        }

        return null;
    }
}
