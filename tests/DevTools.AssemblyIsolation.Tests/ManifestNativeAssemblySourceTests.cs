using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class ManifestNativeAssemblySourceTests
{
    [Fact]
    public void Resolves_native_candidates_by_name_and_file_name()
    {
        var root = CreateTempRoot();
        var candidate = new AssemblyCandidate(Path.Combine(root, "native-tool.dll"), root);
        var source = new ManifestNativeAssemblySource([candidate]);

        Assert.Same(candidate, source.Resolve("native-tool.dll"));
        Assert.Same(candidate, source.Resolve("native-tool"));
        Assert.Same(candidate, source.Resolve("NATIVE-TOOL.DLL"));
    }

    [Fact]
    public void Rejects_ambiguous_native_candidates_with_the_same_file_name()
    {
        var root = CreateTempRoot();
        var first = new AssemblyCandidate(Path.Combine(root, "alpha", "native-tool.dll"), root);
        var second = new AssemblyCandidate(Path.Combine(root, "beta", "native-tool.dll"), root);

        var exception = Assert.Throws<InvalidOperationException>(() => new ManifestNativeAssemblySource([first, second]));
        Assert.Contains("native-tool", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "manifest-native-assembly-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
