using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public static class TestRunnerCli
{
    public const string RunCommand = "run";

    public static string SerializeExecute(TestRunRequest request, TestHostOptions hostOptions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hostOptions);
        return JsonSerializer.Serialize(
            new TestRunExecute(TestingProtocol.CurrentVersion, hostOptions, request),
            TestingJsonContext.Default.TestRunExecute);
    }
}
