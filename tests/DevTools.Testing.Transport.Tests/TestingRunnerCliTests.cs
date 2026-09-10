using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class TestingRunnerCliTests
{
    static TestingRunRequest CreateRequest(TestingSelection selection) =>
        new(
            TestingProtocol.CurrentVersion,
            Guid.Empty,
            "provider.example",
            new TestingAssemblyReference(@"C:\tests\Sample.dll"),
            selection);

    [Fact]
    public void SerializeInvocation_strips_adapter_owned_host_fields()
    {
        var json = TestingRunnerCli.SerializeInvocation(
            CreateRequest(TestingSelection.All),
            new TestingHostOptions(
                "Revit",
                "2025",
                true,
                60,
                180,
                RunnerPath: @"C:\Runner.exe",
                DebugParentPid: 4242,
                FrameworkId: "nunit"));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunInvocation);

        Assert.NotNull(invocation);
        Assert.Equal(TestingProtocol.CurrentVersion, invocation.ProtocolVersion);
        Assert.True(invocation.Host.ForceLaunch);
        Assert.Equal(4242, invocation.Host.DebugParentPid);
        Assert.Null(invocation.Host.FrameworkId);
        Assert.Null(invocation.Host.RunnerPath);
        Assert.DoesNotContain("machine-run", json, StringComparison.Ordinal);
        Assert.DoesNotContain("--framework", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeInvocation_preserves_run_id_and_selection_kind()
    {
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var json = TestingRunnerCli.SerializeInvocation(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                "provider.example",
                new TestingAssemblyReference(@"C:\tests\Sample.dll"),
                TestingSelection.FromTestIds(["opaque-id"])),
            new TestingHostOptions("Revit", "2025", false, 60, 180, null, RequestTimeoutSeconds: 180));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunInvocation);

        Assert.NotNull(invocation);
        Assert.Equal(runId, invocation.Run.RunId);
        Assert.Equal(TestingSelectionKind.TestIds, invocation.Run.Selection.Kind);
        Assert.Equal(["opaque-id"], invocation.Run.Selection.TestIds);
        Assert.Equal(60, invocation.Host.PerTestTimeoutSeconds);
        Assert.Equal(180, invocation.Host.RequestTimeoutSeconds);
        Assert.Equal(180, invocation.Host.EffectiveRequestTimeoutSeconds);
    }

    [Fact]
    public void SerializeInvocation_empty_test_ids_is_not_all()
    {
        var json = TestingRunnerCli.SerializeInvocation(
            CreateRequest(TestingSelection.FromTestIds([])),
            new TestingHostOptions("Revit", "2025", false, 60, 180, null));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunInvocation);

        Assert.NotNull(invocation);
        Assert.Equal(TestingSelectionKind.TestIds, invocation.Run.Selection.Kind);
        Assert.Empty(invocation.Run.Selection.TestIds);
        Assert.True(invocation.Run.Selection.IsConstrained);
    }
}
