using System.Reflection;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class AssemblyIdentityMatcherTests
{
    [Fact]
    public void Assembly_identity_mismatch_exception_rejects_a_null_requested_identity()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AssemblyIdentityMismatchException(null!, new AssemblyName("Contoso.Component")));
    }

    [Fact]
    public void Assembly_identity_mismatch_exception_rejects_a_null_candidate_identity()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AssemblyIdentityMismatchException(new AssemblyName("Contoso.Component"), null!));
    }

    [Fact]
    public void Is_compatible_matches_simple_names_case_insensitively()
    {
        var requested = new AssemblyName("Contoso.Component");
        var candidate = new AssemblyName("contoso.component");

        Assert.True(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
    }

    [Fact]
    public void Is_compatible_rejects_a_different_requested_version()
    {
        var requested = new AssemblyName("Contoso.Component, Version=2.0.0.0");
        var candidate = new AssemblyName("Contoso.Component, Version=1.0.0.0");

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
    }

    [Fact]
    public void Is_compatible_for_parent_share_ignores_requested_version()
    {
        var requested = new AssemblyName("RevitAPIUI, Version=2025.0.0.0, Culture=neutral, PublicKeyToken=null");
        var parent = new AssemblyName("RevitAPIUI, Version=2025.0.2.0, Culture=neutral, PublicKeyToken=null");

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, parent));
        Assert.True(AssemblyIdentityMatcher.IsCompatibleForParentShare(requested, parent));
    }

    [Fact]
    public void Is_compatible_ignores_candidate_version_when_request_does_not_specify_one()
    {
        var requested = new AssemblyName("Contoso.Component");
        var candidate = new AssemblyName("Contoso.Component, Version=2.0.0.0");

        Assert.True(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
    }

    [Fact]
    public void Is_compatible_normalizes_neutral_culture()
    {
        var requested = new AssemblyName("Contoso.Component, Culture=neutral");
        var candidate = new AssemblyName("Contoso.Component");

        Assert.True(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
    }

    [Fact]
    public void Is_compatible_rejects_a_different_requested_culture()
    {
        var requested = new AssemblyName("Contoso.Component, Culture=fr-FR");
        var candidate = new AssemblyName("Contoso.Component, Culture=en-US");

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
    }

    [Fact]
    public void Is_compatible_requires_an_exact_requested_public_key_token()
    {
        var requested = new AssemblyName("Contoso.Component");
        requested.SetPublicKeyToken([0x01, 0x02, 0x03, 0x04]);
        var candidate = new AssemblyName("Contoso.Component");
        candidate.SetPublicKeyToken([0x04, 0x03, 0x02, 0x01]);

        Assert.False(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
    }

    [Fact]
    public void Is_compatible_accepts_an_exact_requested_public_key_token()
    {
        var requested = new AssemblyName("Contoso.Component");
        requested.SetPublicKeyToken([0x01, 0x02, 0x03, 0x04]);
        var candidate = new AssemblyName("Contoso.Component");
        candidate.SetPublicKeyToken([0x01, 0x02, 0x03, 0x04]);

        Assert.True(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
    }
}
