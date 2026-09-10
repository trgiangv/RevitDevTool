using System.Text.Json;
using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Transport;

public static class TestingProtocol
{
    public const int CurrentVersion = 2;

    public const string Hello = "testing/hello";
    public const string Run = "testing/run";
    public const string Cancel = "testing/cancel";
    public const string Progress = "testing/progress";

    public const string IncompatibleCode = "testing/protocol_incompatible";

    public static bool IsCompatible(int protocolVersion) =>
        protocolVersion == CurrentVersion;

    public static string CreateUnsupportedMessage(int protocolVersion) =>
        $"Testing protocol version {protocolVersion} is not supported. Expected {CurrentVersion}.";

    public static BridgeMessage CreateIncompatibleResponse(string requestId, int protocolVersion) =>
        BridgeMessage.Error(
            requestId,
            IncompatibleCode,
            CreateUnsupportedMessage(protocolVersion),
            JsonSerializer.SerializeToElement(new
            {
                requested = protocolVersion,
                expected = CurrentVersion,
            }));
}

public sealed record TestHelloRequest(int ProtocolVersion, TestFrameworkId FrameworkId);

public sealed record TestHelloResponse(
    int ProtocolVersion,
    TestFrameworkId FrameworkId,
    string Host,
    string HostVersion,
    int ProcessId,
    bool IsBusy);

public sealed record TestCancelRequest(Guid RunId);

public sealed record TestCancelResponse(bool Acknowledged);

/// <summary>
/// One NDJSON line on TestRunner <c>run</c> stdout (event lines, then the
/// <see cref="TestRunResponse"/>).
/// </summary>
public sealed record TestRunnerStreamMessage(
    TestEvent? Event = null,
    TestRunResponse? Response = null);

public static class TestCancelSignal
{
    public static string Name(Guid runId) => $@"Local\DevTools.TestRunner.Cancel.{runId:N}";

    public static EventWaitHandle Create(Guid runId) =>
        new(false, EventResetMode.ManualReset, Name(runId));

    public static bool TrySignal(Guid runId)
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(Name(runId));
            return handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }
}
