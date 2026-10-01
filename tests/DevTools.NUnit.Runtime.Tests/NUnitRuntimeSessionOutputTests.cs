using System.Diagnostics;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NUnitRuntimeSessionOutputTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
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
                TestContext.CancellationToken);

            var result = response.Results.Single();
            Assert.AreEqual(DedicatedTestFixturesHarness.OutputCaptureTestFullName, result.FullName);
            Assert.AreEqual(TestOutcomes.Passed, result.Outcome);

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

    [TestMethod]
    public void Run_reports_console_trace_and_debug_output_after_full_semantics_run()
    {
        using (var warmup = FixtureTestHarness.CreateSession())
        {
            _ = warmup.Run(
                CreateFullSemanticsRequest(null),
                new RecordingSink(),
                TestContext.CancellationToken);
        }

        var polluter = new RecordingTraceListener();
        Trace.Listeners.Insert(0, polluter);
        try
        {
            using var session = DedicatedTestFixturesHarness.CreateSession();
            var response = session.Run(
                CreateRequest(DedicatedTestFixturesHarness.OutputCaptureFilter),
                new RecordingSink(),
                TestContext.CancellationToken);

            var output = response.Results.Single().Output ?? string.Empty;
            Assert.Contains("spike-trace-marker", output, StringComparison.Ordinal);
            Assert.Contains("spike-debug-marker", output, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(polluter);
        }
    }

    [TestMethod]
    public void Run_keeps_trace_on_the_data_row_that_wrote_it()
    {
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var response = session.Run(
            CreateRequest(DedicatedTestFixturesHarness.TraceRowFilter),
            new RecordingSink(),
            TestContext.CancellationToken);

        Assert.AreEqual(2, response.Results.Count);
        foreach (var result in response.Results)
        {
            var output = result.Output ?? string.Empty;
            Assert.Contains("trace-row-", output, StringComparison.Ordinal);
            if (result.DisplayName.Contains("(1)", StringComparison.Ordinal)
                || result.FullName!.Contains("(1)", StringComparison.Ordinal))
            {
                Assert.Contains("trace-row-1", output, StringComparison.Ordinal);
                Assert.IsFalse(output.Contains("trace-row-2", StringComparison.Ordinal));
            }
            else
            {
                Assert.Contains("trace-row-2", output, StringComparison.Ordinal);
                Assert.IsFalse(output.Contains("trace-row-1", StringComparison.Ordinal));
            }
        }
    }

    [TestMethod]
    public void Run_reports_a_case_source_that_throws_on_enumeration_as_not_runnable()
    {
        using var session = DedicatedTestFixturesHarness.CreateSession();
        var response = session.Run(
            CreateRequest(DedicatedTestFixturesHarness.NullDocumentCaseSourceFilter),
            new RecordingSink(),
            TestContext.CancellationToken);

        Assert.IsNotEmpty(response.Results);
        foreach (var result in response.Results)
        {
            Assert.AreEqual(
                TestOutcomes.Error,
                result.Outcome,
                result.Message + Environment.NewLine + result.StackTrace);
        }
    }

    private static TestRunRequest CreateRequest(string? filter) => new(
        1,
        Guid.NewGuid(),
        TestFrameworkId.NUnit,
        new TestAssemblyReference(DedicatedTestFixturesHarness.AssemblyPath),
        SelectionFromFilter(filter));

    private static TestRunRequest CreateFullSemanticsRequest(string? filter) => new(
        1,
        Guid.NewGuid(),
        TestFrameworkId.NUnit,
        new TestAssemblyReference(FixtureTestHarness.FixtureAssemblyPath),
        SelectionFromFilter(filter));

    private static TestSelection SelectionFromFilter(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
            ? TestSelection.All
            : TestSelection.FromFrameworkFilter("filter-xml", filter);

    private sealed class RecordingSink : ITestEventSink
    {
        public void Publish(TestEvent testingEvent) { }
    }

    private sealed class RecordingTraceListener : TraceListener
    {
        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }
    }
}
