using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
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

        var built = Version.Parse(manifest.Version);
        var found = ReadFrameworkVersion(manifest, outputDirectory);
        if (found.Major != built.Major)
            throw new TestingRuntimeIncompatibleException(OtherMajor(manifest, found, built, adapterVersion));
        if (found < built)
            throw new TestingRuntimeIncompatibleException(BelowFloor(manifest, found, adapterVersion));

        return manifest;
    }

    public static Version ReadFrameworkVersion(RuntimeManifest manifest, string outputDirectory)
    {
        var path = Path.Combine(outputDirectory, manifest.Assembly + ".dll");
        if (!File.Exists(path))
        {
            throw new TestingGenerationBuildException(
                $"{manifest.Assembly}.dll was not found beside the {manifest.Framework} test assembly.");
        }

        return AssemblyName.GetAssemblyName(path).Version
               ?? throw new TestingGenerationBuildException($"{manifest.Assembly}.dll has no assembly version.");
    }

    private static string OtherMajor(RuntimeManifest manifest, Version found, Version built, string adapterVersion) =>
        $"{manifest.Framework} {RuntimeManifest.Display(found)} is not supported by {AdapterName} {adapterVersion} (built for {manifest.Framework} {built.Major}.x). " +
        $"Update {AdapterName} to a version for {manifest.Framework} {found.Major}.";

    private static string BelowFloor(RuntimeManifest manifest, Version found, string adapterVersion) =>
        $"{manifest.Framework} {RuntimeManifest.Display(found)} is older than the {manifest.DisplayFloor()} that {AdapterName} {adapterVersion} was built against. " +
        $"Update {manifest.Framework} to {manifest.DisplayFloor()} or later.";
}
