#if NETFRAMEWORK
using System.Reflection;

namespace DevTools.NUnit.Host.Loading;

internal sealed class GenerationAssemblyResolutionRecord
{
    internal GenerationAssemblyResolutionRecord(
        string generationId,
        string requestedAssemblyName,
        string requestingAssemblyName,
        string? requestingAssemblyLocation,
        string resolvedAssemblyLocation)
    {
        GenerationId = generationId;
        RequestedAssemblyName = requestedAssemblyName;
        RequestingAssemblyName = requestingAssemblyName;
        RequestingAssemblyLocation = requestingAssemblyLocation;
        ResolvedAssemblyLocation = resolvedAssemblyLocation;
    }

    internal string GenerationId { get; }

    internal string RequestedAssemblyName { get; }

    internal string RequestingAssemblyName { get; }

    internal string? RequestingAssemblyLocation { get; }

    internal string ResolvedAssemblyLocation { get; }
}

internal sealed class NUnitGenerationRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, NetfxNUnitGeneration> _generationsById =
        new(StringComparer.Ordinal);

    private readonly Dictionary<Assembly, NetfxNUnitGeneration> _generationByAssembly =
        new(AssemblyReferenceEqualityComparer.Instance);

    private readonly List<GenerationAssemblyResolutionRecord> _lazyResolutionRecords = new List<GenerationAssemblyResolutionRecord>();

    private NetfxNUnitGeneration? _activeLoadingGeneration;

    internal int RetainedGenerationCount
    {
        get
        {
            lock (_sync)
                return _generationsById.Count;
        }
    }

    internal bool HandlerIsRegisteredForTesting { get; private set; }

    internal IReadOnlyList<GenerationAssemblyResolutionRecord> LazyResolutionRecords
    {
        get
        {
            lock (_sync)
                return _lazyResolutionRecords.ToList();
        }
    }

    internal NetfxNUnitGeneration GetOrCreate(NUnitGenerationManifest manifest)
    {
        if (manifest is null)
            throw new ArgumentNullException(nameof(manifest));

        lock (_sync)
        {
            if (_generationsById.TryGetValue(manifest.GenerationId, out var existing))
                return existing;

            var generation = new NetfxNUnitGeneration(manifest);
            _generationsById[manifest.GenerationId] = generation;
            return generation;
        }
    }

    internal void RegisterOwnedAssembly(NetfxNUnitGeneration generation, Assembly assembly)
    {
        if (generation is null)
            throw new ArgumentNullException(nameof(generation));
        if (assembly is null)
            throw new ArgumentNullException(nameof(assembly));

        lock (_sync)
            _generationByAssembly[assembly] = generation;
    }

    internal void SetActiveLoadingGeneration(NetfxNUnitGeneration generation)
    {
        if (generation is null)
            throw new ArgumentNullException(nameof(generation));

        lock (_sync)
            _activeLoadingGeneration = generation;
    }

    internal void ClearActiveLoadingGeneration(NetfxNUnitGeneration generation)
    {
        if (generation is null)
            throw new ArgumentNullException(nameof(generation));

        lock (_sync)
        {
            if (ReferenceEquals(_activeLoadingGeneration, generation))
                _activeLoadingGeneration = null;
        }
    }

    internal NetfxNUnitGeneration? TryGetGenerationForRequestingAssembly(Assembly? requestingAssembly)
    {
        if (requestingAssembly is null)
            return null;

        lock (_sync)
        {
            return _generationByAssembly.TryGetValue(requestingAssembly, out var generation)
                ? generation
                : null;
        }
    }

    internal NetfxNUnitGeneration? TryGetActiveLoadingGeneration()
    {
        lock (_sync)
            return _activeLoadingGeneration;
    }

    internal void RegisterAssemblyResolveHandler(ResolveEventHandler handler)
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        lock (_sync)
        {
            if (HandlerIsRegisteredForTesting)
            {
                throw new InvalidOperationException(
                    "An AppDomain.AssemblyResolve handler is already registered for this registry.");
            }

            AppDomain.CurrentDomain.AssemblyResolve += handler;
            HandlerIsRegisteredForTesting = true;
        }
    }

    internal void UnregisterAssemblyResolveHandler(ResolveEventHandler handler)
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        lock (_sync)
        {
            if (!HandlerIsRegisteredForTesting)
                return;

            AppDomain.CurrentDomain.AssemblyResolve -= handler;
            HandlerIsRegisteredForTesting = false;
        }
    }

    internal Assembly? ResolveAssembly(object? sender, ResolveEventArgs args)
    {
        if (!TryParseRequestedAssemblyName(args.Name, out var requested, out var simpleName))
            return null;

        if (NUnitSharedAssemblyPolicy.IsShared(simpleName))
            return NetfxNUnitSharedAssemblyResolver.TryResolveFromAppDomain(requested);

        var generation = GetGenerationForResolve(args.RequestingAssembly);
        if (generation is null)
            return null;

        if (TryResolveOwnedFramework(generation, simpleName, args.RequestingAssembly, out var framework))
            return framework;

        return ResolveGenerationDependency(generation, requested, args.RequestingAssembly);
    }

    private static bool TryParseRequestedAssemblyName(
        string? name,
        out AssemblyName requested,
        out string simpleName)
    {
        requested = new AssemblyName();
        simpleName = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            requested = new AssemblyName(name);
        }
        catch
        {
            return false;
        }

        simpleName = requested.Name ?? string.Empty;
        return simpleName.Length > 0;
    }

    private NetfxNUnitGeneration? GetGenerationForResolve(Assembly? requestingAssembly)
    {
        lock (_sync)
        {
            return TryGetGenerationForRequestingAssembly(requestingAssembly)
                ?? _activeLoadingGeneration;
        }
    }

    private bool TryResolveOwnedFramework(
        NetfxNUnitGeneration generation,
        string simpleName,
        Assembly? requestingAssembly,
        out Assembly framework)
    {
        framework = null!;
        if (!string.Equals(simpleName, "nunit.framework", StringComparison.OrdinalIgnoreCase))
            return false;

        if (requestingAssembly is null || !generation.OwnsAssembly(requestingAssembly))
            return false;

        framework = generation.GetLoadedFrameworkAssembly();
        RegisterOwnedAssembly(generation, framework);
        return true;
    }

    private Assembly? ResolveGenerationDependency(
        NetfxNUnitGeneration generation,
        AssemblyName requested,
        Assembly? requestingAssembly)
    {
        try
        {
            var resolved = generation.ResolveDependency(this, requested, requestingAssembly);
            if (resolved is null)
                return null;

            RegisterOwnedAssembly(generation, resolved);
            RecordRequestingResolution(generation, requested, requestingAssembly, resolved);
            return resolved;
        }
        catch (NUnitGenerationAssemblyResolutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new NUnitGenerationAssemblyResolutionException(
                $"Failed to resolve assembly '{requested.FullName}' for generation '{generation.GenerationId}'.",
                ex);
        }
    }

    private void RecordRequestingResolution(
        NetfxNUnitGeneration generation,
        AssemblyName requested,
        Assembly? requestingAssembly,
        Assembly resolved)
    {
        if (requestingAssembly is null)
            return;

        RecordLazyResolution(generation, requested, requestingAssembly, resolved.Location);
        generation.RecordLazyResolution();
    }

    internal NUnitRuntimeDiagnostic CreateRetainedDiagnostic()
    {
        var count = RetainedGenerationCount;
        return new NUnitRuntimeDiagnostic(
            "generation.retained",
            $"Managed generation count retained until host exit: {count}.");
    }

    private void RecordLazyResolution(
        NetfxNUnitGeneration generation,
        AssemblyName requested,
        Assembly requestingAssembly,
        string resolvedAssemblyLocation)
    {
        var requestingName = requestingAssembly.GetName().Name
            ?? throw new NUnitGenerationAssemblyResolutionException(
                "Requesting assembly name is missing during lazy resolution.");

        lock (_sync)
        {
            _lazyResolutionRecords.Add(new GenerationAssemblyResolutionRecord(
                generation.GenerationId,
                requested.Name ?? requested.FullName,
                requestingName,
                requestingAssembly.Location,
                resolvedAssemblyLocation));
        }
    }
}
#endif
