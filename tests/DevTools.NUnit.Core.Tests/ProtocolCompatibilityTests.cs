using DevTools.NUnit.Core.Compatibility;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Transport;

namespace DevTools.NUnit.Core.Tests;

public sealed class ProtocolCompatibilityTests
{
    [Fact]
    public void CurrentVersion_IsCompatible()
    {
        Assert.True(ProtocolCompatibility.IsCompatible(NUnitProtocol.CurrentVersion));
        Assert.Null(ProtocolCompatibility.Validate(NUnitProtocol.CurrentVersion));
    }

    [Fact]
    public void ProtocolV1_IsRejected()
    {
        var error = ProtocolCompatibility.Validate(1);

        Assert.NotNull(error);
        Assert.Equal(ProtocolCompatibility.IncompatibleCode, error.Code);
        Assert.Equal(
            "NUnit protocol version 1 is not supported. Expected 2.",
            error.Message);
        Assert.Equal(1, error.RequestedVersion);
        Assert.Equal(2, error.ExpectedVersion);
        Assert.False(ProtocolCompatibility.IsCompatible(1));
    }

    [Fact]
    public void MismatchedVersion_ReturnsDeterministicError()
    {
        var error = ProtocolCompatibility.Validate(99);

        Assert.NotNull(error);
        Assert.Equal(ProtocolCompatibility.IncompatibleCode, error.Code);
        Assert.Equal(
            "NUnit protocol version 99 is not supported. Expected 2.",
            error.Message);
        Assert.Equal(99, error.RequestedVersion);
        Assert.Equal(NUnitProtocol.CurrentVersion, error.ExpectedVersion);
    }

    [Fact]
    public void CreateErrorResponse_UsesBridgeEnvelope()
    {
        var response = NUnitProtocolBridge.CreateIncompatibleResponse("req-1", 1);

        Assert.Equal(BridgeMessage.TypeResponse, response.Type);
        Assert.Equal("req-1", response.Id);
        Assert.True(response.IsError);
        Assert.Equal(ProtocolCompatibility.IncompatibleCode, response.ErrorDetail?.Code);
        Assert.Equal(
            "NUnit protocol version 1 is not supported. Expected 2.",
            response.ErrorMessage);
        Assert.NotNull(response.ErrorDetail?.Data);
        Assert.Equal(1, response.ErrorDetail.Data.Value.GetProperty("requested").GetInt32());
        Assert.Equal(2, response.ErrorDetail.Data.Value.GetProperty("expected").GetInt32());
    }
}
