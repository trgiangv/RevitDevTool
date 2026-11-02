using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Utilities;

namespace DevTools.Testing.Transport;

public static class TestingProtocol
{
    public const int CurrentVersion = 1;
    public const int MinVersion = 1;

    public const string Hello = "testing/hello";
    public const string Run = "testing/run";
    public const string Cancel = "testing/cancel";
    public const string Progress = "testing/progress";

    public const string IncompatibleCode = TestingErrorCodes.ProtocolIncompatible;

    public const string UpdateAdapterMessage = "Update RevitDevTool.TestAdapter to the latest version.";
    public const string UpdateProductMessage = "Update RevitDevTool to the latest version.";
    public const string RestartHostMessage = "Restart the host app to load the latest RevitDevTool.";

    public static ProtocolRange Range { get; } = new(CurrentVersion, MinVersion);

    public static ProtocolSender Sender { get; } = new(AppVersion.Current, Range);

    private static readonly ProtocolPeer Runner = RunnerPeer(Sender);
    private static readonly ProtocolPeer Host = HostPeer(Sender);

    public static string? Mismatch(TestRunExecute execute) =>
        Mismatch(Runner, new ProtocolPeer("RevitDevTool.TestAdapter", execute.Sender, UpdateAdapterMessage));

    public static string? Mismatch(TestHelloRequest hello) => Mismatch(Host, RunnerPeer(hello.Sender));

    public static string? Mismatch(TestHelloResponse hello) => Mismatch(Runner, HostPeer(hello.Sender));

    public static BridgeMessage CreateIncompatibleResponse(string requestId, string mismatch) =>
        BridgeMessage.Error(requestId, IncompatibleCode, mismatch);

    private static string? Mismatch(ProtocolPeer self, ProtocolPeer other) =>
        self.IsCompatible(other) ? null : self.ExplainMismatch(other);

    private static ProtocolPeer RunnerPeer(ProtocolSender? sender) =>
        new("DevTools.TestRunner", sender, UpdateProductMessage);

    private static ProtocolPeer HostPeer(ProtocolSender? sender) =>
        new("DevTool add-in", sender, RestartHostMessage);
}

public sealed record TestHelloRequest(TestFrameworkId FrameworkId, ProtocolSender? Sender);

public sealed record TestHelloResponse(
    TestFrameworkId FrameworkId,
    string Host,
    string HostVersion,
    int ProcessId,
    bool IsBusy,
    ProtocolSender? Sender);

/// <summary>
/// Adapter → TestRunner execute envelope. Nested <see cref="Run"/> is the
/// same <see cref="TestRunRequest"/> the host receives on <c>testing/run</c>.
/// </summary>
public sealed record TestRunExecute(ProtocolSender? Sender, TestHostOptions Host, TestRunRequest Run);

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
