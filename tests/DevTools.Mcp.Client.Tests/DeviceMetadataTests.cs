using System.IO;
using DevTools.Daemon.Mcp.Processes;

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

    [TestMethod]
    public void LoadOrCreatePersistentMachineId_IsStableAcrossCalls()
    {
        var fallbackPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RevitDevTool",
            "machine-id");
        var previous = File.Exists(fallbackPath) ? File.ReadAllText(fallbackPath) : null;
        try
        {
            if (File.Exists(fallbackPath))
                File.Delete(fallbackPath);

            var first = DeviceMetadata.LoadOrCreatePersistentMachineId();
            var second = DeviceMetadata.LoadOrCreatePersistentMachineId();

            Assert.AreEqual(first, second);
            Assert.AreEqual(first, File.ReadAllText(fallbackPath).Trim());
        }
        finally
        {
            if (previous is not null)
                File.WriteAllText(fallbackPath, previous);
            else if (File.Exists(fallbackPath))
                File.Delete(fallbackPath);
        }
    }
}
