using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

[TestClass]
public sealed class TestRunnerCliTests
{
    static TestRunRequest CreateRequest(TestSelection selection) =>
        new(
            TestingProtocol.CurrentVersion,
            Guid.Empty,
            TestFrameworkId.NUnit,
            new TestAssemblyReference(@"C:\tests\Sample.dll"),
            selection);

    [TestMethod]
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

        Assert.IsNotNull(invocation);
        Assert.AreEqual(TestingProtocol.CurrentVersion, invocation.ProtocolVersion);
        Assert.IsTrue(invocation.Host.ForceLaunch);
        Assert.AreEqual(4242, invocation.Host.DebugParentPid);
        Assert.DoesNotContain("framework_id\":\"nunit", json.Replace(" ", ""), StringComparison.Ordinal);
        Assert.DoesNotContain("runner_path", json, StringComparison.Ordinal);
        Assert.DoesNotContain("machine-run", json, StringComparison.Ordinal);
        Assert.DoesNotContain("--framework", json, StringComparison.Ordinal);
    }

    [TestMethod]
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

        Assert.IsNotNull(invocation);
        Assert.AreEqual(runId, invocation.Run.RunId);
        Assert.AreEqual(TestSelectionKind.TestIds, invocation.Run.Selection.Kind);
        Assert.AreSequenceEqual(["opaque-id"], invocation.Run.Selection.TestIds);
        Assert.AreEqual(60, invocation.Host.PerTestTimeoutSeconds);
        Assert.AreEqual(180, invocation.Host.RequestTimeoutSeconds);
        Assert.AreEqual(180, invocation.Host.EffectiveRequestTimeoutSeconds);
    }

    [TestMethod]
    public void SerializeExecute_empty_test_ids_is_not_all()
    {
        var json = TestRunnerCli.SerializeExecute(
            CreateRequest(TestSelection.FromTestIds([])),
            new TestHostOptions("Revit", "2025", false, 60, 180));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);

        Assert.IsNotNull(invocation);
        Assert.AreEqual(TestSelectionKind.TestIds, invocation.Run.Selection.Kind);
        Assert.IsEmpty(invocation.Run.Selection.TestIds);
        Assert.IsTrue(invocation.Run.Selection.IsConstrained);
    }
}
