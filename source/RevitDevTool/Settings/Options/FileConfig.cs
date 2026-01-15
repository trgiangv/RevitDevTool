using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;
namespace RevitDevTool.Settings.Options;

/// <summary>
/// Stores configurations as JSON files in the settings directory.
/// </summary>
[UsedImplicitly]
public sealed class FileConfig(IOptions<PathOptions> pathOptions) : IFileConfig<PathOptions>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public PathOptions Options => pathOptions.Value;

    public T? Load<T>() where T : class
    {
        var path = Options.GetSettingsPath<T>();
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream);
        }
        catch
        {
            return null;
        }
    }

    public void Save<T>(T config) where T : class
    {
        var path = Options.GetSettingsPath<T>();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }
}
