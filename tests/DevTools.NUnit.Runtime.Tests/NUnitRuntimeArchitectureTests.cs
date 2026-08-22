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
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DevTools.NUnit.Runtime.csproj",
            "DevTools.NUnit.MTP.csproj",
        };
        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "source"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !allowed.Contains(Path.GetFileName(path)))
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
    [InlineData("source/DevTools.TestAdapter")]
    [InlineData("source/DevTools.TestRunner")]
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
    public void HostRuntimeUsesTolerantAssemblyBuilderInsteadOfDefaultGetTypes()
    {
        var sessionPath = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime", "NUnitRuntimeSession.cs");
        var builderPath = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime", "NUnitTolerantAssemblyBuilder.cs");
        Assert.True(File.Exists(builderPath));
        var session = File.ReadAllText(sessionPath);
        Assert.Contains("new NUnitTolerantAssemblyBuilder()", session, StringComparison.Ordinal);
        Assert.DoesNotContain("new DefaultTestAssemblyBuilder()", session, StringComparison.Ordinal);
        Assert.Contains("ReflectionTypeLoadException", File.ReadAllText(builderPath), StringComparison.Ordinal);
        Assert.Contains("DefaultWorkDirectory", File.ReadAllText(builderPath), StringComparison.Ordinal);
        Assert.Contains("ApplyBuilderOptions", File.ReadAllText(builderPath), StringComparison.Ordinal);

        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.MTP");
        Assert.False(File.Exists(Path.Combine(mtpDir, "NUnitLocalAssemblyBuilder.cs")));
        Assert.Contains(
            "NUnitTolerantAssemblyBuilder.cs",
            File.ReadAllText(Path.Combine(mtpDir, "DevTools.NUnit.MTP.csproj")),
            StringComparison.Ordinal);
        Assert.Contains(
            "new NUnitTolerantAssemblyBuilder()",
            File.ReadAllText(Path.Combine(mtpDir, "NUnitHostTestDiscoverer.cs")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_uses_shared_testing_run_trace_scope()
    {
        var runtimeDirectory = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime");
        Assert.False(File.Exists(Path.Combine(runtimeDirectory, "NUnitRunTraceScope.cs")));

        var listener = File.ReadAllText(Path.Combine(runtimeDirectory, "NUnitEventListener.cs"));
        var session = File.ReadAllText(Path.Combine(runtimeDirectory, "NUnitRuntimeSession.cs"));
        Assert.Contains("TestingRunTraceScope", listener, StringComparison.Ordinal);
        Assert.Contains("TestingRunTraceScope.Merge", listener, StringComparison.Ordinal);
        Assert.Contains("new TestingRunTraceScope()", session, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitRunTraceScope", listener, StringComparison.Ordinal);
        Assert.DoesNotContain("NUnitRunTraceScope", session, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_uses_nunit_full_name_as_test_id()
    {
        var runtimeDirectory = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime");
        var identity = File.ReadAllText(Path.Combine(runtimeDirectory, "NUnitTestIdentity.cs"));
        Assert.Contains("test.FullName", identity, StringComparison.Ordinal);
        Assert.DoesNotContain("FormChildId", identity, StringComparison.Ordinal);

        var offenders = Directory
            .EnumerateFiles(runtimeDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, content: File.ReadAllText(path)))
            .Where(file => file.content.Contains("FormChildId", StringComparison.Ordinal)
                           || file.content.Contains("NUnitTestIdentityRegistry", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file.path))
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
        return path.EndsWith("NUnitRuntimeSessionFactory.cs", StringComparison.OrdinalIgnoreCase);
    }
}
