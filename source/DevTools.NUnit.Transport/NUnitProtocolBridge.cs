using System.Text.Json;
using DevTools.Ipc;
using DevTools.NUnit.Transport.Compatibility;

namespace DevTools.NUnit.Transport;

public static class NUnitProtocolBridge
{
    public static BridgeMessage CreateIncompatibleResponse(string requestId, int protocolVersion) =>
        BridgeMessage.Error(
            requestId,
            ProtocolCompatibility.IncompatibleCode,
            ProtocolCompatibility.Validate(protocolVersion)!.Message,
            JsonSerializer.SerializeToElement(new
            {
                requested = protocolVersion,
                expected = Contracts.NUnitProtocol.CurrentVersion,
            }));
}
