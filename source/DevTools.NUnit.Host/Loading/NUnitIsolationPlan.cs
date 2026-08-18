using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// Defines NUnit's explicit host bindings and immutable generation asset sources.
/// </summary>
internal static class NUnitIsolationPlan
{
    internal static AssemblyIsolationPlan Create(TestingGenerationManifest manifest, Assembly frameworkAssembly)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        if (frameworkAssembly is null) throw new ArgumentNullException(nameof(frameworkAssembly));

        var shadowDirectory = Path.GetFullPath(manifest.ShadowDirectory);
        var frameworkPath = NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest);
        var frameworkIdentity = AssemblyName.GetAssemblyName(frameworkPath);
        if (!AssemblyIdentityMatcher.IsCompatible(frameworkIdentity, frameworkAssembly.GetName()))
        {
            throw new NUnitGenerationBuildException(
                $"Host-selected nunit.framework '{frameworkAssembly.FullName}' is incompatible with generation shadow '{frameworkIdentity.FullName}'.");
        }

        var managedCandidates = manifest.ManagedAssemblies
            .Select(path => new AssemblyCandidate(path, "NUnit generation managed manifest", shadowDirectory));
        var nativeCandidates = manifest.NativeAssets
            .Select(path => new AssemblyCandidate(path, "NUnit generation native manifest", shadowDirectory));

        return AssemblyIsolationPlan.Create(manifest.RuntimeAssemblyPath)
            .WithLifecycle(
#if NET
                AssemblyIsolationLifecycle.Collectible
#else
                AssemblyIsolationLifecycle.ScopedNetFramework
#endif
            )
            .BindToParent(frameworkAssembly)
            .BindToParent(typeof(ITestingRuntimeSession).Assembly)
            .AddManagedSource(new ManifestAssemblySource(managedCandidates))
            .AddNativeSource(new ManifestNativeAssemblySource(nativeCandidates));
    }
}
