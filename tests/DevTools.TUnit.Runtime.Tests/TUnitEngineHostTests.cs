using Microsoft.Testing.Platform.Services;

namespace DevTools.TUnit.Runtime.Tests;

#pragma warning disable TPEXP

public sealed class TUnitEngineHostTests
{
    [Fact]
    public void Client_info_is_mtp_2_4_non_stateful()
    {
        IClientInfo info = new TUnitEngineClientInfo();

        Assert.Equal("devtools-revit-host", info.Id);
        Assert.Equal("1.0", info.Version);
        Assert.False(info.Capabilities.IsStateful);
    }
}
