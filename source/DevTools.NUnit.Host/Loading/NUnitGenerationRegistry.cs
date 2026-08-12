#if NETFRAMEWORK
using System.Reflection;
using DevTools.NUnit.Core.Contracts;

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
    private readonly Dictionary<string, NetFrameworkNUnitGeneration> _generationsById =
        new(StringComparer.Ordinal);

    private readonly Dictionary<Assembly, NetFrameworkNUnitGeneration> _generationByAssembly =
        new(AssemblyReferenceEqualityComparer.Instance);

    private readonly List<GenerationAssemblyResolutionRecord> _lazyResolutionRecords = new List<GenerationAssemblyResolutionRecord>();

    private NetFrameworkNUnitGeneration? _activeLoadingGeneration;

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

    internal NetFrameworkNUnitGeneration GetOrCreate(NUnitGenerationManifest manifest)
    {
        if (manifest is null)
            throw new ArgumentNullException(nameof(manifest));

        lock (_sync)
        {
            if (_generationsById.TryGetValue(manifest.GenerationId, out var existing))
                return existing;

            var generation = new NetFrameworkNUnitGeneration(manifest);
            _generationsById[manifest.GenerationId] = generation;
            return generation;
        }
    }

    internal void RegisterOwnedAssembly(NetFrameworkNUnitGeneration generation, Assembly assembly)
    {
        if (generation is null)
            throw new ArgumentNullException(nameof(generation));
        if (assembly is null)
            throw new ArgumentNullException(nameof(assembly));

        lock (_sync)
            _generationByAssembly[assembly] = generation;
    }

    internal void SetActiveLoadingGeneration(NetFrameworkNUnitGeneration generation)
    {
        if (generation is null)
            throw new ArgumentNullException(nameof(generation));

        lock (_sync)
            _activeLoadingGeneration = generation;
    }

    internal void ClearActiveLoadingGeneration(NetFrameworkNUnitGeneration generation)
    {
        if (generation is null)
            throw new ArgumentNullException(nameof(generation));

        lock (_sync)
        {
            if (ReferenceEquals(_activeLoadingGeneration, generation))
                _activeLoadingGeneration = null;
        }
    }

    internal NetFrameworkNUnitGeneration? TryGetGenerationForRequestingAssembly(Assembly? requestingAssembly)
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

    internal NetFrameworkNUnitGeneration? TryGetActiveLoadingGeneration()
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
        if (string.IsNullOrWhiteSpace(args.Name))
            return null;

        AssemblyName requested;
        try
        {
            requested = new AssemblyName(args.Name);
        }
        catch
        {
            return null;
        }

        var simpleName = requested.Name;
        if (string.IsNullOrWhiteSpace(simpleName))
            return null;

        if (NUnitSharedAssemblyPolicy.IsShared(simpleName))
            return NetFrameworkNUnitSharedAssemblyResolver.TryResolveFromAppDomain(requested);

        NetFrameworkNUnitGeneration? generation;
        lock (_sync)
        {
            generation = TryGetGenerationForRequestingAssembly(args.RequestingAssembly)
                ?? _activeLoadingGeneration;
        }

        if (generation is null)
            return null;

        if (string.Equals(simpleName, "nunit.framework", StringComparison.OrdinalIgnoreCase)
            && args.RequestingAssembly is not null
            && generation.OwnsAssembly(args.RequestingAssembly))
        {
            var framework = generation.GetLoadedFrameworkAssembly();
            RegisterOwnedAssembly(generation, framework);
            return framework;
        }

        try
        {
            var resolved = generation.ResolveDependency(this, requested, args.RequestingAssembly);
            if (resolved is null)
                return null;

            RegisterOwnedAssembly(generation, resolved);

            if (args.RequestingAssembly is not null)
            {
                RecordLazyResolution(generation, requested, args.RequestingAssembly, resolved.Location);
                generation.RecordLazyResolution();
            }

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

    internal NUnitRuntimeDiagnostic CreateRetainedDiagnostic()
    {
        var count = RetainedGenerationCount;
        return new NUnitRuntimeDiagnostic(
            "generation.retained",
            $"Managed generation count retained until host exit: {count}.");
    }

    private void RecordLazyResolution(
        NetFrameworkNUnitGeneration generation,
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
