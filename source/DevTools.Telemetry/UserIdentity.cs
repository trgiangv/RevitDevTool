using Microsoft.Win32;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace DevTools.Telemetry;

/// <summary>
/// Anonymous id for one Windows user on one machine: a keyed one-way hash of the machine GUID and the account SID.
/// Falls back to a random id for this process when either value is not available. Nothing is stored on disk.
/// </summary>
public static class UserIdentity
{
    private static readonly byte[] HashKey = Encoding.UTF8.GetBytes("RevitDevTool.Telemetry.UserIdentity.v1");
    private static string? _cached;

    public static string Get()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var cryptography = hive.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var machineGuid = cryptography?.GetValue("MachineGuid") as string;

            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value;

            if (!string.IsNullOrWhiteSpace(machineGuid) && !string.IsNullOrWhiteSpace(sid))
            {
                return _cached = Hash($"{machineGuid!.Trim().ToLowerInvariant()}|{sid}");
            }
        }
        catch
        {
        }

        return _cached = Guid.NewGuid().ToString("N");
    }

    internal static string Hash(string value)
    {
        using var hmac = new HMACSHA256(HashKey);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(32);
        for (var i = 0; i < 16; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }
}
