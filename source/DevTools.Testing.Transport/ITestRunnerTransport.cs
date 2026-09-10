using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public interface ITestRunnerTransport : IDisposable
{
    TestRunResponse Run(
        TestRunRequest request,
        TestHostOptions hostOptions,
        Action<TestEvent> onEvent);

    void Cancel(Guid runId);
}
