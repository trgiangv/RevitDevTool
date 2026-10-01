using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Abstractions.Providers;

public interface ITestFrameworkProvider
{
    TestFrameworkId FrameworkId { get; }

    TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken);

    bool Cancel(Guid runId);
}
