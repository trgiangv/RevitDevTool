using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;
using System.Diagnostics;
using System.Text;

namespace DevTools.NUnit.Host.Tests;

internal static class NUnitGenerationTestEnvironment
{
    private const string RuntimeFileName = "DevTools.NUnit.Runtime";

    public const string FrameworkFileName = "nunit.framework.dll";

    public static string RepositoryRoot { get; } = LocateRepositoryRoot();

    public static string FixtureOutputDirectory { get; } = Path.Combine(
        RepositoryRoot,
        "tests",
        "DevTools.NUnit.Runtime.Fixtures",
        "bin",
        "Debug",
        "net10.0-windows");

    private static string RuntimeOutputBase { get; } = Path.Combine(
        RepositoryRoot,
        "source",
        "DevTools.NUnit.Runtime",
        "bin",
        "Debug",
        "net10.0-windows",
        RuntimeFileName);

    public static string SharedAssemblyPath { get; } = Path.Combine(
        RepositoryRoot,
        "source",
        "DevTools.Testing.Abstractions",
        "bin",
        "Debug",
        "net10.0-windows",
        "DevTools.Testing.Abstractions.dll");

    public static string CreateIsolatedGenerationsRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "DevTools.nunit." + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    public static string CreateFixtureWorkspace(
        string parentDirectory,
        string folderName,
        Action<string>? configure = null)
    {
        var workspace = Path.Combine(parentDirectory, folderName);
        CopyDirectory(FixtureOutputDirectory, workspace);
        foreach (var extension in new[] { ".dll", ".json", ".pdb" })
            File.Copy(RuntimeOutputBase + extension, Path.Combine(workspace, RuntimeFileName + extension), overwrite: true);
        File.Copy(typeof(OutputGeneration).Assembly.Location, Path.Combine(workspace, "DevTools.TestAdapter.dll"), overwrite: true);
        configure?.Invoke(workspace);
        return Path.Combine(workspace, "DevTools.NUnit.Runtime.Fixtures.dll");
    }

    public static string CreateGenerationOneAssembly(string parentDirectory, string folderName) =>
        CreateFixtureWorkspace(parentDirectory, folderName);

    public static NUnitGenerationHarness CreateBuilder(string generationsRoot) =>
        new(new TestingGenerationStore(generationsRoot), new OutputGeneration(TestFrameworkId.NUnit));

    public static string PatchFileVersion(string assemblyPath)
    {
        var original = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion!;
        var patched = original[..^1] + (original[^1] == '9' ? '8' : '9');
        var bytes = File.ReadAllBytes(assemblyPath);
        ReplaceAll(bytes, Encoding.UTF8.GetBytes(original), Encoding.UTF8.GetBytes(patched));
        ReplaceAll(bytes, Encoding.Unicode.GetBytes(original), Encoding.Unicode.GetBytes(patched));
        File.WriteAllBytes(assemblyPath, bytes);
        return patched;
    }

    private static void ReplaceAll(byte[] buffer, byte[] pattern, byte[] replacement)
    {
        for (var index = IndexOf(buffer, pattern); index >= 0; index = IndexOf(buffer, pattern, index + 1))
            Array.Copy(replacement, 0, buffer, index, replacement.Length);
    }

    private static int IndexOf(byte[] buffer, byte[] pattern, int start = 0)
    {
        for (var i = start; i <= buffer.Length - pattern.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return i;
        }

        return -1;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDirectory, destinationDirectory));

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var destinationPath = file.Replace(sourceDirectory, destinationDirectory);
            var destinationFolder = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

internal sealed class NUnitGenerationHarness(
    TestingGenerationStore store,
    OutputGeneration policy)
{
    public TestingGenerationManifest Build(string testAssemblyPath) => store.Build(policy, testAssemblyPath);
}
