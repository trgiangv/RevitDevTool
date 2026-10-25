using DevTools.Update.Autodesk;
using System.Text.Json;

namespace DevTools.Update.Installations;

/// <summary>
///     Reads and writes <c>installation.json</c> in the bundle root. A write replaces the file in one move.
/// </summary>
public sealed class InstallationStore(AutodeskBundle bundle)
{
    private readonly Lock _gate = new();

    public bool Exists => File.Exists(bundle.InstallationPath);

    public Installation? Load()
    {
        lock (_gate)
        {
            return File.Exists(bundle.InstallationPath)
                ? JsonSerializer.Deserialize(File.ReadAllBytes(bundle.InstallationPath), UpdateJsonContext.Default.Installation)
                : null;
        }
    }

    public void Save(Installation installation)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(bundle.Root);
            var temp = bundle.InstallationPath + ".tmp";
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(installation, UpdateJsonContext.Default.Installation));
            File.Move(temp, bundle.InstallationPath, overwrite: true);
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
