using DevTools.Daemon.Desktop.Models;
using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop;
using DevTools.Settings.Configs;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class ControlJsonTests
{
    [TestMethod]
    public void Options_RoundtripTokenAndControlPayloads()
    {
        var token = new TokenData { AccessToken = "a", RefreshToken = "r", ExpiresAt = 1 };
        var json = JsonSerializer.Serialize(token, ControlJsonContext.Default.TokenData);
        var loaded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.TokenData);
        Assert.AreEqual("a", loaded?.AccessToken);
    }

    [TestMethod]
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
        Assert.AreEqual(AppTheme.Dark, loaded?[UserSettings.SectionName].Theme);
    }
}
