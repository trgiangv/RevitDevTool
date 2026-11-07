using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ForeignContextTests
{
    private const string AsyncFilter =
        "<filter><test>DevTools.NUnit.Runtime.Fixtures.AsyncLifecycleFixture.AsyncLifecycle_TestCompletes</test></filter>";

    [TestMethod]
    public void Async_test_ends_on_foreign_context()
    {
        FixtureTestHarness.ResetAcceptanceLog();
        var foreign = new NeverPumpedContext();
        TestRunResponse? response = null;
        SynchronizationContext? after = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(foreign);
            using var session = new NUnitRuntimeSession(
                FixtureTestHarness.LoadFixtureAssembly(),
                FixtureTestHarness.FixtureAssemblyPath,
                FixtureTestHarness.GenerationId,
                runOnCallingThread: true);
            response = session.Run(
                new TestRunRequest(
                    Guid.NewGuid(),
                    TestFrameworkId.NUnit,
                    new TestAssemblyReference(FixtureTestHarness.FixtureAssemblyPath),
                    TestSelection.FromFrameworkFilter("filter-xml", AsyncFilter)),
                new NoOpSink(),
                CancellationToken.None);
            after = SynchronizationContext.Current;
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "The run blocked the thread the test had to return to.");
        Assert.AreEqual(TestOutcomes.Passed, response!.Results.Single().Outcome);
        Assert.AreSame(foreign, after);
    }

    [TestMethod]
    public void Pumpable_contexts_stay()
    {
        Assert.IsTrue(ForeignContextScope.CanPump(null));
        Assert.IsTrue(ForeignContextScope.CanPump(new SynchronizationContext()));
        Assert.IsFalse(ForeignContextScope.CanPump(new NeverPumpedContext()));
    }

    private sealed class NeverPumpedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }

    private sealed class NoOpSink : ITestEventSink
    {
        public void Publish(TestEvent testingEvent)
        {
        }
    }
}
