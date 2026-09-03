using System.Text.Json;
using System.Text.Json.Serialization;
using DevTools.Settings.Configs;
using DevTools.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DevTools.Daemon.Desktop;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class UserSettings
{
    public const string SectionName = "User";
    public const string FileName = "settings.json";

    public static string FilePath =>
        Path.Combine(AppUtils.GetApplicationDataPath(), FileName);

    public AppTheme Theme { get; set; } = AppTheme.Auto;
    public bool AutoStartEnabled { get; set; }
}

public sealed class UserSettingsStore(IOptionsMonitor<UserSettings> monitor, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public UserSettings Current => monitor.CurrentValue;

    public void Update(Action<UserSettings> apply)
    {
        var next = new UserSettings
        {
            Theme = Current.Theme,
            AutoStartEnabled = Current.AutoStartEnabled
        };
        apply(next);

        try
        {
            File.WriteAllText(
                UserSettings.FilePath,
                JsonSerializer.Serialize(
                    new Dictionary<string, UserSettings> { [UserSettings.SectionName] = next },
                    JsonOptions));
            (configuration as IConfigurationRoot)?.Reload();
        }
        catch
        {
            /* ignored */
        }
    }
}
