#if NETFRAMEWORK
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DevTools.NUnit.Host.Loading;

internal sealed class NetfxNUnitGeneration
{
    private readonly NUnitGenerationManifest _manifest;
    private readonly string _shadowDirectory;
    private readonly NUnitGenerationManagedAssemblyIndex _assemblyIndex;
    private readonly Dictionary<string, Assembly> _loadedByPath;
    private readonly HashSet<Assembly> _ownedAssemblies;
    private readonly object _loadLock = new();

    private bool _loaded;
    private Assembly? _runtimeAssembly;
    private Assembly? _testAssembly;

    internal NetfxNUnitGeneration(NUnitGenerationManifest manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _shadowDirectory = Path.GetFullPath(manifest.ShadowDirectory);
        _assemblyIndex = NUnitGenerationManagedAssemblyIndex.Create(manifest.ManagedAssemblies);
        _loadedByPath = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        _ownedAssemblies = new HashSet<Assembly>(AssemblyReferenceEqualityComparer.Instance);
    }

    internal string GenerationId => _manifest.GenerationId;

    internal NUnitGenerationManifest Manifest => _manifest;

    internal Assembly RuntimeAssembly =>
        _runtimeAssembly ?? throw new InvalidOperationException("Generation runtime assembly is not loaded.");

    internal Assembly TestAssembly =>
        _testAssembly ?? throw new InvalidOperationException("Generation test assembly is not loaded.");

    internal IReadOnlyCollection<Assembly> OwnedAssemblies => _ownedAssemblies;

    internal int LazyResolutionCount { get; private set; }

    internal void EnsureLoaded(NUnitGenerationRegistry registry)
    {
        lock (_loadLock)
        {
            if (_loaded)
                return;

            registry.SetActiveLoadingGeneration(this);
            try
            {
                foreach (var absolutePath in GetBootstrapLoadPaths(_manifest))
                    LoadGenerationAssemblyFromFile(registry, absolutePath);

                _runtimeAssembly = GetRequiredLoadedAssembly(_manifest.RuntimeAssemblyPath);
                _testAssembly = GetRequiredLoadedAssembly(_manifest.ShadowAssemblyPath);
                _loaded = true;
            }
            finally
            {
                registry.ClearActiveLoadingGeneration(this);
            }
        }
    }

    internal bool OwnsAssembly(Assembly assembly) =>
        _ownedAssemblies.Contains(assembly);

    internal bool OwnsAssemblyNamed(string simpleName) =>
        _ownedAssemblies.Any(assembly =>
            string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));

    internal Assembly? ResolveDependency(
        NUnitGenerationRegistry registry,
        AssemblyName requested,
        Assembly? requestingAssembly)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));
        if (requested is null)
            throw new ArgumentNullException(nameof(requested));

        var simpleName = requested.Name;
        if (string.IsNullOrWhiteSpace(simpleName))
            return null;

        if (NUnitSharedAssemblyPolicy.IsShared(simpleName))
            return NetfxNUnitSharedAssemblyResolver.TryResolveFromAppDomain(requested);

        if (requestingAssembly is not null && !OwnsAssembly(requestingAssembly))
            return null;

        var manifestPath = _assemblyIndex.ResolvePath(requested);
        if (manifestPath is null)
            return null;

        return LoadGenerationAssemblyFromFile(registry, manifestPath, requested);
    }

    internal void RecordLazyResolution() => LazyResolutionCount++;

    internal Assembly GetLoadedFrameworkAssembly()
    {
        var frameworkPath = Path.GetFullPath(_manifest.FrameworkAssemblyPath);
        return GetRequiredLoadedAssembly(frameworkPath);
    }

    private Assembly LoadGenerationAssemblyFromFile(
        NUnitGenerationRegistry? registry,
        string absolutePath,
        AssemblyName? requested = null)
    {
        var normalizedPath = Path.GetFullPath(absolutePath);
        if (!IsUnderShadowDirectory(normalizedPath))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Refusing to load assembly outside generation shadow directory: {normalizedPath}");
        }

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException(
                $"Generation assembly not found: {normalizedPath}",
                normalizedPath);
        }

        if (_loadedByPath.TryGetValue(normalizedPath, out var existing))
            return existing;

        var identity = AssemblyName.GetAssemblyName(normalizedPath);
        if (NUnitSharedAssemblyPolicy.IsShared(identity.Name!))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Generation shadow path '{normalizedPath}' contains allowlisted shared assembly '{identity.Name}'.");
        }

        if (requested is not null
            && !NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(requested, identity))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Generation shadow path '{normalizedPath}' identity '{identity.FullName}' is incompatible with requested '{requested.FullName}'.");
        }

        var enteredActiveLoading = false;
        if (registry is not null)
        {
            registry.SetActiveLoadingGeneration(this);
            enteredActiveLoading = true;
        }

        try
        {
            var assembly = Assembly.LoadFile(normalizedPath);
            RegisterOwnedAssembly(assembly);
            _loadedByPath[normalizedPath] = assembly;

            if (registry is not null)
                registry.RegisterOwnedAssembly(this, assembly);

            return assembly;
        }
        finally
        {
            if (enteredActiveLoading)
                registry!.ClearActiveLoadingGeneration(this);
        }
    }

    private Assembly GetRequiredLoadedAssembly(string absolutePath)
    {
        var normalizedPath = Path.GetFullPath(absolutePath);
        if (_loadedByPath.TryGetValue(normalizedPath, out var assembly))
            return assembly;

        throw new InvalidOperationException($"Generation assembly was not loaded: {normalizedPath}");
    }

    private void RegisterOwnedAssembly(Assembly assembly)
    {
        _ownedAssemblies.Add(assembly);
    }

    private bool IsUnderShadowDirectory(string absolutePath)
    {
        if (string.Equals(absolutePath, _shadowDirectory, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = _shadowDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? _shadowDirectory
            : _shadowDirectory + Path.DirectorySeparatorChar;

        return absolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetBootstrapLoadPaths(NUnitGenerationManifest manifest) =>
    [
        Path.GetFullPath(manifest.FrameworkAssemblyPath),
            Path.GetFullPath(manifest.RuntimeAssemblyPath),
            Path.GetFullPath(manifest.ShadowAssemblyPath)
    ];
}

internal sealed class AssemblyReferenceEqualityComparer : IEqualityComparer<Assembly>
{
    internal static AssemblyReferenceEqualityComparer Instance { get; } = new();

    public bool Equals(Assembly? x, Assembly? y) => ReferenceEquals(x, y);

    public int GetHashCode(Assembly obj) => RuntimeHelpers.GetHashCode(obj);
}
#endif
