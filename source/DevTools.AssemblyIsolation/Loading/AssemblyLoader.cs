using System.Reflection;
using System.Security.Cryptography;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;
#if NET
using System.Runtime.InteropServices;
using System.Runtime.Loader;
#endif

namespace DevTools.AssemblyIsolation.Loading;

/// <summary>
/// Path-loads assemblies once for the process lifetime. Optional
/// <see cref="Register"/> probes one directory when the default context asks
/// for a missing sibling (managed or native).
/// </summary>
public sealed class AssemblyLoader(IAssemblyIsolationDiagnosticSink? diagnostics = null) : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, LoadedPath> assembliesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SelectedAssembly> assembliesByIdentity = new(StringComparer.OrdinalIgnoreCase);
    private DirectoryAssemblySource? managedSource;
    private bool registered;
    private bool disposed;

#if NET
    private AssemblyLoadContext LoadContext { get; } = new("DevTools.AssemblyLoader", isCollectible: false);
#endif

    public Assembly LoadPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("An assembly path is required.", nameof(assemblyPath));

        var normalizedPath = Path.GetFullPath(assemblyPath);
        var requestedIdentity = AssemblyName.GetAssemblyName(normalizedPath);
        var identity = requestedIdentity.FullName
            ?? throw new InvalidOperationException($"Assembly '{normalizedPath}' has no full identity.");
        var fingerprint = ComputeFingerprint(normalizedPath);

        lock (gate)
        {
            if (assembliesByPath.TryGetValue(normalizedPath, out var pathMatch))
            {
                if (!string.Equals(pathMatch.Identity, identity, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(pathMatch.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    Publish("path-changed", requestedIdentity,
                        $"Path '{normalizedPath}' changed after '{pathMatch.Identity}' was loaded. Returning the initial assembly; this loader does not support hot reload.");
                }

                return pathMatch.Assembly;
            }

            if (assembliesByIdentity.TryGetValue(identity, out var identityMatch))
            {
                assembliesByPath.Add(normalizedPath, new LoadedPath(identity, fingerprint, identityMatch.Assembly));
                Publish("identity-already-loaded", requestedIdentity,
                    $"Identity '{identity}' was already loaded from '{identityMatch.Path}'. Returning the initial assembly instead of loading '{normalizedPath}'.");
                return identityMatch.Assembly;
            }

#if NETFRAMEWORK
            var assembly = Assembly.LoadFrom(normalizedPath);
#else
            var assembly = LoadContext.LoadFromAssemblyPath(normalizedPath);
#endif
            assembliesByPath.Add(normalizedPath, new LoadedPath(identity, fingerprint, assembly));
            assembliesByIdentity.Add(identity, new SelectedAssembly(normalizedPath, assembly));
            return assembly;
        }
    }

    public void Register(string probeDirectory)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(probeDirectory))
            throw new ArgumentException("A directory is required.", nameof(probeDirectory));
        if (registered)
            return;

        managedSource = new DirectoryAssemblySource(probeDirectory);
#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += ResolveManaged;
#else
        AssemblyLoadContext.Default.Resolving += ResolveManaged;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveUnmanaged;
        LoadContext.Resolving += ResolveManaged;
        LoadContext.ResolvingUnmanagedDll += ResolveUnmanaged;
#endif
        registered = true;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        if (registered)
        {
#if NETFRAMEWORK
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveManaged;
#else
            AssemblyLoadContext.Default.Resolving -= ResolveManaged;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll -= ResolveUnmanaged;
            LoadContext.Resolving -= ResolveManaged;
            LoadContext.ResolvingUnmanagedDll -= ResolveUnmanaged;
#endif
            registered = false;
        }

        disposed = true;
    }

#if NETFRAMEWORK
    private Assembly? ResolveManaged(object? sender, ResolveEventArgs args) => ResolveManaged(new AssemblyName(args.Name));
#else
    private Assembly? ResolveManaged(AssemblyLoadContext context, AssemblyName requested) => ResolveManaged(requested);
#endif

    private Assembly? ResolveManaged(AssemblyName requested)
    {
        if (disposed || managedSource is null)
            return null;

        var candidate = managedSource.Resolve(requested);
        if (candidate is null)
            return null;
        if (!AssemblyCandidate.IsExistingPathUnderRoot(candidate.Path, candidate.Root))
            return null;
        return LoadPath(candidate.Path);
    }

#if NET
    private IntPtr ResolveUnmanaged(Assembly assembly, string unmanagedDllName) => ResolveUnmanaged(unmanagedDllName);

    internal string? FindUnmanagedPathForTesting(string name) => FindUnmanagedPath(name);

    private IntPtr ResolveUnmanaged(string name)
    {
        var candidate = FindUnmanagedPath(name);
        return candidate is null ? IntPtr.Zero : NativeLibrary.Load(candidate);
    }

    private string? FindUnmanagedPath(string name)
    {
        if (disposed || managedSource is null || string.IsNullOrWhiteSpace(name))
            return null;
        var fileName = Path.GetFileName(name);
        if (!string.Equals(fileName, name, StringComparison.Ordinal))
            return null;

        var directory = managedSource.Root;
        var candidate = Path.Combine(directory, AssemblyCandidate.WithExtension(fileName));
        return AssemblyCandidate.IsExistingPathUnderRoot(candidate, directory)
            ? candidate
            : null;
    }
#endif

    private void Publish(string code, AssemblyName requestedIdentity, string message) =>
        diagnostics?.Publish(new AssemblyIsolationDiagnostic(code, message, requestedIdentity));

    private static string ComputeFingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(stream));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private sealed record LoadedPath(string Identity, string Fingerprint, Assembly Assembly);

    private sealed record SelectedAssembly(string Path, Assembly Assembly);
}
