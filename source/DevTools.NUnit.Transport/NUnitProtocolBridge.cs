using System.Text.Json;
using DevTools.Ipc;
using DevTools.NUnit.Core.Compatibility;

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
                expected = Core.Contracts.NUnitProtocol.CurrentVersion,
            }));
}
