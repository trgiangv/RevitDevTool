using System.Text.Json.Serialization;

namespace DevTools.Daemon.Desktop;

[JsonSerializable(typeof(UserSettings))]
[JsonSerializable(typeof(Dictionary<string, UserSettings>))]
internal sealed partial class UserSettingsJsonContext : JsonSerializerContext;
