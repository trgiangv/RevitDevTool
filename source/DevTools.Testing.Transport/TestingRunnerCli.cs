using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public static class TestingRunnerCli
{
    public const string MachineRunCommand = "machine-run";

    public static string SerializeInvocation(TestingRunRequest request, TestingHostOptions hostOptions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hostOptions);
        var wireHost = hostOptions with { FrameworkId = null, RunnerPath = null };
        return JsonSerializer.Serialize(
            new TestingRunInvocation(TestingProtocol.CurrentVersion, wireHost, request),
            TestingJsonContext.Default.TestingRunInvocation);
    }
}
