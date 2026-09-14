using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.Testing.Host.NUnit.Loading;

public sealed class NUnitRuntimeSessionFactory : ITestingRuntimeSessionFactory
{
    private const string RuntimeSessionTypeName = "DevTools.NUnit.Runtime.NUnitRuntimeSession";

    public ITestingRuntimeSession Create(TestingGenerationManifest generation)
    {
        ArgumentNullException.ThrowIfNull(generation);

        var frameworkAssembly = NUnitFrameworkHostShare.GetOrLoadFromShadow(
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(generation));
        return IsolatedRuntimeActivator.Activate(
            generation,
            CreateIsolationPlan(generation, frameworkAssembly),
            RuntimeSessionTypeName,
            testAssembly => [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId, true],
            (inner, isolation, shadow) => new IsolatedRuntimeSessionHandle(inner, isolation, shadow));
    }

    internal static AssemblyIsolationPlan CreateIsolationPlan(
        TestingGenerationManifest manifest,
        Assembly frameworkAssembly)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(frameworkAssembly);

        var shadowDirectory = Path.GetFullPath(manifest.ShadowDirectory);
        var frameworkPath = NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest);
        var frameworkIdentity = AssemblyName.GetAssemblyName(frameworkPath);
        if (!AssemblyIdentityMatcher.IsCompatible(frameworkIdentity, frameworkAssembly.GetName()))
        {
            throw new TestingGenerationBuildException(
                $"Host-selected nunit.framework '{frameworkAssembly.FullName}' is incompatible with generation shadow '{frameworkIdentity.FullName}'.");
        }

        var managedCandidates = manifest.ManagedAssemblies
            .Select(path => new AssemblyCandidate(path, shadowDirectory));

        return AssemblyIsolationPlan.Create(manifest.RuntimeAssemblyPath)
            .WithKind(AssemblyIsolationKind.Isolated)
#if NETFRAMEWORK
            .WithDistinctFileIdentity()
#endif
            .Pin(frameworkAssembly)
            .Pin(typeof(ITestingRuntimeSession).Assembly)
            .AddManagedSource(new ManifestAssemblySource(managedCandidates))
            .WithGenerationNatives(manifest.ShadowAssemblyPath);
    }
}
