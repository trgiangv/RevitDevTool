using Microsoft.Testing.Platform.Services;

namespace DevTools.TUnit.Runtime.Tests;

#pragma warning disable TPEXP

[TestClass]
public sealed class TUnitEngineHostTests
{
    [TestMethod]
    public void Client_info_is_mtp_2_4_non_stateful()
    {
        IClientInfo info = new TUnitEngineClientInfo();

        Assert.AreEqual("devtools-revit-host", info.Id);
        Assert.AreEqual("1.0", info.Version);
        Assert.IsFalse(info.Capabilities.IsStateful);
    }
}
