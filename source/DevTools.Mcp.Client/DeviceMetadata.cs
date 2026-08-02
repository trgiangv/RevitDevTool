using Microsoft.Win32;

namespace DevTools.Mcp.Client;

public sealed record DeviceMetadata(string MachineId, string MachineName)
{
    private const string MachineGuidRegistryPath = @"SOFTWARE\Microsoft\Cryptography";
    private const string MachineGuidValueName = "MachineGuid";

    public static DeviceMetadata Collect()
    {
        var machineId = GetMachineGuid();
        return new DeviceMetadata(machineId, Environment.MachineName);
    }

    private static string GetMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MachineGuidRegistryPath);
            return key?.GetValue(MachineGuidValueName)?.ToString() ?? Guid.NewGuid().ToString();
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }
}
