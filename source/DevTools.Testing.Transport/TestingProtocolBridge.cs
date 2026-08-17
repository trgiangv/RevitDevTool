using System.Text.Json;
using DevTools.Ipc;

namespace DevTools.Testing.Transport;

public static class TestingProtocolBridge
{
    public static bool IsCompatible(int protocolVersion) =>
        protocolVersion == TestingProtocol.CurrentVersion;

    public static BridgeMessage CreateIncompatibleResponse(string requestId, int protocolVersion) =>
        BridgeMessage.Error(
            requestId,
            TestingProtocol.IncompatibleCode,
            CreateMessage(protocolVersion),
            JsonSerializer.SerializeToElement(new
            {
                requested = protocolVersion,
                expected = TestingProtocol.CurrentVersion,
            }));

    public static string CreateMessage(int protocolVersion) =>
        $"Testing protocol version {protocolVersion} is not supported. Expected {TestingProtocol.CurrentVersion}.";
}
