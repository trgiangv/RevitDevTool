#if NET
using System.Reflection;
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

        var nunitGeneration = NUnitGenerationManifestAdapter.ToNUnit(generation);
        var loadContext = new NUnitRuntimeLoadContext(nunitGeneration);
        try
        {
            var runtimeAssembly = loadContext.LoadFromManifestPath(nunitGeneration.RuntimeAssemblyPath);
            var testAssembly = loadContext.LoadFromManifestPath(nunitGeneration.ShadowAssemblyPath);
            var sessionType = runtimeAssembly.GetType(RuntimeSessionTypeName, throwOnError: true)!;

            var inner = (ITestingRuntimeSession)Activator.CreateInstance(
                sessionType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: [testAssembly, nunitGeneration.ShadowAssemblyPath, nunitGeneration.GenerationId, true],
                culture: null)!;

            return new NUnitRuntimeSessionHandle(inner, loadContext);
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    internal ITestingRuntimeSession Create(NUnitGenerationManifest generation) =>
        Create(NUnitGenerationManifestAdapter.ToTesting(generation));
}
#endif
