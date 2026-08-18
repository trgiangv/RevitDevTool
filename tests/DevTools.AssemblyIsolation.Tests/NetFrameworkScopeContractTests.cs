namespace DevTools.AssemblyIsolation.Tests;

public sealed class NetFrameworkScopeContractTests
{
    [Fact]
    public void Scoped_net_framework_session_never_claims_to_unload_default_app_domain_assemblies()
    {
        var entry = typeof(NetFrameworkScopeContractTests).Assembly;
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(entry.Location)
                .WithLifecycle(AssemblyIsolationLifecycle.ScopedNetFramework)
                .BindToParent(entry));

        Assert.Same(entry, session.LoadEntryAssembly());

        var result = session.VerifyUnload();

        Assert.False(result.IsCollectible);
        Assert.False(result.IsUnloaded);
        Assert.NotNull(result.Detail);
    }
}
