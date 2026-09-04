using System.Diagnostics;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[Collection(nameof(BlockingFixtureCollection))]
public sealed class NUnitRuntimeSessionOutputTests
{
    [Fact]
    public void Run_reports_console_trace_and_debug_output()
    {
        var polluter = new RecordingTraceListener();
        Trace.Listeners.Insert(0, polluter);
        try
        {
            using var session = DedicatedTestFixturesHarness.CreateSession();
            var response = session.Run(
                CreateRequest(DedicatedTestFixturesHarness.OutputCaptureFilter),
                new RecordingSink(),
                TestContext.Current.CancellationToken);

            var result = Assert.Single(response.Results);
            Assert.Equal(DedicatedTestFixturesHarness.OutputCaptureTestFullName, result.FullName);
            Assert.Equal(TestingOutcomes.Passed, result.Outcome);

            var output = result.Output ?? string.Empty;
            Assert.Contains("spike-output-marker", output, StringComparison.Ordinal);
            Assert.Contains("spike-trace-marker", output, StringComparison.Ordinal);
            Assert.Contains("spike-debug-marker", output, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(polluter);
        }
    }

    [Fact]
    public void Run_reports_console_trace_and_debug_output_after_full_semantics_run()
    {
        using (var warmup = FixtureTestHarness.CreateSession())
        {
            _ = warmup.Run(
                CreateFullSemanticsRequest(null),
                new RecordingSink(),
                TestContext.Current.CancellationToken);
        }

        var polluter = new RecordingTraceListener();
        Trace.Listeners.Insert(0, polluter);
        try
        {
            using var session = DedicatedTestFixturesHarness.CreateSession();
            var response = session.Run(
                CreateRequest(DedicatedTestFixturesHarness.OutputCaptureFilter),
                new RecordingSink(),
                TestContext.Current.CancellationToken);

            var output = Assert.Single(response.Results).Output ?? string.Empty;
            Assert.Contains("spike-trace-marker", output, StringComparison.Ordinal);
            Assert.Contains("spike-debug-marker", output, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(polluter);
        }
    }

    private static TestingRunRequest CreateRequest(string? filter) => new(
        1,
        Guid.NewGuid(),
        "nunit",
        new TestingAssemblyReference(DedicatedTestFixturesHarness.AssemblyPath, "net10.0-windows", null),
        new TestingSelection([], filter),
        new Dictionary<string, string>());

    private static TestingRunRequest CreateFullSemanticsRequest(string? filter) => new(
        1,
        Guid.NewGuid(),
        "nunit",
        new TestingAssemblyReference(FixtureTestHarness.FixtureAssemblyPath, "net10.0-windows", null),
        new TestingSelection([], filter),
        new Dictionary<string, string>());

    private sealed class RecordingSink : ITestingRuntimeEventSink
    {
        public void Publish(TestingRuntimeEvent testingEvent) { }
    }

    private sealed class RecordingTraceListener : TraceListener
    {
        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }
    }
}
