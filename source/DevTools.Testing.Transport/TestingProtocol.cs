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
}

public sealed record TestingHelloRequest(int ProtocolVersion, string FrameworkId);

public sealed record TestingHelloResponse(
    int ProtocolVersion,
    string FrameworkId,
    string Host,
    string HostVersion,
    int ProcessId,
    bool IsBusy);

public sealed record TestingCancelRequest(Guid RunId);
