using Microsoft.Win32;

namespace DevTools.Daemon.Hosting;

public sealed record DeviceMetadata(string MachineId, string MachineName)
{
    private const string MachineGuidRegistryPath = @"SOFTWARE\Microsoft\Cryptography";
    private const string MachineGuidValueName = "MachineGuid";

    public static DeviceMetadata Collect()
    {
        var machineId = GetMachineGuid();
        var machineName = Environment.MachineName;
        return new DeviceMetadata(machineId, machineName);
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
