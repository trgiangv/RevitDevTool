using AcadDevTool.Adapters;
using Autodesk.AutoCAD.Runtime;
using DevTools.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AcadDevTool.Tests;

public sealed class AcadCompiledScriptBridgeTests
{
    [Fact]
    public void Parent_bindings_include_the_core_api_used_by_compiled_scripts_without_duplicate_identities()
    {
        var bridge = new AcadCompiledScriptBridge(new StubHostAppInfo());
        var parentBindings = bridge.GetParentBindings().ToArray();
        var coreApiAssembly = typeof(Autodesk.AutoCAD.ApplicationServices.Core.Application).Assembly;

        Assert.True(parentBindings.Any(assembly => assembly.FullName == coreApiAssembly.FullName),
            $"Expected Core API '{coreApiAssembly.FullName}'.");
        Assert.Equal(
            parentBindings.Length,
            parentBindings.Select(assembly => assembly.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Compiled_command_runner_returns_success_after_invoking_a_static_command()
    {
        SuccessfulCommand.Calls = 0;
        var result = new AcadCommandRunner(NullLogger<AcadCommandRunner>.Instance)
            .RunCompiledCommand(new SuccessfulCommand());

        Assert.True(result.Success);
        Assert.Equal(1, SuccessfulCommand.Calls);
    }

    [Fact]
    public void Compiled_command_runner_preserves_the_command_failure()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new AcadCommandRunner(NullLogger<AcadCommandRunner>.Instance)
                .RunCompiledCommand(new FailingCommand()));

        Assert.Equal("command failure", error.Message);
    }

    private sealed class StubHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.AutoCad;
        public string VersionNumber => "2027";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    public sealed class SuccessfulCommand
    {
        public static int Calls;

        [CommandMethod("Success")]
        public static void Execute() => Calls++;
    }

    public sealed class FailingCommand
    {
        [CommandMethod("Failure")]
        public static void Execute() => throw new InvalidOperationException("command failure");
    }
}
