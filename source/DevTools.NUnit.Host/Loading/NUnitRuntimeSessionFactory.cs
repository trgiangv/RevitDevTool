#if NET
using System.Reflection;
using DevTools.NUnit.Core.Runtime;

namespace DevTools.NUnit.Host.Loading;

public sealed class NUnitRuntimeSessionFactory : INUnitRuntimeSessionFactory
{
    private const string RuntimeSessionTypeName = "DevTools.NUnit.Runtime.NUnitRuntimeSession";

    public INUnitRuntimeSession Create(NUnitGenerationManifest generation)
    {
        ArgumentNullException.ThrowIfNull(generation);

        var loadContext = new NUnitRuntimeLoadContext(generation);
        try
        {
            var runtimeAssembly = loadContext.LoadFromManifestPath(generation.RuntimeAssemblyPath);
            var testAssembly = loadContext.LoadFromManifestPath(generation.ShadowAssemblyPath);
            var sessionType = runtimeAssembly.GetType(RuntimeSessionTypeName, throwOnError: true)!;

            var inner = (INUnitRuntimeSession)Activator.CreateInstance(
                sessionType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId, true],
                culture: null)!;

            return new NUnitRuntimeSessionHandle(inner, loadContext);
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }
}
#endif
