using DevTools.Testing.Abstractions;

namespace DevTools.NUnit.MTP;

/// <summary>
/// Back-compat alias for <see cref="DiscoveryRefs"/>.
/// </summary>
internal static class NUnitDiscoveryRefs
{
    public const string FileSuffix = DiscoveryRefs.FileSuffix;

    public static string FilePathFor(string assemblyPath) => DiscoveryRefs.FilePathFor(assemblyPath);

    public static IReadOnlyDictionary<string, string> Read(string assemblyPath) => DiscoveryRefs.Read(assemblyPath);
}
