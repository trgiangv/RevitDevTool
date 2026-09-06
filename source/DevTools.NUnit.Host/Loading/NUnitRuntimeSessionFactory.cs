using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;

namespace DevTools.NUnit.Host.Loading;

public sealed class NUnitRuntimeSessionFactory : ITestingRuntimeSessionFactory
{
    private const string RuntimeSessionTypeName = "DevTools.NUnit.Runtime.NUnitRuntimeSession";

    public ITestingRuntimeSession Create(TestingGenerationManifest generation)
    {
        ArgumentNullException.ThrowIfNull(generation);

        var frameworkPath = NUnitGenerationPolicy.GetFrameworkAssemblyPath(generation);
        var frameworkAssembly = NUnitFrameworkHostShare.GetOrLoadFromShadow(frameworkPath);
        var isolationSession = AssemblyIsolationSession.Create(NUnitIsolationPlan.Create(generation, frameworkAssembly));
        try
        {
            var runtimeAssembly = isolationSession.LoadEntryAssembly();
            var testAssembly = isolationSession.LoadFromPath(generation.ShadowAssemblyPath);
            var sessionType = runtimeAssembly.GetType(RuntimeSessionTypeName, throwOnError: true)!;

            var inner = (ITestingRuntimeSession)Activator.CreateInstance(
                sessionType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId, true],
                culture: null)!;

            return new NUnitRuntimeSessionHandle(inner, isolationSession, generation.ShadowAssemblyPath);
        }
        catch
        {
            isolationSession.Dispose();
            throw;
        }
    }
}
