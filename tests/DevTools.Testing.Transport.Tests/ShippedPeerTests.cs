using DevTools.Testing.Abstractions.Contracts;
using System.Text.Json;

namespace DevTools.Testing.Transport.Tests;

/// <summary>
/// Frozen messages of shipped peers (TestAdapter 0.1.4, TestRunner 4.0.0-beta.4). They carry no sender, so they
/// are rejected with a sentence that names the update (ADR 0055 §3).
/// </summary>
[TestClass]
public sealed class ShippedPeerTests
{
    private const string ShippedHello = """{"protocol_version":2,"framework_id":"NUnit"}""";

    private const string ShippedExecute = """
        {"protocol_version":2,
         "host":{"host_name":"Revit","host_version":"2025","force_launch":false,"per_test_timeout_seconds":60,"launch_timeout_seconds":180},
         "run":{"protocol_version":2,"run_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","framework_id":"NUnit",
                "assembly":{"path":"C:\\tests\\Sample.dll"},"selection":{"kind":"All","test_ids":[],"names":[]}}}
        """;

    [TestMethod]
    public void Shipped_runner_hello_is_told_to_update_RevitDevTool()
    {
        var hello = JsonSerializer.Deserialize(ShippedHello, TestingJsonContext.Default.TestHelloRequest)!;

        var message = TestingProtocol.Mismatch(hello);

        Assert.AreEqual(
            $"DevTools.TestRunner is not compatible with DevTool add-in {TestingProtocol.Sender.Version}. {TestingProtocol.UpdateProductMessage}",
            message);
    }

    [TestMethod]
    public void Shipped_adapter_execute_is_told_to_update_the_adapter()
    {
        var execute = JsonSerializer.Deserialize(ShippedExecute, TestingJsonContext.Default.TestRunExecute)!;

        var message = TestingProtocol.Mismatch(execute);

        Assert.AreEqual(
            $"RevitDevTool.TestAdapter is not compatible with DevTools.TestRunner {TestingProtocol.Sender.Version}. {TestingProtocol.UpdateAdapterMessage}",
            message);
    }
}
