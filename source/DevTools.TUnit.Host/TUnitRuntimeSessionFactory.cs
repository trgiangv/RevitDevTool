using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Sources;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.TUnit.Host;

public sealed class TUnitRuntimeSessionFactory : ITestingRuntimeSessionFactory
{
    private const string RuntimeSessionTypeName = "DevTools.TUnit.Runtime.TUnitRuntimeSession";

    public ITestingRuntimeSession Create(TestingGenerationManifest generation)
    {
        var root = Path.GetFullPath(generation.ShadowDirectory);
        var plan = AssemblyIsolationPlan.Create(generation.RuntimeAssemblyPath)
            .WithKind(AssemblyIsolationKind.Isolated)
#if NETFRAMEWORK
            .WithDistinctFileIdentity()
#endif
            .Pin(typeof(ITestingRuntimeSession).Assembly)
            .AddManagedSource(new ManifestAssemblySource(
                generation.ManagedAssemblies.Select(path =>
                    new AssemblyCandidate(path, root))))
            .AddNativeSource(new ManifestNativeAssemblySource(
                generation.NativeAssets.Select(path =>
                    new AssemblyCandidate(path, root))));

        var isolation = AssemblyIsolationSession.Create(plan);
        try
        {
            var runtimeAssembly = isolation.LoadEntryAssembly();
            var testAssembly = isolation.LoadFromPath(generation.ShadowAssemblyPath);
            var sessionType = runtimeAssembly.GetType(RuntimeSessionTypeName, throwOnError: true)!;
            var inner = (ITestingRuntimeSession)Activator.CreateInstance(
                sessionType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId],
                culture: null)!;
            return new SessionHandle(inner, isolation, generation.ShadowAssemblyPath);
        }
        catch
        {
            isolation.Dispose();
            throw;
        }
    }

    private sealed class SessionHandle(
        ITestingRuntimeSession inner,
        AssemblyIsolationSession isolation,
        string shadowAssemblyPath) : ITestingRuntimeSession
    {
        private ITestingRuntimeSession? _inner = inner;
        public string GenerationId => Inner.GenerationId;

        public TestingRunResponse Run(
            TestingRunRequest request,
            ITestingRuntimeEventSink eventSink,
            CancellationToken cancellationToken) =>
            Inner.Run(
                request with { Assembly = request.Assembly with { Path = shadowAssemblyPath } },
                eventSink,
                cancellationToken);

        public void Cancel(Guid runId) => Inner.Cancel(runId);

        public void Dispose()
        {
            if (_inner is null)
                return;
            try
            {
                _inner.Dispose();
            }
            finally
            {
                _inner = null;
                isolation.Dispose();
            }
        }

        private ITestingRuntimeSession Inner => _inner
            ?? throw new ObjectDisposedException(nameof(SessionHandle));
    }
}
