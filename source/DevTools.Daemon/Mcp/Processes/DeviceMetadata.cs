using System.IO;
using Microsoft.Win32;

namespace DevTools.Daemon.Mcp.Processes;

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
            var registryId = key?.GetValue(MachineGuidValueName)?.ToString();
            if (!string.IsNullOrWhiteSpace(registryId))
                return registryId;
        }
        catch
        {
            // fall through to persistent fallback
        }

        // Stable fallback: %LOCALAPPDATA%\RevitDevTool\machine-id (survives Daemon restarts).
        return LoadOrCreatePersistentMachineId();
    }

    internal static string LoadOrCreatePersistentMachineId()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RevitDevTool",
            "machine-id");
        try
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(existing))
                    return existing;
            }

            var created = Guid.NewGuid().ToString("D");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, created);
            return created;
        }
        catch
        {
            return Guid.NewGuid().ToString("D");
        }
    }
}
