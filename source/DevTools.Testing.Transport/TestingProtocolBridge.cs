using System.Text.Json;
using DevTools.Ipc;

namespace DevTools.Testing.Transport;

public static class TestingProtocolBridge
{
    public static bool IsCompatible(int protocolVersion) =>
        TestingProtocol.IsCompatible(protocolVersion);

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
        TestingProtocol.CreateUnsupportedMessage(protocolVersion);
}
