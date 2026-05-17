using System.Text.Json.Serialization;

namespace RevitDevTool.CommandBrowser.Models;

/// <summary>
/// Persisted settings for the Command Browser feature.
/// Stored as a standalone JSON file via <see cref="DevTools.Settings.FileConfig"/>.
/// </summary>
[Serializable]
public sealed class CommandBrowserConfig
{
    [JsonPropertyName("favoriteCommandIds")]
    public List<string> FavoriteCommandIds { get; set; } = [];

    [JsonPropertyName("isBarVisible")]
    public bool IsBarVisible { get; set; } = true;
}
