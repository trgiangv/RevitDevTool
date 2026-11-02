using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Utilities;
using System.Text.Json;

namespace DevTools.Testing.Transport.Tests;

[TestClass]
public sealed class TestingProtocolRangeTests
{
    [TestMethod]
    [DataRow(2, 2, 2, 2, true)]
    [DataRow(3, 2, 2, 2, true)]
    [DataRow(2, 2, 3, 2, true)]
    [DataRow(3, 3, 2, 2, false)]
    [DataRow(2, 2, 3, 3, false)]
    [DataRow(4, 3, 3, 1, true)]
    [DataRow(1, 1, 3, 2, false)]
    public void Ranges_are_compatible_when_they_overlap(int aCurrent, int aMin, int bCurrent, int bMin, bool expected)
    {
        var a = new ProtocolRange(aCurrent, aMin);
        var b = new ProtocolRange(bCurrent, bMin);

        Assert.AreEqual(expected, a.Overlaps(b));
        Assert.AreEqual(expected, b.Overlaps(a));
    }

    [TestMethod]
    public void Hello_carries_the_sender()
    {
        var json = JsonSerializer.Serialize(
            new TestHelloRequest(TestFrameworkId.NUnit, Sender("4.1.0", 3, 2)),
            TestingJsonContext.Default.TestHelloRequest);

        Assert.Contains("\"sender\":{\"version\":\"4.1.0\",\"protocol\":{\"current\":3,\"min\":2}}", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Response_with_unknown_members_still_reads()
    {
        var hello = JsonSerializer.Deserialize(
            """{"framework_id":"NUnit","host":"Revit","host_version":"2025","process_id":1,"is_busy":false,"sender":{"version":"4.1.0","protocol":{"current":1,"min":1}},"future_field":{"x":1}}""",
            TestingJsonContext.Default.TestHelloResponse);

        Assert.AreEqual("4.1.0", hello!.Sender!.Version);
        Assert.IsNull(TestingProtocol.Mismatch(hello));
    }

    [TestMethod]
    public void Hello_without_a_sender_is_told_to_update()
    {
        var message = TestingProtocol.Mismatch(new TestHelloRequest(TestFrameworkId.NUnit, Sender: null));

        Assert.StartsWith("DevTools.TestRunner is not compatible with DevTool add-in", message!, StringComparison.Ordinal);
        Assert.EndsWith(TestingProtocol.UpdateProductMessage, message!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Older_adapter_is_told_to_update_itself()
    {
        var message = TestingProtocol.Mismatch(Execute(Sender("0.1.4", TestingProtocol.MinVersion - 1, TestingProtocol.MinVersion - 1)));

        Assert.StartsWith("RevitDevTool.TestAdapter 0.1.4 is not compatible with DevTools.TestRunner", message!, StringComparison.Ordinal);
        Assert.EndsWith(TestingProtocol.UpdateAdapterMessage, message!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Adapter_without_a_sender_is_told_to_update_itself()
    {
        var message = TestingProtocol.Mismatch(Execute(sender: null));

        Assert.StartsWith("RevitDevTool.TestAdapter is not compatible with DevTools.TestRunner", message!, StringComparison.Ordinal);
        Assert.EndsWith(TestingProtocol.UpdateAdapterMessage, message!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Newer_adapter_tells_the_user_to_update_RevitDevTool()
    {
        var message = TestingProtocol.Mismatch(Execute(Sender("1.0.0", TestingProtocol.CurrentVersion + 2, TestingProtocol.CurrentVersion + 1)));

        Assert.StartsWith("RevitDevTool.TestAdapter 1.0.0 is not compatible with DevTools.TestRunner", message!, StringComparison.Ordinal);
        Assert.EndsWith(TestingProtocol.UpdateProductMessage, message!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Compatible_adapter_has_no_mismatch()
    {
        Assert.IsNull(TestingProtocol.Mismatch(Execute(Sender("1.0.0", TestingProtocol.CurrentVersion + 1, TestingProtocol.MinVersion))));
    }

    [TestMethod]
    public void Newer_runner_is_told_to_restart_the_host()
    {
        var hello = new TestHelloRequest(TestFrameworkId.NUnit, Sender("5.0.0", TestingProtocol.CurrentVersion + 2, TestingProtocol.CurrentVersion + 1));

        var response = TestingProtocol.CreateIncompatibleResponse("1", TestingProtocol.Mismatch(hello)!);

        Assert.AreEqual(TestingProtocol.IncompatibleCode, response.ErrorDetail!.Code);
        Assert.EndsWith(TestingProtocol.RestartHostMessage, response.ErrorMessage!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Execute_carries_this_process_as_the_sender()
    {
        var run = Execute(sender: null);
        var json = TestRunnerCli.SerializeExecute(run.Run, run.Host);
        var execute = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);

        Assert.AreEqual(TestingProtocol.Sender, execute!.Sender);
        Assert.AreEqual(AppVersion.Current, execute.Sender!.Version);
    }

    private static ProtocolSender Sender(string version, int current, int min) => new(version, new ProtocolRange(current, min));

    private static TestRunExecute Execute(ProtocolSender? sender) =>
        new(
            sender,
            new TestHostOptions("Revit", "2025", false, 60, 180),
            new TestRunRequest(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(@"C:\tests\Sample.dll"),
                TestSelection.All));
}
