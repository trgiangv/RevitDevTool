using DevTools.Mcp.Client;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class DeviceMetadataTests
{
    [TestMethod]
    public void Collect_ReturnsNonEmptyMachineIdAndName()
    {
        var metadata = DeviceMetadata.Collect();

        Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.MachineId));
        Assert.AreEqual(Environment.MachineName, metadata.MachineName);
    }
}
