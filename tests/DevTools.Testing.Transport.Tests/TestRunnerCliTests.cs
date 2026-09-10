using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class TestRunnerCliTests
{
    static TestRunRequest CreateRequest(TestSelection selection) =>
        new(
            TestingProtocol.CurrentVersion,
            Guid.Empty,
            TestFrameworkId.NUnit,
            new TestAssemblyReference(@"C:\tests\Sample.dll"),
            selection);

    [Fact]
    public void SerializeExecute_strips_adapter_owned_host_fields()
    {
        var json = TestRunnerCli.SerializeExecute(
            CreateRequest(TestSelection.All),
            new TestHostOptions(
                "Revit",
                "2025",
                true,
                60,
                180,
                DebugParentPid: 4242));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);

        Assert.NotNull(invocation);
        Assert.Equal(TestingProtocol.CurrentVersion, invocation.ProtocolVersion);
        Assert.True(invocation.Host.ForceLaunch);
        Assert.Equal(4242, invocation.Host.DebugParentPid);
        Assert.DoesNotContain("framework_id\":\"nunit", json.Replace(" ", ""), StringComparison.Ordinal);
        Assert.DoesNotContain("runner_path", json, StringComparison.Ordinal);
        Assert.DoesNotContain("machine-run", json, StringComparison.Ordinal);
        Assert.DoesNotContain("--framework", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeExecute_preserves_run_id_and_selection_kind()
    {
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var json = TestRunnerCli.SerializeExecute(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                TestFrameworkId.NUnit,
                new TestAssemblyReference(@"C:\tests\Sample.dll"),
                TestSelection.FromTestIds(["opaque-id"])),
            new TestHostOptions("Revit", "2025", false, 60, 180, RequestTimeoutSeconds: 180));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);

        Assert.NotNull(invocation);
        Assert.Equal(runId, invocation.Run.RunId);
        Assert.Equal(TestSelectionKind.TestIds, invocation.Run.Selection.Kind);
        Assert.Equal(["opaque-id"], invocation.Run.Selection.TestIds);
        Assert.Equal(60, invocation.Host.PerTestTimeoutSeconds);
        Assert.Equal(180, invocation.Host.RequestTimeoutSeconds);
        Assert.Equal(180, invocation.Host.EffectiveRequestTimeoutSeconds);
    }

    [Fact]
    public void SerializeExecute_empty_test_ids_is_not_all()
    {
        var json = TestRunnerCli.SerializeExecute(
            CreateRequest(TestSelection.FromTestIds([])),
            new TestHostOptions("Revit", "2025", false, 60, 180));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);

        Assert.NotNull(invocation);
        Assert.Equal(TestSelectionKind.TestIds, invocation.Run.Selection.Kind);
        Assert.Empty(invocation.Run.Selection.TestIds);
        Assert.True(invocation.Run.Selection.IsConstrained);
    }
}
