using System.Diagnostics;
using System.Text.Json;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class AssemblyBoundaryTests
{
    static readonly string[] ForbiddenReferences =
    [
        "Execution",
        "Mcp",
        "NUnit",
        "RevitAPI",
        "acmgd",
        "PresentationFramework",
        "Microsoft.Extensions.Logging",
        "ZLogger",
    ];

    [Fact]
    public void Assembly_isolation_project_is_a_host_neutral_leaf()
    {
        var projectDirectory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.AssemblyIsolation");
        var sourceFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path, projectDirectory))
            .ToArray();
        Assert.NotEmpty(sourceFiles);

        var sourceAndProjectFiles = sourceFiles.Append(Path.Combine(projectDirectory, "DevTools.AssemblyIsolation.csproj"));
        var sourceViolations = sourceAndProjectFiles
            .SelectMany(path => ForbiddenReferences
                .Where(forbidden => System.Text.RegularExpressions.Regex.IsMatch(
                    File.ReadAllText(path),
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(forbidden)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .Select(forbidden => $"{Path.GetFileName(path)} contains {forbidden}"))
            .ToArray();

        var referenceViolations = typeof(AssemblyIdentityMatcher).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(reference => ForbiddenReferences.Any(forbidden =>
                reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
            .Select(reference => $"references {reference}")
            .ToArray();

        Assert.True(sourceViolations.Length == 0, string.Join(Environment.NewLine, sourceViolations));
        Assert.True(referenceViolations.Length == 0, string.Join(Environment.NewLine, referenceViolations));
    }

    [Theory]
    [InlineData("net48")]
    [InlineData("net8.0-windows")]
    [InlineData("net10.0-windows")]
    public void Assembly_isolation_project_has_only_the_allowed_resolved_package_reference(string targetFramework)
    {
        var project = Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.AssemblyIsolation",
            "DevTools.AssemblyIsolation.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("-getItem:PackageReference");
        startInfo.ArgumentList.Add($"-p:TargetFramework={targetFramework}");
        startInfo.ArgumentList.Add("-nologo");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, error);
        using var result = JsonDocument.Parse(output);
        var packages = result.RootElement
            .GetProperty("Items")
            .GetProperty("PackageReference")
            .EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString()
                ?? throw new InvalidOperationException("Package reference identity is missing."))
            .ToArray();

        Assert.Equal(["System.Reflection.MetadataLoadContext", "Polyfill"], packages);
    }

    static bool IsBuildArtifact(string path, string projectDirectory)
    {
        var relativePath = Path.GetRelativePath(projectDirectory, path);
        var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return firstSegment is "bin" or "obj";
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
