#if NETFRAMEWORK
using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.NUnit.Host.Loading;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal sealed class NetfxRunnerBindingDiagnostic
{
    internal NetfxRunnerBindingDiagnostic(
        Assembly runnerAssembly,
        Assembly generationFrameworkAssembly)
    {
        RunnerAssembly = runnerAssembly ?? throw new ArgumentNullException(nameof(runnerAssembly));
        GenerationFrameworkAssembly = generationFrameworkAssembly
            ?? throw new ArgumentNullException(nameof(generationFrameworkAssembly));
    }

    internal Assembly RunnerAssembly { get; }

    internal Assembly GenerationFrameworkAssembly { get; }
}

internal sealed class NetfxNUnitSessionHandle : ITestingRuntimeSession
{
    private const string RunnerFieldName = "_runner";

    private ITestingRuntimeSession _inner;
    private bool _disposed;

    internal NetfxNUnitSessionHandle(
        ITestingRuntimeSession inner,
        NUnitGenerationRegistry registry,
        NetfxNUnitGeneration generation)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    internal NetfxNUnitGeneration Generation { get; }

    private NUnitGenerationRegistry Registry { get; }

    public string GenerationId => _inner.GenerationId;

    internal Assembly GetLoadedTestAssembly() => Generation.TestAssembly;

    internal Assembly GetLoadedFrameworkAssembly() => Generation.GetLoadedFrameworkAssembly();

    internal Assembly GetLoadedRuntimeAssembly() => Generation.RuntimeAssembly;

    [UsedImplicitly]
    internal NetfxRunnerBindingDiagnostic GetRunnerBindingDiagnostic()
    {
        var runnerField = _inner.GetType().GetField(RunnerFieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Runtime session runner field was not found.");

        var runner = runnerField.GetValue(_inner)
            ?? throw new InvalidOperationException("Runtime session runner was not initialized.");

        return new NetfxRunnerBindingDiagnostic(
            runner.GetType().Assembly,
            Generation.GetLoadedFrameworkAssembly());
    }

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingRuntimeEventSink eventSink,
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
        }
    }

    internal NUnitRuntimeDiagnostic CreateRetainedDiagnostic() => Registry.CreateRetainedDiagnostic();
}

public sealed class NetfxNUnitRuntimeSessionFactory : ITestingRuntimeSessionFactory, IDisposable
{
    private const string RuntimeSessionTypeName = "DevTools.NUnit.Runtime.NUnitRuntimeSession";

    private readonly NUnitGenerationRegistry _registry = new();
    private readonly ResolveEventHandler _resolveHandler;
    private readonly object _lifecycleLock = new();
    private bool _disposed;

    public NetfxNUnitRuntimeSessionFactory()
    {
        _resolveHandler = OnAssemblyResolve;
        _registry.RegisterAssemblyResolveHandler(_resolveHandler);
    }

    public int RetainedGenerationCount => _registry.RetainedGenerationCount;

    internal bool HandlerIsRegisteredForTesting => _registry.HandlerIsRegisteredForTesting;

    internal IReadOnlyList<GenerationAssemblyResolutionRecord> LazyResolutionRecords =>
        _registry.LazyResolutionRecords;

    public ITestingRuntimeSession Create(TestingGenerationManifest generation)
    {
        if (generation is null)
            throw new ArgumentNullException(nameof(generation));

        lock (_lifecycleLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NetfxNUnitRuntimeSessionFactory));

            var loadedGeneration = _registry.GetOrCreate(NUnitGenerationManifestAdapter.ToNUnit(generation));
            loadedGeneration.EnsureLoaded(_registry);

            var inner = CreateRuntimeSession(loadedGeneration, _registry);
            return new NetfxNUnitSessionHandle(inner, _registry, loadedGeneration);
        }
    }

    internal ITestingRuntimeSession Create(NUnitGenerationManifest generation) =>
        Create(NUnitGenerationManifestAdapter.ToTesting(generation));

    internal NUnitRuntimeDiagnostic CreateRetainedDiagnostic() => _registry.CreateRetainedDiagnostic();

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _registry.UnregisterAssemblyResolveHandler(_resolveHandler);
        }
    }

    private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args) =>
        _registry.ResolveAssembly(sender, args);

    private static ITestingRuntimeSession CreateRuntimeSession(
        NetfxNUnitGeneration generation,
        NUnitGenerationRegistry registry)
    {
        var runtimeAssembly = generation.RuntimeAssembly;
        var testAssembly = generation.TestAssembly;
        var sessionType = runtimeAssembly.GetType(RuntimeSessionTypeName, throwOnError: true)!;

        var hostCore = typeof(ITestingRuntimeSession).Assembly;
        var runtimeCore = sessionType.Assembly.GetReferencedAssemblies()
            .FirstOrDefault(reference =>
                string.Equals(
                    reference.Name,
                    hostCore.GetName().Name,
                    StringComparison.OrdinalIgnoreCase));

        if (runtimeCore is not null
            && !string.Equals(runtimeCore.FullName, hostCore.FullName, StringComparison.OrdinalIgnoreCase))
        {
            throw new NUnitGenerationAssemblyResolutionException(
                "Generation runtime must bind DevTools.Testing.Abstractions to the host copy.");
        }

        registry.SetActiveLoadingGeneration(generation);
        try
        {
            return (ITestingRuntimeSession)Activator.CreateInstance(
                sessionType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: [testAssembly, generation.Manifest.ShadowAssemblyPath, generation.GenerationId, true],
                culture: null)!;
        }
        finally
        {
            registry.ClearActiveLoadingGeneration(generation);
        }
    }
}
#endif
