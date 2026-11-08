using DevTools.Hosting;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class SentryTelemetryTests
{
    private const string DummyDsn = "https://publickey@127.0.0.1/1";
    private const string Version = "1.0.0-test";

    [TestMethod]
    public void Constructor_validates_arguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SentryTelemetry(null!, Version, DummyDsn));
        Assert.ThrowsExactly<ArgumentException>(() => new SentryTelemetry(new FakeHostAppInfo(), "  ", DummyDsn));
        Assert.ThrowsExactly<ArgumentException>(() => new SentryTelemetry(new FakeHostAppInfo(), Version, "  "));
    }

    [TestMethod]
    public void Internal_errors_sent_once_each()
    {
        var transport = new CapturingTransport();
        using (var telemetry = CreateTelemetry(transport))
        {
            telemetry.StartOperation(TelemetryKeys.Operation.Execution, "  ").Complete();
            telemetry.StartOperation(TelemetryKeys.Operation.Execution, "CSharp").Fail();
            telemetry.StartOperation(TelemetryKeys.Operation.Execution, "Python").Fail(Throw(new OperationCanceledException()));
            telemetry.StartOperation(TelemetryKeys.Operation.Execution, "IronPython")
                .Fail(TelemetryReporter.MarkExternal(Throw(new InvalidOperationException("user script"))));
            telemetry.StartOperation(TelemetryKeys.Operation.Mcp, "BuiltIn").Fail(new InvalidOperationException("never thrown"));
            telemetry.StartOperation(TelemetryKeys.Operation.Mcp, "Python").Fail(Throw(new InvalidOperationException("mcp")));

            telemetry.ReportError(Throw(new InvalidOperationException("render")), TelemetryKeys.Operation.Visualization, "Mesh");
            telemetry.ReportError(Throw(new InvalidOperationException("render again")), TelemetryKeys.Operation.Visualization, "Mesh");
        }

        Assert.AreSequenceEqual(
            ["mcp", "render"],
            transport.Events.Select(e => e.GetProperty("exception").GetProperty("values")[0].GetProperty("value").GetString()!)
                .Order()
                .ToArray());
    }

    [TestMethod]
    public void Sent_error_is_scrubbed()
    {
        var transport = new CapturingTransport();
        using (var telemetry = CreateTelemetry(transport))
        {
            telemetry.StartOperation(TelemetryKeys.Operation.Mcp, "BuiltIn")
                .Fail(Throw(new InvalidOperationException(@"Cannot read C:\Users\tester\Projects\Tower.rvt")));
        }

        var evt = transport.Events.Single();
        var value = evt.GetProperty("exception").GetProperty("values")[0].GetProperty("value").GetString()!;
        Assert.DoesNotContain(@"\Users\tester", value, StringComparison.Ordinal);
        Assert.Contains("[path]", value, StringComparison.Ordinal);
        Assert.IsFalse(evt.TryGetProperty("server_name", out _));
        Assert.AreEqual("test", evt.GetProperty("environment").GetString());
        Assert.AreEqual("tester", evt.GetProperty("user").GetProperty("id").GetString());
        Assert.AreEqual(TelemetryKeys.Operation.Mcp, evt.GetProperty("tags").GetProperty(TelemetryKeys.Tag.Operation).GetString());
        Assert.AreEqual("BuiltIn", evt.GetProperty("tags").GetProperty(TelemetryKeys.Tag.Provider).GetString());
    }

    private static SentryTelemetry CreateTelemetry(ITransport transport) =>
        new(new FakeHostAppInfo { VersionBuild = "26.0" }, Version, DummyDsn, "test", "tester", transport);

    private static T Throw<T>(T exception) where T : Exception
    {
        try
        {
            throw exception;
        }
        catch (T ex)
        {
            return ex;
        }
    }

    private sealed class CapturingTransport : ITransport
    {
        private readonly ConcurrentQueue<JsonElement> _events = new();

        public JsonElement[] Events => _events.ToArray();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            foreach (var item in envelope.Items.Where(item => item.TryGetType() == "event"))
            {
                using var stream = new MemoryStream();
                await item.Payload.SerializeAsync(stream, null, cancellationToken);
                using var document = JsonDocument.Parse(stream.ToArray());
                _events.Enqueue(document.RootElement.Clone());
            }
        }
    }

    internal sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host { get; init; } = HostApp.Revit;
        public string VersionNumber { get; init; } = "2025";
        public string? VersionBuild { get; init; }
        public int ProcessId { get; init; } = 42;
    }
}
