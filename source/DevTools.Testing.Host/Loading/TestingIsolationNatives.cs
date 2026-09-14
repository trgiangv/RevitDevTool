using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.Testing.Host.Loading;

internal static class TestingIsolationNatives
{
    public static AssemblyIsolationPlan WithGenerationNatives(
        this AssemblyIsolationPlan plan,
        string shadowAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shadowAssemblyPath);
#if NET
        return plan.AddNativeSource(new ResolverNativeAssemblySource(shadowAssemblyPath));
#else
        return plan;
#endif
    }
}
