using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Build.Trains;

public static partial class VersionFile
{
    public static string Read(string root, string path) =>
        path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            ? ReadToml(root, path)
            : ReadProperty(root, path, "Version");

    public static string ReadProperty(string root, string path, string property)
    {
        var value = XDocument.Load(Existing(root, path))
            .Descendants()
            .Where(element => element.Name.LocalName == property)
            .Select(element => element.Value.Trim())
            .FirstOrDefault(text => text.Length > 0);

        return value ?? throw new InvalidOperationException($"{path} has no <{property}>; add it.");
    }

    public static string WithTomlVersion(string text, string version) =>
        TomlVersionRegex().Replace(text, $"version = \"{version}\"", 1);

    private static string ReadToml(string root, string path)
    {
        var match = TomlVersionRegex().Match(File.ReadAllText(Existing(root, path)));
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException($"{path} has no version line; add version = \"X.Y.Z\" under [project].");
    }

    private static string Existing(string root, string path)
    {
        var full = Path.Combine(root, path);
        return File.Exists(full) ? full : throw new InvalidOperationException($"{path} is missing; the train cannot read its version.");
    }

    [GeneratedRegex("""^version\s*=\s*"([^"]+)"[ \t]*(?=\r?$)""",RegexOptions.Multiline)]
    private static partial Regex TomlVersionRegex();
}
