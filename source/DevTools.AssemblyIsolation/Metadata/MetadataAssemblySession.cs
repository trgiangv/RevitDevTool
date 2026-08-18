using System.Reflection;

namespace DevTools.AssemblyIsolation.Metadata;

public sealed class MetadataAssemblySession : IDisposable
{
    readonly string entryAssemblyPath;
    MetadataLoadContext? context;

    MetadataAssemblySession(string entryAssemblyPath, IReadOnlyList<string> resolutionPaths)
    {
        this.entryAssemblyPath = entryAssemblyPath;
        context = new MetadataLoadContext(new PathAssemblyResolver(resolutionPaths));
    }

    public static MetadataAssemblySession Create(string entryPath, IEnumerable<string> resolutionPaths)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
            throw new ArgumentException("An entry assembly path is required.", nameof(entryPath));
        if (resolutionPaths is null)
            throw new ArgumentNullException(nameof(resolutionPaths));

        var entryAssemblyPath = Path.GetFullPath(entryPath);
        var paths = CollectResolutionPaths(entryAssemblyPath, resolutionPaths);
        return new MetadataAssemblySession(entryAssemblyPath, paths);
    }

    public Assembly LoadEntryAssembly()
    {
        var metadataContext = context ?? throw new ObjectDisposedException(nameof(MetadataAssemblySession));
        return metadataContext.LoadFromAssemblyPath(entryAssemblyPath);
    }

    public void Dispose()
    {
        context?.Dispose();
        context = null;
    }

    static IReadOnlyList<string> CollectResolutionPaths(string entryPath, IEnumerable<string> resolutionPaths)
    {
        var pathsByIdentity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[] { entryPath }.Concat(resolutionPaths))
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Metadata resolution paths cannot be empty.", nameof(resolutionPaths));

            var normalizedPath = Path.GetFullPath(path);
            AssemblyName assemblyName;
            try
            {
                assemblyName = AssemblyName.GetAssemblyName(normalizedPath);
            }
            catch (BadImageFormatException) when (!string.Equals(normalizedPath, entryPath, StringComparison.OrdinalIgnoreCase))
            {
                // Resolution folders may also contain native DLLs. They cannot participate in metadata resolution.
                continue;
            }

            var identity = assemblyName.FullName
                ?? throw new InvalidOperationException($"Metadata assembly '{normalizedPath}' has no full identity.");
            if (pathsByIdentity.TryGetValue(identity, out var existingPath))
            {
                if (!string.Equals(existingPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Duplicate metadata assembly identity '{identity}' at '{existingPath}' and '{normalizedPath}'.");

                continue;
            }

            pathsByIdentity.Add(identity, normalizedPath);
        }

        return pathsByIdentity.Values.ToArray();
    }
}
