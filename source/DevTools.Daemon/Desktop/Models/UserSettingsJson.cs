using System.Text.Json.Serialization;

namespace DevTools.Daemon.Desktop.Models;

[JsonSerializable(typeof(UserSettings))]
[JsonSerializable(typeof(Dictionary<string, UserSettings>))]
internal sealed partial class UserSettingsJsonContext : JsonSerializerContext;
