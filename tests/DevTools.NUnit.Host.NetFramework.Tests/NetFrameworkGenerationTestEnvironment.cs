using System.Reflection;
using System.Text;
using DevTools.NUnit.Host.Loading;

namespace DevTools.NUnit.Host.NetFramework.Tests;

public static class NetFrameworkGenerationTestEnvironment
{
    private const string GenerationOneMarker = "generation-one";
    private const string GenerationTwoMarker = "generation-two";
    private const string BehaviorOneMarker = "behavior-one";
    private const string BehaviorTwoMarker = "behavior-two";

    public static string RepositoryRoot { get; } = LocateRepositoryRoot();

    public static string FixtureOutputDirectory { get; } = Path.Combine(
        RepositoryRoot,
        "tests",
        "DevTools.NUnit.Runtime.Fixtures",
        "bin",
        "Debug",
        "net48");

    public static string RuntimeAssemblyPath { get; } = Path.Combine(
        RepositoryRoot,
        "source",
        "DevTools.NUnit.Runtime",
        "bin",
        "Debug",
        "net48",
        NUnitGenerationBuilder.RuntimeAssemblyFileName);

    public static string RuntimeSymbolPath { get; } = Path.Combine(
        RepositoryRoot,
        "source",
        "DevTools.NUnit.Runtime",
        "bin",
        "Debug",
        "net48",
        NUnitGenerationBuilder.RuntimeSymbolFileName);

    public static string ConflictingNUnitStubPath { get; } = Path.Combine(
        RepositoryRoot,
        "tests",
        "DevTools.NUnit.Host.NetFramework.Tests",
        "Fixtures",
        "ConflictingNUnitFramework",
        "bin",
        "Debug",
        "net48",
        NUnitGenerationBuilder.FrameworkAssemblyFileName);

    public static string DependencyConsumerOutputDirectory { get; } = Path.Combine(
        RepositoryRoot,
        "tests",
        "DevTools.NUnit.Host.NetFramework.Tests",
        "Fixtures",
        "DependencyConsumer",
        "bin",
        "Debug",
        "net48");

    public static string CreateIsolatedGenerationsRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "DevTools",
            "NUnit",
            "Generations",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    public static NUnitGenerationBuilder CreateBuilder(string generationsRoot) =>
        new(
            () => new NUnitRuntimeSource(
                RuntimeAssemblyPath,
                File.Exists(RuntimeSymbolPath) ? RuntimeSymbolPath : null,
                RuntimeDependencyPaths()),
            generationsRoot);

    private static IReadOnlyList<string> RuntimeDependencyPaths() =>
        new[] { "System.Reflection.Metadata.dll", "System.Collections.Immutable.dll" }
            .Select(name => Path.Combine(Path.GetDirectoryName(RuntimeAssemblyPath)!, name))
            .Where(File.Exists)
            .ToList();

    public static string CreateGenerationOneAssembly(string parentDirectory, string folderName) =>
        CreateFixtureWorkspace(parentDirectory, folderName);

    public static string CreateGenerationTwoAssembly(string parentDirectory, string folderName)
    {
        var assemblyPath = CreateFixtureWorkspace(parentDirectory, folderName);
        PatchUtf16Constant(assemblyPath, GenerationOneMarker, GenerationTwoMarker, replaceAll: true);
        return assemblyPath;
    }

    public static NUnitGenerationManifest BuildFixtureGenerationOne()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = CreateGenerationOneAssembly(workspace.Root, "generation-one");
        var generationsRoot = CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    public static NUnitGenerationManifest BuildFixtureGenerationTwo()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = CreateGenerationTwoAssembly(workspace.Root, "generation-two");
        var generationsRoot = CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    public static NUnitGenerationManifest BuildDependencyGenerationOne()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = CreateDependencyWorkspace(workspace.Root, "dependency-generation-one");
        var generationsRoot = CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    public static NUnitGenerationManifest BuildDependencyGenerationTwo()
    {
        using var workspace = new TempWorkspace();
        const string folderName = "dependency-generation-two";
        var testAssembly = CreateDependencyWorkspace(workspace.Root, folderName);
        PatchUtf16Constant(
            Path.Combine(workspace.Root, folderName, "private", "GenerationPrivateDependency.dll"),
            BehaviorOneMarker,
            BehaviorTwoMarker,
            replaceAll: true);
        PatchUtf16Constant(testAssembly, BehaviorOneMarker, BehaviorTwoMarker, replaceAll: true);
        var generationsRoot = CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    public static NUnitGenerationManifest BuildRootDependencyGenerationOne()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = CreateRootDependencyWorkspace(workspace.Root, "root-dependency-generation-one");
        var generationsRoot = CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    public static NUnitGenerationManifest BuildRootDependencyGenerationTwo()
    {
        using var workspace = new TempWorkspace();
        const string folderName = "root-dependency-generation-two";
        var testAssembly = CreateRootDependencyWorkspace(workspace.Root, folderName);
        PatchUtf16Constant(
            Path.Combine(workspace.Root, folderName, "GenerationPrivateDependency.dll"),
            BehaviorOneMarker,
            BehaviorTwoMarker,
            replaceAll: true);
        PatchUtf16Constant(testAssembly, BehaviorOneMarker, BehaviorTwoMarker, replaceAll: true);
        var generationsRoot = CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    public static Assembly LoadConflictingNUnitIntoAppDomain()
    {
        if (!File.Exists(ConflictingNUnitStubPath))
        {
            throw new FileNotFoundException(
                $"Conflicting NUnit stub was not built: {ConflictingNUnitStubPath}",
                ConflictingNUnitStubPath);
        }

        var isolatedCopyDirectory = Path.Combine(
            Path.GetTempPath(),
            "DevTools",
            "NUnit",
            "ConflictingDefault",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(isolatedCopyDirectory);
        var isolatedCopyPath = Path.Combine(isolatedCopyDirectory, NUnitGenerationBuilder.FrameworkAssemblyFileName);
        File.Copy(ConflictingNUnitStubPath, isolatedCopyPath, overwrite: true);

        var loaded = Assembly.LoadFile(isolatedCopyPath);
        if (loaded.GetName().Version != new Version(3, 14, 0, 0))
        {
            throw new InvalidOperationException(
                $"Expected conflicting NUnit stub version 3.14.0.0 but found {loaded.GetName().Version}.");
        }

        return loaded;
    }

    private static string CreateFixtureWorkspace(string parentDirectory, string folderName)
    {
        var workspace = Path.Combine(parentDirectory, folderName);
        CopyDirectory(FixtureOutputDirectory, workspace);
        return Path.Combine(workspace, "DevTools.NUnit.Runtime.Fixtures.dll");
    }

    private static string CreateDependencyWorkspace(string parentDirectory, string folderName)
    {
        var workspace = Path.Combine(parentDirectory, folderName);
        CopyDirectory(DependencyConsumerOutputDirectory, workspace);

        var dependencyPath = Path.Combine(workspace, "GenerationPrivateDependency.dll");
        if (File.Exists(dependencyPath))
        {
            var privateDirectory = Path.Combine(workspace, "private");
            Directory.CreateDirectory(privateDirectory);
            File.Move(dependencyPath, Path.Combine(privateDirectory, "GenerationPrivateDependency.dll"));
        }

        return Path.Combine(workspace, "DependencyConsumer.dll");
    }

    private static string CreateRootDependencyWorkspace(string parentDirectory, string folderName)
    {
        var workspace = Path.Combine(parentDirectory, folderName);
        CopyDirectory(DependencyConsumerOutputDirectory, workspace);
        return Path.Combine(workspace, "DependencyConsumer.dll");
    }

    private static void PatchUtf16Constant(
        string filePath,
        string original,
        string replacement,
        bool replaceAll = false)
    {
        var originalBytes = Encoding.Unicode.GetBytes(original);
        var replacementBytes = Encoding.Unicode.GetBytes(replacement);
        if (replacementBytes.Length != originalBytes.Length)
        {
            throw new InvalidOperationException(
                "Replacement constants must preserve encoded byte length.");
        }

        var bytes = File.ReadAllBytes(filePath);
        var index = 0;
        var replaced = 0;

        while (index <= bytes.Length - originalBytes.Length)
        {
            var matchIndex = IndexOf(bytes, originalBytes, index);
            if (matchIndex < 0)
                break;

            Array.Copy(replacementBytes, 0, bytes, matchIndex, replacementBytes.Length);
            replaced++;
            index = matchIndex + replacementBytes.Length;

            if (!replaceAll)
                break;
        }

        if (replaced == 0)
            throw new InvalidOperationException($"Could not locate '{original}' in {filePath}.");

        File.WriteAllBytes(filePath, bytes);
    }

    private static int IndexOf(byte[] buffer, byte[] pattern, int startIndex = 0)
    {
        for (var i = startIndex; i <= buffer.Length - pattern.Length; i++)
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

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "DevTools",
                "NUnit",
                "NetFrameworkRuntimeTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp workspaces.
            }
        }
    }
}
