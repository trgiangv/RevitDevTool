using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Discover: sample files stay next to source (no CopyToOutputDirectory).
// [CallerFilePath] is the compile-time .cs path — same machine as the build.

[TestFixture]
public sealed class SourceAssetPathTests
{
    [Test]
    public void Sample_is_next_to_source_not_shadow()
    {
        var path = Sample("sample.txt");
        Assert.That(File.Exists(path), Is.True, path);
        Assert.That(File.ReadAllText(path).Trim(), Is.EqualTo("devtools-nunit-source-asset"));

        var work = TestContext.CurrentContext.WorkDirectory;
        var shadowCopy = Path.Combine(work, "Testdata", "sample.txt");
        Assert.That(File.Exists(shadowCopy), Is.False, shadowCopy);

        Console.WriteLine($"source={path}");
        Console.WriteLine($"work={work}");
        Console.WriteLine($"test-dir={TestContext.CurrentContext.TestDirectory}");
    }

    static string Sample(string name, [CallerFilePath] string cs = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cs)!, "Testdata", name));
}
