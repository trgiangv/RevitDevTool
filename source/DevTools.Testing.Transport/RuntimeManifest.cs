using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using System.Diagnostics;
using System.Text.Json;

namespace DevTools.Testing.Transport;

public sealed record RuntimeManifest(
    string Framework,
    string Entry,
    ProtocolRange Contract,
    string Assembly,
    string Version)
{
    private const string AdapterAssemblyFileName = "DevTools.TestAdapter.dll";
    private const string UnknownVersion = "unknown";

    public static string FileName(TestFrameworkId frameworkId) => $"DevTools.{frameworkId}.Runtime.json";

    public static string Display(System.Version version) =>
        version.Revision == 0 ? version.ToString(3) : version.ToString();

    public string DisplayFloor() => FrameworkVersion.Parse(Version).ToString();

    public static RuntimeManifest? TryRead(string outputDirectory, TestFrameworkId frameworkId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var path = Path.Combine(outputDirectory, FileName(frameworkId));
        if (!File.Exists(path))
            return null;

        try
        {
            var manifest = JsonSerializer.Deserialize(File.ReadAllText(path), TestingJsonContext.Default.RuntimeManifest);
            return manifest is not null && manifest.IsComplete(frameworkId) ? manifest : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string ReadAdapterVersion(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var path = Path.Combine(outputDirectory, AdapterAssemblyFileName);
        var version = File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).ProductVersion : null;
        return string.IsNullOrWhiteSpace(version) ? UnknownVersion : version!.Split('+')[0];
    }

    private bool IsComplete(TestFrameworkId frameworkId) =>
        string.Equals(Framework, frameworkId.ToString(), StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Entry)
        && !string.IsNullOrWhiteSpace(Assembly)
        && Contract.Min > 0
        && Contract.Current >= Contract.Min
        && FrameworkVersion.TryParse(Version, out _);
}
