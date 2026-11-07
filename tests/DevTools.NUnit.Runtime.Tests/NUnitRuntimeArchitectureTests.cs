namespace DevTools.NUnit.Runtime.Tests;

[TestClass]
public sealed class NUnitRuntimeArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [TestMethod]
    public void Session_contract_has_no_nunit_types()
    {
        var contract = typeof(DevTools.Testing.Abstractions.Runtime.ITestingRuntimeSession);

        Assert.IsTrue(typeof(NUnitRuntimeSession).GetInterfaces().Contains(contract));
        foreach (var method in contract.GetMethods())
        {
            Assert.DoesNotContain("NUnit", method.ReturnType.FullName ?? string.Empty, StringComparison.Ordinal);
            foreach (var parameter in method.GetParameters())
                Assert.DoesNotContain("NUnit", parameter.ParameterType.FullName ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void Only_runtime_projects_reference_NUnit()
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

        Assert.IsEmpty(offenders);
    }

    [TestMethod]
    public void No_project_references_NUnit_Engine()
    {
        var offenders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "source"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("NUnit.Engine", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.IsEmpty(offenders);
    }

    [TestMethod]
    [DataRow("source/DevTools.TestAdapter")]
    [DataRow("source/DevTools.TestRunner")]
    public void Adapter_never_runs_NUnit_by_hand(string relativeProjectPath)
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

        Assert.IsEmpty(offenders);
    }

    [TestMethod]
    public void Runtime_uses_NUnitAssemblyBuilder()
    {
        var sessionPath = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime", "NUnitRuntimeSession.cs");
        var builderPath = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime", "NUnitAssemblyBuilder.cs");
        Assert.IsTrue(File.Exists(builderPath));
        var session = File.ReadAllText(sessionPath);
        Assert.Contains("new NUnitAssemblyBuilder()", session, StringComparison.Ordinal);
        Assert.DoesNotContain("new DefaultTestAssemblyBuilder()", session, StringComparison.Ordinal);
        Assert.Contains("ReflectionTypeLoadException", File.ReadAllText(builderPath), StringComparison.Ordinal);
        Assert.Contains("DefaultWorkDirectory", File.ReadAllText(builderPath), StringComparison.Ordinal);
        Assert.Contains("ApplyBuilderOptions", File.ReadAllText(builderPath), StringComparison.Ordinal);

        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.MTP");
        Assert.Contains(
            "NUnitAssemblyBuilder.cs",
            File.ReadAllText(Path.Combine(mtpDir, "DevTools.NUnit.MTP.csproj")),
            StringComparison.Ordinal);
        Assert.Contains(
            "new NUnitAssemblyBuilder()",
            File.ReadAllText(Path.Combine(mtpDir, "NUnitTestDiscoverer.cs")),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void Runtime_uses_shared_trace_scope()
    {
        var runtimeDirectory = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime");
        var listener = File.ReadAllText(Path.Combine(runtimeDirectory, "NUnitEventListener.cs"));
        var session = File.ReadAllText(Path.Combine(runtimeDirectory, "NUnitRuntimeSession.cs"));
        Assert.Contains("TestRunTraceScope", listener, StringComparison.Ordinal);
        Assert.Contains("TestRunTraceScope.Merge", listener, StringComparison.Ordinal);
        Assert.Contains("new TestRunTraceScope(", session, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Test_id_is_nunit_full_name()
    {
        var identity = File.ReadAllText(Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Runtime", "NUnitTestIdentity.cs"));
        Assert.Contains("test.FullName", identity, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Host_never_runs_NUnit_by_hand()
    {
        var hostDirectory = Path.Combine(RepositoryRoot, "source", "DevTools.Testing.Host", "NUnit");
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
            .Select(path => (path, content: File.ReadAllText(path)))
            .SelectMany(file => forbiddenPatterns
                .Where(pattern => file.content.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepositoryRoot, file.path)} -> {pattern}"))
            .ToList();

        Assert.IsEmpty(offenders);
    }
}
