using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Sources;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Runtime;

internal sealed class ManifestRuntimeSessionFactory(string runtimeSessionTypeName) : ITestingRuntimeSessionFactory
{
    public ITestingRuntimeSession Create(TestingGenerationManifest generation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeSessionTypeName);
        var root = Path.GetFullPath(generation.ShadowDirectory);
        var plan = AssemblyIsolationPlan.Create(generation.RuntimeAssemblyPath)
            .WithKind(AssemblyIsolationKind.Isolated)
#if NETFRAMEWORK
            .WithDistinctFileIdentity()
#endif
            .Pin(typeof(ITestingRuntimeSession).Assembly)
            .AddManagedSource(new ManifestAssemblySource(
                generation.ManagedAssemblies.Select(path =>
                    new AssemblyCandidate(path, root))));

        return IsolatedRuntimeActivator.Activate(
            generation,
            plan,
            runtimeSessionTypeName,
            testAssembly => [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId]);
    }
}
