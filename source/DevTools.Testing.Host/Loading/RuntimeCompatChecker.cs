using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using System.Diagnostics;
using System.Reflection;

namespace DevTools.Testing.Host.Loading;

public sealed class TestingRuntimeIncompatibleException(string message) : Exception(message);

internal static class RuntimeCompatChecker
{
    private const string AdapterName = "RevitDevTool.TestAdapter";

    public static RuntimeManifest Check(TestFrameworkId frameworkId, string outputDirectory)
    {
        var adapterVersion = RuntimeManifest.ReadAdapterVersion(outputDirectory);
        var manifest = RuntimeManifest.TryRead(outputDirectory, frameworkId)
            ?? throw new TestingRuntimeIncompatibleException(TestingProtocol.NoRuntimeManifest(adapterVersion));

        if (TestingProtocol.RuntimeMismatch(manifest, adapterVersion) is { } mismatch)
            throw new TestingRuntimeIncompatibleException(mismatch);

        var built = FrameworkVersion.Parse(manifest.Version);
        var found = ReadFrameworkVersion(manifest, outputDirectory);
        if (found.Major != built.Major)
            throw new TestingRuntimeIncompatibleException(OtherMajor(manifest, found, built, adapterVersion));
        if (found < built)
            throw new TestingRuntimeIncompatibleException(BelowFloor(manifest, found, adapterVersion));

        return manifest;
    }

    public static FrameworkVersion ReadFrameworkVersion(RuntimeManifest manifest, string outputDirectory)
    {
        var path = Path.Combine(outputDirectory, manifest.Assembly + ".dll");
        if (!File.Exists(path))
        {
            throw new TestingRuntimeIncompatibleException(
                $"{manifest.Assembly}.dll was not found beside the {manifest.Framework} test assembly. " +
                $"Reference the {manifest.Framework} package in the test project.");
        }

        if (FrameworkVersion.TryParse(FileVersionInfo.GetVersionInfo(path).ProductVersion, out var product))
            return product;

        return FrameworkVersion.From(
            AssemblyName.GetAssemblyName(path).Version
            ?? throw new TestingGenerationBuildException($"{manifest.Assembly}.dll has no assembly version."));
    }

    private static string OtherMajor(RuntimeManifest manifest, FrameworkVersion found, FrameworkVersion built, string adapterVersion) =>
        $"{manifest.Framework} {found} is not supported by {AdapterName} {adapterVersion} (built for {manifest.Framework} {built.Major}.x). " +
        $"Update {AdapterName} to a version for {manifest.Framework} {found.Major}.";

    private static string BelowFloor(RuntimeManifest manifest, FrameworkVersion found, string adapterVersion) =>
        $"{manifest.Framework} {found} is older than the {manifest.DisplayFloor()} that {AdapterName} {adapterVersion} was built against. " +
        $"Update {manifest.Framework} to {manifest.DisplayFloor()} or later.";
}
