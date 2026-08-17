namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitRuntimeArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Runtime_session_exposes_a_nunit_free_neutral_runtime_contract()
    {
        var contract = typeof(DevTools.Testing.Abstractions.Runtime.ITestingRuntimeSession);

        Assert.Contains(contract, typeof(NUnitRuntimeSession).GetInterfaces());
        Assert.All(contract.GetMethods(), method =>
        {
            Assert.DoesNotContain("NUnit", method.ReturnType.FullName ?? string.Empty, StringComparison.Ordinal);
            Assert.All(method.GetParameters(), parameter =>
                Assert.DoesNotContain("NUnit", parameter.ParameterType.FullName ?? string.Empty, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void OnlyRuntimeProjectReferencesNUnitInProductionSource()
    {
        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "source"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}DevTools.NUnit.Runtime{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("<PackageReference Include=\"NUnit\"", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoProductionProjectReferencesNUnitEngine()
    {
        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "source"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("NUnit.Engine", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("source/DevTools.NUnit.Provider")]
    [InlineData("source/DevTools.TestRunner")]
    [InlineData("source/DevTools.NUnit.TestAdapter")]
    public void ForbiddenManualNUnitExecutionPatternsStayOutOfBoundary(string relativeProjectPath)
    {
        var projectDirectory = Path.Combine(RepositoryRoot, relativeProjectPath);
        var forbiddenPatterns = new[]
        {
            "MethodInfo.Invoke(",
            "Activator.CreateInstance(",
            "AssertionException",
            "InconclusiveException",
            "SuccessException",
            "NUnit.Framework.Internal",
            "NUnit.Engine",
        };

        var offenders = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, content: File.ReadAllText(path)))
            .SelectMany(file => forbiddenPatterns
                .Where(pattern => file.content.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepositoryRoot, file.path)} -> {pattern}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Runtime_DoesNotShipNUnitConsoleSelectionParser()
    {
        var runtimeDirectory = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime");
        Assert.False(File.Exists(Path.Combine(runtimeDirectory, "NUnitTestSelectionParser.cs")));

        var offenders = Directory
            .EnumerateFiles(runtimeDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => File.ReadAllText(path))
            .Where(content => content.Contains("NUnitTestSelectionParser", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void HostManualNUnitExecution_IsNotPresent()
    {
        var hostDirectory = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Host");
        var forbiddenPatterns = new[]
        {
            "MethodInfo.Invoke(",
            "Activator.CreateInstance(",
            "AssertionException",
            "InconclusiveException",
            "SuccessException",
        };

        var offenders = Directory
            .EnumerateFiles(hostDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsAllowedHostBootstrapPath(path))
            .Select(path => (path, content: File.ReadAllText(path)))
            .SelectMany(file => forbiddenPatterns
                .Where(pattern => file.content.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepositoryRoot, file.path)} -> {pattern}"))
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsAllowedHostBootstrapPath(string path)
    {
        // Collectible/no-context loaders must Activator.CreateInstance the Runtime session
        // type across the ALC boundary; that is not reflective NUnit lifecycle emulation.
        return path.EndsWith("NUnitRuntimeSessionFactory.cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("NetfxNUnitRuntimeSessionFactory.cs", StringComparison.OrdinalIgnoreCase);
    }
}
