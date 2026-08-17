using DevTools.NUnit.Transport.Contracts;

namespace DevTools.NUnit.Transport.Compatibility;

public static class ProtocolCompatibility
{
    public const string IncompatibleCode = "nunit/protocol_incompatible";

    /// <summary>Protocol version is major-only (integer major, no minor field in v1).</summary>
    public static bool IsCompatible(int protocolVersion) =>
        protocolVersion == NUnitProtocol.CurrentVersion;

    public static ProtocolCompatibilityError? Validate(int protocolVersion)
    {
        if (IsCompatible(protocolVersion))
            return null;

        return new ProtocolCompatibilityError(
            IncompatibleCode,
            CreateMessage(protocolVersion),
            protocolVersion,
            NUnitProtocol.CurrentVersion);
    }

    private static string CreateMessage(int protocolVersion) =>
        $"NUnit protocol version {protocolVersion} is not supported. Expected {NUnitProtocol.CurrentVersion}.";
}

public sealed record ProtocolCompatibilityError(
    string Code,
    string Message,
    int RequestedVersion,
    int ExpectedVersion);
