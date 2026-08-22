using System.Reflection;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;

namespace DevTools.TUnit.Host;

public sealed record TUnitRuntimeSource(string AssemblyPath, IReadOnlyList<string> DependencyPaths);
public delegate TUnitRuntimeSource TUnitRuntimeSourceProvider();

public sealed class TUnitGenerationPolicy(TUnitRuntimeSourceProvider runtimeSourceProvider) : ITestingGenerationPolicy
{
    public const string FrameworkId = "tunit";
    public const string RuntimeAssemblyFileName = "DevTools.TUnit.Runtime.dll";
    private const string FrameworkAssemblyFileName = "TUnit.Core.dll";

    public TestingGenerationPlan CreatePlan(string testAssemblyPath)
    {
        var assemblyPath = Path.GetFullPath(testAssemblyPath);
        if (!File.Exists(assemblyPath))
            throw new TestingGenerationBuildException($"TUnit test assembly not found: {assemblyPath}");

        var outputDirectory = Path.GetDirectoryName(assemblyPath)!;
        var files = new Dictionary<string, TestingGenerationFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Normalize(Path.GetRelativePath(outputDirectory, path));
            if (!IsVolatile(relativePath) && !IsSharedContract(path))
                files[relativePath] = new TestingGenerationFile(path, relativePath, Classify(path));
        }

        if (!files.Keys.Any(path => string.Equals(Path.GetFileName(path), FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new TestingGenerationBuildException(
                $"{FrameworkAssemblyFileName} was not found beside the TUnit test assembly.");
        }

        var runtime = runtimeSourceProvider();
        AddRuntimeFile(files, runtime.AssemblyPath, RuntimeAssemblyFileName);
        foreach (var dependency in runtime.DependencyPaths)
            AddRuntimeFile(files, dependency, Path.GetFileName(dependency));

        return new TestingGenerationPlan(
            FrameworkId,
            assemblyPath,
            files.Values.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(),
            RuntimeAssemblyFileName);
    }

    public void ValidatePublished(TestingGenerationManifest manifest)
    {
        if (!string.Equals(manifest.FrameworkId, FrameworkId, StringComparison.OrdinalIgnoreCase))
            throw new TestingGenerationBuildException($"Expected TUnit generation framework ID '{FrameworkId}'.");
        if (!File.Exists(manifest.RuntimeAssemblyPath))
            throw new TestingGenerationBuildException($"Published TUnit runtime is missing: {manifest.RuntimeAssemblyPath}");
        if (!manifest.ManagedAssemblies.Any(path => string.Equals(Path.GetFileName(path), FrameworkAssemblyFileName,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new TestingGenerationBuildException($"Published TUnit generation is missing {FrameworkAssemblyFileName}.");
        }
    }

    private static void AddRuntimeFile(
        IDictionary<string, TestingGenerationFile> files,
        string sourcePath,
        string relativePath)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath))
            throw new TestingGenerationBuildException($"TUnit runtime dependency not found: {sourcePath}");
        if (IsSharedContract(sourcePath))
            return;
        files[relativePath] = new TestingGenerationFile(sourcePath, relativePath, Classify(sourcePath));
    }

    private static bool IsSharedContract(string path)
    {
        if (!IsManaged(path))
            return false;
        return string.Equals(
            AssemblyName.GetAssemblyName(path).Name,
            typeof(ITestingRuntimeSession).Assembly.GetName().Name,
            StringComparison.OrdinalIgnoreCase);
    }

    private static TestingGenerationFileKind Classify(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase))
            return TestingGenerationFileKind.Symbols;
        if (IsSatelliteResourceAssembly(path))
            return TestingGenerationFileKind.Other;
        if (IsManaged(path))
        {
            var identity = AssemblyName.GetAssemblyName(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileName, identity.Name, StringComparison.OrdinalIgnoreCase))
                return TestingGenerationFileKind.Other;

            return TestingGenerationFileKind.Managed;
        }
        return string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase)
            ? TestingGenerationFileKind.Native
            : TestingGenerationFileKind.Other;
    }

    private static bool IsSatelliteResourceAssembly(string path)
    {
        if (!IsManaged(path))
            return false;
        var identity = AssemblyName.GetAssemblyName(path);
        return identity.Name?.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) == true
               && !string.IsNullOrWhiteSpace(identity.CultureName);
    }

    private static bool IsManaged(string path)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            _ = AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
    }

    private static bool IsVolatile(string relativePath)
    {
        var root = Normalize(relativePath).Split('\\')[0];
        return root.Equals("TestResults", StringComparison.OrdinalIgnoreCase)
               || root.Equals("Log", StringComparison.OrdinalIgnoreCase)
               || Path.GetExtension(relativePath).Equals(".log", StringComparison.OrdinalIgnoreCase)
               || Path.GetExtension(relativePath).Equals(".diag", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('/', '\\');
}
