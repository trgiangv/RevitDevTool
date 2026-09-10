using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Runtime;

internal static class IsolatedRuntimeActivator
{
    public static ITestingRuntimeSession Activate(
        TestingGenerationManifest generation,
        AssemblyIsolationPlan plan,
        string runtimeSessionTypeName,
        Func<Assembly, object[]> constructorArguments,
        Func<ITestingRuntimeSession, AssemblyIsolationSession, string, ITestingRuntimeSession> wrap)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeSessionTypeName);
        ArgumentNullException.ThrowIfNull(constructorArguments);
        ArgumentNullException.ThrowIfNull(wrap);

        var isolation = AssemblyIsolationSession.Create(plan);
        try
        {
            var runtimeAssembly = isolation.LoadEntryAssembly();
            var testAssembly = isolation.LoadFromPath(generation.ShadowAssemblyPath);
            var sessionType = runtimeAssembly.GetType(runtimeSessionTypeName, throwOnError: true)!;
            var inner = (ITestingRuntimeSession)Activator.CreateInstance(
                sessionType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: constructorArguments(testAssembly),
                culture: null)!;
            return wrap(inner, isolation, generation.ShadowAssemblyPath);
        }
        catch
        {
            isolation.Dispose();
            throw;
        }
    }
}
