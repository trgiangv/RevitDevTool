using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public interface ITestRunnerTransport : IDisposable
{
    TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingEvent> onEvent);

    void Cancel(Guid runId);
}
