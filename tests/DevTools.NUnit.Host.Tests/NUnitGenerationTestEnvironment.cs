using System.Diagnostics;
using System.Text;
using DevTools.NUnit.Host.Loading;

namespace DevTools.NUnit.Host.Tests;

internal static class NUnitGenerationTestEnvironment
{
    private const string GenerationOneMarker = "generation-one";
    private const string GenerationTwoMarker = "generation-two";

    public static string RepositoryRoot { get; } = LocateRepositoryRoot();

    public static string FixtureOutputDirectory { get; } = Path.Combine(
        RepositoryRoot,
        "tests",
        "DevTools.NUnit.Runtime.Fixtures",
        "bin",
        "Debug",
        "net10.0-windows");

    public static string CoreAssemblyPath { get; } = Path.Combine(
        RepositoryRoot,
        "source",
        "DevTools.NUnit.Core",
        "bin",
        "Debug",
        "net10.0-windows",
        "DevTools.NUnit.Core.dll");

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

    public static string CreateFixtureWorkspace(
        string parentDirectory,
        string folderName,
        Action<string>? configure = null)
    {
        var workspace = Path.Combine(parentDirectory, folderName);
        CopyDirectory(FixtureOutputDirectory, workspace);
        configure?.Invoke(workspace);
        return Path.Combine(workspace, "DevTools.NUnit.Runtime.Fixtures.dll");
    }

    public static string CreateGenerationOneAssembly(string parentDirectory, string folderName) =>
        CreateFixtureWorkspace(parentDirectory, folderName);

    public static string CreateGenerationTwoAssembly(string parentDirectory, string folderName)
    {
        var assemblyPath = CreateFixtureWorkspace(parentDirectory, folderName);
        PatchGenerationMarker(assemblyPath, GenerationTwoMarker);
        return assemblyPath;
    }

    public static string BuildFixtureGeneration(
        string parentDirectory,
        string folderName,
        string generationMarker)
    {
        var outputDirectory = Path.Combine(parentDirectory, folderName);
        Directory.CreateDirectory(outputDirectory);

        var projectPath = Path.Combine(
            RepositoryRoot,
            "tests",
            "DevTools.NUnit.Runtime.Fixtures",
            "DevTools.NUnit.Runtime.Fixtures.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Debug");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("net10.0-windows");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add($"-p:OutputPath={outputDirectory}");
        startInfo.ArgumentList.Add("-p:AppendTargetFrameworkToOutputPath=false");
        startInfo.ArgumentList.Add($"-p:GenerationMarker={generationMarker}");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet build for the NUnit fixture.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Timed out rebuilding the NUnit fixture generation.");
        }

        var output = standardOutput.GetAwaiter().GetResult();
        var error = standardError.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"NUnit fixture generation build failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                output + Environment.NewLine + error);
        }

        return Path.Combine(outputDirectory, "DevTools.NUnit.Runtime.Fixtures.dll");
    }

    public static NUnitRuntimeSource CreateRuntimeStub(string parentDirectory)
    {
        var runtimeDirectory = Path.Combine(parentDirectory, "runtime-source");
        Directory.CreateDirectory(runtimeDirectory);

        var assemblyPath = Path.Combine(runtimeDirectory, NUnitGenerationBuilder.RuntimeAssemblyFileName);
        var symbolPath = Path.Combine(runtimeDirectory, NUnitGenerationBuilder.RuntimeSymbolFileName);

        File.Copy(typeof(NUnitGenerationBuilderTests).Assembly.Location, assemblyPath, overwrite: true);

        var sourceSymbolPath = Path.ChangeExtension(
            typeof(NUnitGenerationBuilderTests).Assembly.Location,
            ".pdb");

        if (File.Exists(sourceSymbolPath))
            File.Copy(sourceSymbolPath, symbolPath, overwrite: true);

        return new NUnitRuntimeSource(
            assemblyPath,
            File.Exists(symbolPath) ? symbolPath : null,
            Array.Empty<string>());
    }

    public static NUnitGenerationBuilder CreateBuilder(
        string generationsRoot,
        string runtimeParentDirectory)
    {
        var runtimeSource = CreateRuntimeStub(runtimeParentDirectory);
        return new NUnitGenerationBuilder(
            () => runtimeSource,
            generationsRoot);
    }

    public static void PatchGenerationMarker(string assemblyPath, string marker)
    {
        var original = Encoding.Unicode.GetBytes(GenerationOneMarker);
        var replacement = Encoding.Unicode.GetBytes(marker);
        if (replacement.Length != original.Length)
        {
            throw new InvalidOperationException(
                "Generation marker replacements must preserve encoded byte length.");
        }

        var bytes = File.ReadAllBytes(assemblyPath);
        var index = IndexOf(bytes, original);
        if (index < 0)
            throw new InvalidOperationException($"Could not locate {GenerationOneMarker} in {assemblyPath}.");

        Array.Copy(replacement, 0, bytes, index, replacement.Length);
        File.WriteAllBytes(assemblyPath, bytes);
    }

    private static int IndexOf(byte[] buffer, byte[] pattern)
    {
        for (var i = 0; i <= buffer.Length - pattern.Length; i++)
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
