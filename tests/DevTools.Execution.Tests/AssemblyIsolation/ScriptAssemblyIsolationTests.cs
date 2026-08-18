using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.Execution.Abstractions;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests.AssemblyIsolation;

public sealed class ScriptAssemblyIsolationTests
{
    [Fact]
    public void Script_plan_binds_the_explicit_parent_contract_and_only_resolves_selected_nuget_assemblies()
    {
        var parent = typeof(ScriptAssemblyIsolationTests).Assembly;
        var selectedPackageAssembly = typeof(Microsoft.Extensions.Logging.ILogger).Assembly;
        var plan = ScriptIsolationPlan.Create(
            "ScriptAssemblyIsolationTests",
            [selectedPackageAssembly.Location],
            [parent]);

        Assert.Equal(AssemblyIsolationLifecycle.Collectible, plan.Lifecycle);
        Assert.True(plan.ParentBindings.TryResolve(parent.GetName(), out var actualParent));
        Assert.Same(parent, actualParent);

        var selected = selectedPackageAssembly.GetName();
        Assert.Equal(Path.GetFullPath(selectedPackageAssembly.Location), plan.ManagedSources.Single().Resolve(selected)!.Path);

        var versionDrift = new AssemblyName(selected.FullName) { Version = new Version(99, 0, 0, 0) };
        Assert.Null(plan.ManagedSources.Single().Resolve(versionDrift));
    }

    [Fact]
    public void Script_plan_loads_compiled_bytes_into_a_collectible_session()
    {
        using var workload = CommandFixtureWorkload.Create(includeSibling: false);
        using var session = CreateAndLoad(File.ReadAllBytes(workload.EntryPath));

        var result = session.VerifyUnload();

        Assert.True(result.IsCollectible);
        Assert.True(result.IsUnloaded, result.Detail);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static AssemblyIsolationSession CreateAndLoad(byte[] assemblyBytes)
    {
        var session = AssemblyIsolationSession.Create(
            ScriptIsolationPlan.Create("ScriptAssemblyIsolationTests", Array.Empty<string>(), Array.Empty<Assembly>()));
        _ = session.LoadAssembly(assemblyBytes);
        return session;
    }

    [Fact]
    public async Task Compiler_keeps_the_collectible_session_until_the_compiled_command_handoff_is_disposed()
    {
        var compiler = new CSharpCompiler(NullLogger<CSharpCompiler>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var bridge = new ContractBridge();
        const string script = "public sealed class ScriptCommand { public System.Type ContractType() => typeof(DevTools.Execution.Tests.AssemblyIsolation.ScriptContract); }";

        var result = await compiler.CompileAsync(script, bridge, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.FormatDiagnostics());
        Assert.NotNull(result.Cleanup);
        Assert.NotNull(result.Command);
        var command = result.Command!;
        var contractType = (Type)command.GetType().GetMethod("ContractType")!.Invoke(command, null)!;
        Assert.Same(typeof(ScriptContract), contractType);
        result.Cleanup!.Dispose();
    }

    private sealed class ContractBridge : ICompiledScriptBridge
    {
        public IEnumerable<string> GetSessionReferences() => [typeof(ScriptContract).Assembly.Location];
        public IEnumerable<Assembly> GetParentBindings() => [typeof(ScriptContract).Assembly];
        public Type? TryFindCommandType(Assembly assembly) => assembly.GetType("ScriptCommand");
        public string? GetHostReferencePattern() => null;
        public string GetHostReferenceReplacement() => string.Empty;
    }
}

public static class ScriptContract
{
}
