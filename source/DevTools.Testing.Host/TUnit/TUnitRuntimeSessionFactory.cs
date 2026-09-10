using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Sources;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;
namespace DevTools.Testing.Host.TUnit;

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

        return IsolatedRuntimeActivator.Activate(
            generation,
            plan,
            RuntimeSessionTypeName,
            testAssembly => [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId],
            (inner, isolation, shadow) => new IsolatedRuntimeSessionHandle(inner, isolation, shadow));
    }
}
