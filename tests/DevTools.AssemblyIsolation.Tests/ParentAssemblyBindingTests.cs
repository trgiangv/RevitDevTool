using System.Reflection;
using System.Reflection.Emit;
using DevTools.AssemblyIsolation.Bindings;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class ParentAssemblyBindingTests
{
    [Fact]
    public void Parent_binding_returns_the_exact_compatible_instance()
    {
        var expected = typeof(ParentAssemblyBindingTests).Assembly;
        var bindings = ParentAssemblyBindings.Create([expected]);

        Assert.True(bindings.TryResolve(expected.GetName(), out var actual));
        Assert.Same(expected, actual);
    }

    [Fact]
    public void Parent_binding_rejects_same_name_with_different_version_when_strict()
    {
        var loaded = typeof(ParentAssemblyBindingTests).Assembly;
        var requested = new AssemblyName(loaded.FullName!) { Version = new Version(99, 0, 0, 0) };
        var bindings = ParentAssemblyBindings.Create([new ParentAssemblyBinding(loaded)]);

        var error = Assert.Throws<AssemblyIdentityMismatchException>(
            () => bindings.TryResolve(requested, out _));
        Assert.Contains(loaded.GetName().Name!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parent_binding_accepts_requested_version_drift_when_host_share_is_explicit()
    {
        var loaded = typeof(ParentAssemblyBindingTests).Assembly;
        var requested = new AssemblyName(loaded.FullName!) { Version = new Version(99, 0, 0, 0) };
        var bindings = ParentAssemblyBindings.Create([new ParentAssemblyBinding(loaded, ignoreRequestedVersion: true)]);

        Assert.True(bindings.TryResolve(requested, out var actual));
        Assert.Same(loaded, actual);
    }

    [Fact]
    public void Parent_binding_returns_false_when_no_simple_name_is_bound()
    {
        var bindings = ParentAssemblyBindings.Create([typeof(ParentAssemblyBindingTests).Assembly]);

        Assert.False(bindings.TryResolve(new AssemblyName("Unbound.Component"), out var actual));
        Assert.Null(actual);
    }

    [Fact]
    public void Parent_binding_creation_rejects_duplicate_simple_names_with_different_identities()
    {
        var first = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Parent.Binding") { Version = new Version(1, 0, 0, 0) },
            AssemblyBuilderAccess.Run);
        var second = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Parent.Binding") { Version = new Version(2, 0, 0, 0) },
            AssemblyBuilderAccess.Run);

        Assert.Throws<AssemblyIdentityMismatchException>(() => ParentAssemblyBindings.Create([first, second]));
    }
}
