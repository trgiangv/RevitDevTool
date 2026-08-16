using DevTools.Utilities.AssemblyLoading;

namespace DevTools.Utilities.Tests;

[CollectionDefinition("HostSharedAssemblies", DisableParallelization = true)]
public sealed class HostSharedAssembliesCollection;

[Collection("HostSharedAssemblies")]
public sealed class HostSharedAssemblyPolicyTests
{
    [Fact]
    public void Use_revit_policy_shares_RevitAPI_not_acmgd()
    {
        HostSharedAssemblies.Use(new StubPolicy(["RevitAPI", "RevitAPIUI", "AdWindows"], ["Autodesk."]));
        Assert.True(HostSharedAssemblies.IsShared("RevitAPI"));
        Assert.True(HostSharedAssemblies.IsExplicitHostAssembly("RevitAPI"));
        Assert.True(HostSharedAssemblies.IsShared("Autodesk.Revit.DB"));
        Assert.True(HostSharedAssemblies.IsShared("MahApps.Metro"));
        Assert.False(HostSharedAssemblies.IsExplicitHostAssembly("acmgd"));
    }

    [Fact]
    public void Use_acad_policy_shares_acmgd_not_RevitAPI()
    {
        HostSharedAssemblies.Use(new StubPolicy(["acmgd", "acdbmgd"], ["Autodesk."]));
        Assert.True(HostSharedAssemblies.IsShared("acmgd"));
        Assert.True(HostSharedAssemblies.IsExplicitHostAssembly("acmgd"));
        Assert.True(HostSharedAssemblies.IsShared("Autodesk.AutoCAD.DatabaseServices"));
        Assert.False(HostSharedAssemblies.IsExplicitHostAssembly("RevitAPI"));
    }

    [Fact]
    public void Without_policy_host_api_names_are_not_shared()
    {
        HostSharedAssemblies.Use(new StubPolicy([], []));
        Assert.False(HostSharedAssemblies.IsExplicitHostAssembly("RevitAPI"));
        Assert.False(HostSharedAssemblies.IsExplicitHostAssembly("acmgd"));
        Assert.True(HostSharedAssemblies.IsShared("MahApps.Metro"));
    }

    [Fact]
    public void Execution_Abstractions_owns_ui_package_prefixes()
    {
        var path = Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.Execution.Abstractions",
            "HostPackagePrefixes.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("MahApps.", text, StringComparison.Ordinal);
        Assert.Contains("ControlzEx.", text, StringComparison.Ordinal);
        Assert.Contains("CommunityToolkit.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Utilities_does_not_reference_Hosting()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(),
            "source",
            "DevTools.Utilities",
            "DevTools.Utilities.csproj"));
        Assert.DoesNotContain("DevTools.Hosting.csproj", csproj, StringComparison.Ordinal);
    }

    private sealed class StubPolicy(
        IReadOnlyCollection<string> names,
        IReadOnlyCollection<string> prefixes) : IHostSharedAssemblyPolicy
    {
        public IReadOnlyCollection<string> HostApiSimpleNames => names;
        public IReadOnlyCollection<string> HostApiPrefixes => prefixes;
    }
}
