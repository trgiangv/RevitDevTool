using DevTools.Utilities;

namespace DevTools.Telemetry;

/// <summary>
/// Stable anonymous id for this machine/install (not Windows username).
/// </summary>
public static class InstallationId
{
    private const string FileName = "installation_id.txt";
    private static string? _cached;

    public static string GetOrCreate()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var dir = AppUtils.GetApplicationDataPath();
        var path = Path.Combine(dir, FileName);
        try
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (Guid.TryParse(existing, out _))
                {
                    return _cached = existing;
                }
            }

            var id = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, id);
            return _cached = id;
        }
        catch
        {
            // Fall back to volatile id if disk is not writable (telemetry still works per process).
            return _cached = Guid.NewGuid().ToString("N");
        }
    }
}
