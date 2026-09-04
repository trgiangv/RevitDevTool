using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop;
using DevTools.Settings.Configs;

namespace DevTools.Daemon.Tests;

public sealed class ControlJsonTests
{
    [Fact]
    public void Options_RoundtripTokenAndControlPayloads()
    {
        var token = new TokenData { AccessToken = "a", RefreshToken = "r", ExpiresAt = 1 };
        var json = JsonSerializer.Serialize(token, ControlJsonContext.Default.TokenData);
        var loaded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.TokenData);
        Assert.Equal("a", loaded?.AccessToken);

        Assert.Contains("isRunning", JsonSerializer.Serialize(new StatusResponse(true, "1.0.0"), ControlJsonContext.Default.StatusResponse));
        Assert.NotNull(ControlJsonContext.Default.HostInfoEntryArray);
    }

    [Fact]
    public void UserSettings_RoundtripSection()
    {
        var payload = new Dictionary<string, UserSettings>
        {
            [UserSettings.SectionName] = new() { Theme = AppTheme.Dark, AutoStartEnabled = true }
        };
        var json = JsonSerializer.Serialize(payload, UserSettingsJsonContext.Default.DictionaryStringUserSettings);
        Assert.Contains("\"User\"", json);
        Assert.Contains("\"Theme\"", json);
        var loaded = JsonSerializer.Deserialize(json, UserSettingsJsonContext.Default.DictionaryStringUserSettings);
        Assert.Equal(AppTheme.Dark, loaded?[UserSettings.SectionName].Theme);
    }
}
