using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace DevTools.Daemon.Desktop.Models;

public sealed class UserSettingsStore(IOptionsMonitor<UserSettings> monitor, IConfiguration configuration)
{
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
                    UserSettingsJsonContext.Default.DictionaryStringUserSettings));
            (configuration as IConfigurationRoot)?.Reload();
        }
        catch
        {
            /* ignored */
        }
    }
}
