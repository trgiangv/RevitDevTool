using System.Text.Json;
using DevTools.UI.Theme;
using DevTools.Utilities;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Persisted user preferences for the Daemon. Stored as JSON in AppData.
/// </summary>
[UsedImplicitly]
public sealed class DaemonSettings
{
    private const string SettingsFileName = "daemon-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppTheme Theme { get; set; } = AppTheme.Auto;
    public bool AutoStartEnabled { get; set; }

    private static string FilePath =>
        Path.Combine(AppUtils.GetApplicationDataPath(), SettingsFileName);

    public static DaemonSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new DaemonSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<DaemonSettings>(json, JsonOptions) ?? new DaemonSettings();
        }
        catch
        {
            return new DaemonSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // ignored
        }
    }
}
