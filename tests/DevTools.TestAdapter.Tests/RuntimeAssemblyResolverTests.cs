using System.Reflection;

namespace DevTools.TestAdapter.Tests;

public sealed class RuntimeAssemblyResolverTests
{
    [Fact]
    public void Private_runtime_resolver_loads_only_the_exact_private_closure_identity()
    {
        var candidate = typeof(TestingPlatformBuilderHook).Assembly.GetName();

        var resolved = Resolve(candidate.FullName!);

        Assert.Same(typeof(TestingPlatformBuilderHook).Assembly, resolved);
    }

    [Fact]
    public void Private_runtime_resolver_rejects_a_same_simple_name_with_a_different_version()
    {
        var candidate = typeof(TestingPlatformBuilderHook).Assembly.GetName();
        var requested = new AssemblyName(candidate.FullName)
        {
            Version = new Version(candidate.Version!.Major + 1, 0, 0, 0),
        };

        var resolved = Resolve(requested.FullName!);

        Assert.Null(resolved);
    }

    [Fact]
    public void Private_runtime_resolver_rejects_incomplete_and_path_like_requests()
    {
        var candidate = typeof(TestingPlatformBuilderHook).Assembly.GetName();

        Assert.Null(Resolve(candidate.Name!));
        Assert.Null(Resolve($"..\\{candidate.Name}, Version={candidate.Version}, Culture=neutral, PublicKeyToken=null"));
    }

    private static Assembly? Resolve(string requestedIdentity)
    {
        var resolver = typeof(RuntimeAssemblyResolver).GetMethod(
            "ResolvePrivateRuntimeAssembly",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Private runtime resolver was not found.");

        return (Assembly?)resolver.Invoke(null, [null, new ResolveEventArgs(requestedIdentity)]);
    }
}
