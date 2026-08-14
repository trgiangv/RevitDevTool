#if NET
using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.Loader;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Runtime;
using DevTools.Utilities.AssemblyLoading;

namespace DevTools.NUnit.Host.Loading;

internal sealed class NUnitRuntimeLoadContext : AssemblyLoadContext
{
    private readonly NUnitGenerationManifest _manifest;
    private readonly string _shadowDirectory;
    private readonly AssemblyDependencyResolver _resolver;
    private readonly NUnitGenerationManagedAssemblyIndex _managedAssemblyIndex;
    private readonly HashSet<string> _manifestNativeAssetPaths;
    private readonly FrozenDictionary<string, IReadOnlyList<string>> _nativeAssetsByFileName;

    public NUnitRuntimeLoadContext(NUnitGenerationManifest manifest)
        : base($"NUnitGeneration_{manifest.GenerationId}", isCollectible: true)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        _manifest = manifest;
        _shadowDirectory = Path.GetFullPath(manifest.ShadowDirectory);
        _resolver = new AssemblyDependencyResolver(Path.GetFullPath(manifest.ShadowAssemblyPath));
        _managedAssemblyIndex = NUnitGenerationManagedAssemblyIndex.Create(manifest.ManagedAssemblies);
        _manifestNativeAssetPaths = manifest.NativeAssets
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _nativeAssetsByFileName = BuildNativeAssetsByFileName(manifest.NativeAssets);
    }

    public Assembly LoadFromManifestPath(string absolutePath)
    {
        var normalizedPath = Path.GetFullPath(absolutePath);
        if (!IsUnderShadowDirectory(normalizedPath))
        {
            throw new InvalidOperationException(
                $"Refusing to load assembly outside generation shadow directory: {normalizedPath}");
        }

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException(
                $"Generation assembly not found: {normalizedPath}",
                normalizedPath);
        }

        // Stream-load so shadow copies are not locked for the next generation rebuild.
        return ByteAssemblyLoader.LoadFromStream(this, normalizedPath);
    }

    internal Assembly? ResolveAssemblyForTesting(AssemblyName assemblyName) => ResolveAssembly(assemblyName);

    internal string? ResolveNativeAssetForTesting(string unmanagedDllName) =>
        NUnitGenerationNativeAssetResolver.Resolve(
            unmanagedDllName,
            _resolver,
            _manifestNativeAssetPaths,
            _shadowDirectory,
            _nativeAssetsByFileName);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            return ResolveAssembly(assemblyName);
        }
        catch (NUnitGenerationAssemblyResolutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Failed to resolve assembly '{assemblyName.FullName}' for generation '{_manifest.GenerationId}'.",
                ex);
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var nativePath = NUnitGenerationNativeAssetResolver.Resolve(
            unmanagedDllName,
            _resolver,
            _manifestNativeAssetPaths,
            _shadowDirectory,
            _nativeAssetsByFileName);

        return nativePath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(nativePath);
    }

    private Assembly? ResolveAssembly(AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name
            ?? throw new NUnitGenerationAssemblyResolutionException("Requested assembly name is missing.");

        if (NUnitSharedAssemblyPolicy.IsShared(simpleName))
            return NUnitSharedAssemblyResolver.TryResolveFromDefault(assemblyName);

        // Keep nunit.framework out of the collectible ALC: AsyncLocal/statics on a
        // generation-private copy pin unload inside Autodesk hosts. Still load OUR
        // shadow version (not Dynamo's) into the non-collectible Plugin/Default ALC.
        if (NUnitFrameworkHostShare.IsFrameworkSimpleName(simpleName))
            return ResolveHostSharedFramework(assemblyName);

        var resolverPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolverPath is not null && TryLoadShadowAssembly(resolverPath, assemblyName, out var resolvedAssembly))
            return resolvedAssembly;

        var manifestPath = _managedAssemblyIndex.ResolvePath(assemblyName);
        if (manifestPath is not null && TryLoadShadowAssembly(manifestPath, assemblyName, out var manifestAssembly))
            return manifestAssembly;

        // The policy intentionally covers known shared cases only. Returning
        // null preserves normal CLR resolution for dependencies outside both
        // the host-shared policy and this immutable generation.
        return null;
    }

    private Assembly ResolveHostSharedFramework(AssemblyName requested)
    {
        var shared = NUnitFrameworkHostShare.GetOrLoadFromShadow(_manifest.FrameworkAssemblyPath);
        if (!NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(requested, shared.GetName()))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Host-shared nunit.framework '{shared.FullName}' is incompatible with requested '{requested.FullName}'.");
        }

        return shared;
    }

    private bool TryLoadShadowAssembly(string absolutePath, AssemblyName requested, out Assembly assembly)
    {
        assembly = null!;

        var normalizedPath = Path.GetFullPath(absolutePath);
        if (!IsUnderShadowDirectory(normalizedPath) || !File.Exists(normalizedPath))
            return false;

        var identity = AssemblyName.GetAssemblyName(normalizedPath);
        if (NUnitSharedAssemblyPolicy.IsShared(identity.Name!))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Generation shadow path '{normalizedPath}' contains allowlisted shared assembly '{identity.Name}'.");
        }

        if (NUnitFrameworkHostShare.IsFrameworkSimpleName(identity.Name))
        {
            // Never stream-load nunit into the collectible generation ALC.
            assembly = ResolveHostSharedFramework(requested);
            return true;
        }

        if (!NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(requested, identity))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Generation shadow path '{normalizedPath}' identity '{identity.FullName}' is incompatible with requested '{requested.FullName}'.");
        }

        assembly = ByteAssemblyLoader.LoadFromStream(this, normalizedPath);
        return true;
    }

    private bool IsUnderShadowDirectory(string absolutePath)
    {
        var normalizedPath = Path.GetFullPath(absolutePath);
        if (string.Equals(normalizedPath, _shadowDirectory, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = _shadowDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _shadowDirectory
            : _shadowDirectory + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<string, IReadOnlyList<string>> BuildNativeAssetsByFileName(
        IReadOnlyList<string> nativeAssets)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var absolutePath in nativeAssets)
        {
            var normalizedPath = Path.GetFullPath(absolutePath);
            AddNativeLookup(groups, Path.GetFileName(normalizedPath), normalizedPath);

            var withoutExtension = Path.GetFileNameWithoutExtension(normalizedPath);
            if (!string.IsNullOrWhiteSpace(withoutExtension))
                AddNativeLookup(groups, withoutExtension, normalizedPath);
        }

        return groups.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<string>)pair.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddNativeLookup(
        Dictionary<string, List<string>> groups,
        string lookupKey,
        string normalizedPath)
    {
        if (!groups.TryGetValue(lookupKey, out var paths))
        {
            paths = [];
            groups[lookupKey] = paths;
        }

        if (!paths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
            paths.Add(normalizedPath);
    }
}

internal sealed class NUnitRuntimeSessionHandle : INUnitRuntimeSession
{
    private INUnitRuntimeSession _inner;
    private NUnitRuntimeLoadContext? _loadContext;
    private readonly WeakReference _loadContextWeakReference;
    private NUnitRuntimeDiagnostic? _unloadDiagnostic;
    private bool _disposed;

    internal NUnitRuntimeSessionHandle(INUnitRuntimeSession inner, NUnitRuntimeLoadContext loadContext)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
        _loadContextWeakReference = new WeakReference(loadContext, trackResurrection: true);
    }

    public string GenerationId => _inner.GenerationId;

    internal Assembly GetLoadedTestAssembly()
    {
        var field = _inner.GetType().GetField("_testAssembly", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Runtime session test assembly field not found.");

        return (Assembly)field.GetValue(_inner)!;
    }

    internal static Assembly GetLoadedFrameworkAssembly()
    {
        if (!NUnitFrameworkHostShare.TryGetLoaded(out var shared))
        {
            throw new InvalidOperationException(
                "Host-shared nunit.framework has not been loaded for this generation.");
        }

        return shared;
    }

    internal Assembly GetLoadedRuntimeAssembly() =>
        _inner.GetType().Assembly;

    internal NUnitRuntimeLoadContext GetLoadContext() =>
        _loadContext ?? throw new ObjectDisposedException(nameof(NUnitRuntimeSessionHandle));

    public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request) => _inner.Discover(request);

    public NUnitRunResponse Run(
        NUnitRunRequest request,
        INUnitRuntimeEventSink eventSink,
        CancellationToken cancellationToken) =>
        _inner.Run(request, eventSink, cancellationToken);

    public void Cancel(Guid runId) => _inner.Cancel(runId);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _inner.Dispose();
        }
        finally
        {
            _inner = null!;
            _loadContext?.Unload();
            _loadContext = null;
        }
    }

    public NUnitRuntimeDiagnostic VerifyUnload()
    {
        ObjectDisposedException.ThrowIf(!_disposed, this);

        return _unloadDiagnostic ??= NUnitRuntimeUnloadVerifier.Verify(_loadContextWeakReference);
    }
}
#endif
