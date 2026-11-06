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

    [TestMethod]
    public void Hello_without_a_testing_runtime_range_still_reads()
    {
        var hello = JsonSerializer.Deserialize(
            """{"framework_id":"NUnit","host":"Revit","host_version":"2025","process_id":1,"is_busy":false,"sender":{"version":"4.1.0","protocol":{"current":1,"min":1}}}""",
            TestingJsonContext.Default.TestHelloResponse);

        Assert.IsNull(hello!.TestingRuntime);
        Assert.IsNull(TestingProtocol.Mismatch(hello));
    }

    [TestMethod]
    public void Hello_round_trips_the_testing_runtime_range()
    {
        var json = JsonSerializer.Serialize(Hello(TestingProtocol.RuntimeRange), TestingJsonContext.Default.TestHelloResponse);

        Assert.Contains("\"testing_runtime\":{", json, StringComparison.Ordinal);
        Assert.AreEqual(
            TestingProtocol.RuntimeRange,
            JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestHelloResponse)!.TestingRuntime);
    }

    [TestMethod]
    public void Runner_has_nothing_to_say_when_the_output_has_no_manifest()
    {
        using var output = new OutputDirectory();

        Assert.IsNull(TestingProtocol.RuntimeMismatch(Hello(range: null), output.TestAssembly, TestFrameworkId.NUnit));
    }

    [TestMethod]
    public void Runner_accepts_an_overlapping_host_range()
    {
        using var output = new OutputDirectory();
        output.WriteManifest(TestingProtocol.RuntimeRange);

        Assert.IsNull(TestingProtocol.RuntimeMismatch(Hello(TestingProtocol.RuntimeRange), output.TestAssembly, TestFrameworkId.NUnit));
    }

    [TestMethod]
    public void Runner_reads_a_missing_host_range_as_too_old()
    {
        using var output = new OutputDirectory();
        output.WriteManifest(TestingProtocol.RuntimeRange);

        Assert.AreEqual(
            $"RevitDevTool 4.1.0 is not compatible with RevitDevTool.TestAdapter unknown. {TestingProtocol.UpdateProductMessage}",
            TestingProtocol.RuntimeMismatch(Hello(range: null), output.TestAssembly, TestFrameworkId.NUnit));
    }

    [TestMethod]
    public void Runner_tells_an_older_adapter_to_update_itself()
    {
        using var output = new OutputDirectory();
        output.WriteManifest(new ProtocolRange(1, 1));
        var host = new ProtocolRange(TestingProtocol.RuntimeRange.Current + 2, TestingProtocol.RuntimeRange.Current + 2);

        Assert.AreEqual(
            $"RevitDevTool 4.1.0 is not compatible with RevitDevTool.TestAdapter unknown. {TestingProtocol.UpdateAdapterMessage}",
            TestingProtocol.RuntimeMismatch(Hello(host), output.TestAssembly, TestFrameworkId.NUnit));
    }

    [TestMethod]
    public void Manifest_reader_ignores_unknown_members_and_rejects_an_incomplete_file()
    {
        using var output = new OutputDirectory();
        var path = Path.Combine(output.Path, RuntimeManifest.FileName(TestFrameworkId.NUnit));

        File.WriteAllText(path, """{"framework":"NUnit","entry":"E","contract":{"current":1,"min":1},"assembly":"nunit.framework","version":"5.0.0.0","future":true}""");
        Assert.AreEqual("5.0.0.0", RuntimeManifest.TryRead(output.Path, TestFrameworkId.NUnit)!.Version);

        foreach (var incomplete in new[]
                 {
                     """{"framework":"NUnit","contract":{"current":1,"min":1},"assembly":"nunit.framework","version":"5.0.0.0"}""",
                     """{"framework":"NUnit","entry":"E","assembly":"nunit.framework","version":"5.0.0.0"}""",
                     """{"framework":"NUnit","entry":"E","contract":{"current":1,"min":1},"version":"5.0.0.0"}""",
                     """{"framework":"NUnit","entry":"E","contract":{"current":1,"min":1},"assembly":"nunit.framework"}""",
                     """{"framework":"NUnit","entry":"E","contract":{"current":1,"min":1},"assembly":"nunit.framework","version":"five"}""",
                     "not json",
                 })
        {
            File.WriteAllText(path, incomplete);
            Assert.IsNull(RuntimeManifest.TryRead(output.Path, TestFrameworkId.NUnit), incomplete);
        }
    }

    private static TestHelloResponse Hello(ProtocolRange? range) =>
        new(TestFrameworkId.NUnit, "Revit", "2025", 1, false, Sender("4.1.0", 1, 1), range);

    private sealed class OutputDirectory : IDisposable
    {
        public OutputDirectory() => Directory.CreateDirectory(Path);

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"runtime-manifest-{Guid.NewGuid():N}");

        public string TestAssembly => System.IO.Path.Combine(Path, "Consumer.dll");

        public void WriteManifest(ProtocolRange contract) =>
            File.WriteAllText(
                System.IO.Path.Combine(Path, RuntimeManifest.FileName(TestFrameworkId.NUnit)),
                JsonSerializer.Serialize(
                    new RuntimeManifest("NUnit", "Entry", contract, "nunit.framework", "5.0.0.0"),
                    TestingJsonContext.Default.RuntimeManifest));

        public void Dispose() => Directory.Delete(Path, recursive: true);
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
