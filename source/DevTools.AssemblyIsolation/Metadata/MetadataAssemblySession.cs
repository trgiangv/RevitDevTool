using DevTools.AssemblyIsolation;
using System.Reflection;

namespace DevTools.AssemblyIsolation.Metadata;

public sealed class MetadataAssemblySession : IDisposable
{
    private readonly string _entryAssemblyPath;
    private MetadataLoadContext? _context;

    private MetadataAssemblySession(string entryAssemblyPath, IReadOnlyList<string> resolutionPaths)
    {
        _entryAssemblyPath = entryAssemblyPath;
        _context = new MetadataLoadContext(new PathAssemblyResolver(resolutionPaths));
    }

    public static MetadataAssemblySession Create(string entryPath, IEnumerable<string> resolutionPaths)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
            throw new ArgumentException("An entry assembly path is required.", nameof(entryPath));
        ArgumentNullException.ThrowIfNull(resolutionPaths);

        var entryAssemblyPath = Path.GetFullPath(entryPath);
        var paths = CollectResolutionPaths(entryAssemblyPath, resolutionPaths);
        return new MetadataAssemblySession(entryAssemblyPath, paths);
    }

    public Assembly LoadEntryAssembly()
    {
        var loadContext = _context ?? throw new ObjectDisposedException(nameof(MetadataAssemblySession));
        return loadContext.LoadFromAssemblyPath(_entryAssemblyPath);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }

    private static IReadOnlyList<string> CollectResolutionPaths(string entryPath, IEnumerable<string> resolutionPaths)
    {
        var pathsByIdentity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[] { entryPath }.Concat(resolutionPaths))
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Metadata resolution paths cannot be empty.", nameof(resolutionPaths));

            var normalizedPath = Path.GetFullPath(path);
            if (!string.Equals(normalizedPath, entryPath, StringComparison.OrdinalIgnoreCase)
                && !ManagedAssembly.IsManaged(normalizedPath))
            {
                continue;
            }

            var assemblyName = AssemblyName.GetAssemblyName(normalizedPath);

            var identity = assemblyName.FullName
                ?? throw new InvalidOperationException($"Metadata assembly '{normalizedPath}' has no full identity.");
            if (pathsByIdentity.ContainsKey(identity))
                continue;

            pathsByIdentity.Add(identity, normalizedPath);
        }

        return pathsByIdentity.Values.ToArray();
    }
}
