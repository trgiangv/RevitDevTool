using DevTools.Telemetry;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class InstallationIdTests
{
    [TestMethod]
    public void GetOrCreate_returns_stable_guid_on_repeat_calls()
    {
        var first = InstallationId.GetOrCreate();
        var second = InstallationId.GetOrCreate();
        Assert.AreEqual(first, second);
        Assert.IsTrue(Guid.TryParse(first, out _));
    }
}
