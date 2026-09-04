using DevTools.Mcp.Client;

namespace DevTools.Mcp.Client.Tests;

public sealed class DeviceMetadataTests
{
    [Fact]
    public void Collect_ReturnsNonEmptyMachineIdAndName()
    {
        var metadata = DeviceMetadata.Collect();

        Assert.False(string.IsNullOrWhiteSpace(metadata.MachineId));
        Assert.Equal(Environment.MachineName, metadata.MachineName);
    }
}
