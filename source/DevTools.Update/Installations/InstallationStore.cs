using System.Text.Json;

namespace DevTools.Update.Installations;

/// <summary>
///     Reads and writes <c>installation.json</c> in <c>%APPDATA%\RevitDevTool</c>, because it covers every bundle.
///     A write replaces the file in one move.
/// </summary>
public sealed class InstallationStore(string path)
{
    private readonly Lock _gate = new();

    public string Path { get; } = path;

    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitDevTool", "installation.json");

    public bool Exists => File.Exists(Path);

    public Installation? Load()
    {
        lock (_gate)
        {
            return File.Exists(Path)
                ? JsonSerializer.Deserialize(File.ReadAllBytes(Path), UpdateJsonContext.Default.Installation)
                : null;
        }
    }

    public void Save(Installation installation)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(installation, UpdateJsonContext.Default.Installation));
            File.Move(temp, Path, overwrite: true);
        }
    }

    public void Delete()
    {
        lock (_gate)
        {
            File.Delete(Path);
        }
    }

    public void Update(Action<Installation> apply)
    {
        lock (_gate)
        {
            var installation = Load() ?? new Installation();
            apply(installation);
            Save(installation);
        }
    }
}
