using System.Text.RegularExpressions;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class RepositoryAssemblyLoadingArchitectureTests
{
    private static readonly string[] InputRoots = ["source", "libs", "build", "props"];
    private static readonly string[] RootInputs =
    [
        "RevitDevTool.slnx",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "global.json",
    ];
    private static readonly HashSet<string> InputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".props", ".targets", ".slnx", ".nuspec", ".json",
    };

    [Fact]
    public void Direct_assembly_loading_stays_in_the_kernel_or_an_explicit_bootstrap_or_plan_adapter()
    {
        var violations = new List<string>();

        foreach (var path in EnumerateRepositoryInputs())
        {
            var relativePath = Normalize(Path.GetRelativePath(RepositoryRoot, path));
            var content = File.ReadAllText(path);

            foreach (var pattern in DirectLoaderPatterns)
            {
                if (!Regex.IsMatch(content, pattern, RegexOptions.CultureInvariant))
                    continue;

                if (IsKernel(relativePath) || IsMtpBootstrapException(relativePath, content) || IsPlanAdapter(relativePath, content))
                    continue;

                violations.Add($"{relativePath} matches {pattern}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Direct assembly loading must use DevTools.AssemblyIsolation unless the explicit MTP bootstrap exception applies:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Mtp_bootstrap_exception_is_limited_to_the_private_runtime_closure()
    {
        const string relativePath = "source/DevTools.TestAdapter/RuntimeAssemblyResolver.cs";
        var content = File.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("Interlocked.Exchange(ref _registered, 1)", content, StringComparison.Ordinal);
        Assert.Contains("AppDomain.CurrentDomain.AssemblyResolve += ResolvePrivateRuntimeAssembly", content, StringComparison.Ordinal);
        Assert.Contains("Path.GetFullPath(AppContext.BaseDirectory)", content, StringComparison.Ordinal);
        Assert.Contains("Path.Combine(baseDirectory, name + \".dll\")", content, StringComparison.Ordinal);
        Assert.Contains("Assembly.LoadFrom(path)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.AssemblyIsolation", content, StringComparison.Ordinal);
        Assert.DoesNotContain("HostShared", content, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedAssembly", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Prefix", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Revit", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Acad", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Execution", content, StringComparison.Ordinal);
        Assert.Contains("AssemblyName.GetAssemblyName(path)", content, StringComparison.Ordinal);
        Assert.Contains("HasSameFullIdentity(requested, candidate)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Shipped_stub_generator_uses_the_kernel_without_framework_runtime_probing()
    {
        const string relativePath = "libs/pythonnet-stub-generator/csharp/PythonNetStubGenerator/StubBuilder.cs";
        var content = File.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("AssemblyLoader", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly.LoadFrom", content, StringComparison.Ordinal);
        Assert.DoesNotContain("AssemblyResolve +=", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.WindowsDesktop.App", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.NETCore.App", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.App", content, StringComparison.Ordinal);
    }

    private static readonly string[] DirectLoaderPatterns =
    [
        @"class\s+\w+\s*:\s*AssemblyLoadContext",
        @"AssemblyResolve\s*\+=",
        @"Assembly\.LoadFile\s*\(",
        @"Assembly\.LoadFrom\s*\(",
        @"Assembly\.Load\s*\(\s*(?:File\.ReadAllBytes|bytes)\b",
        @"new\s+MetadataLoadContext\s*\(",
        @"LoadFromStream\s*\(",
    ];

    private static bool IsKernel(string relativePath) =>
        relativePath.StartsWith("source/DevTools.AssemblyIsolation/", StringComparison.Ordinal);

    private static bool IsMtpBootstrapException(string relativePath, string content) =>
        (relativePath.Equals("source/DevTools.TestAdapter/RuntimeAssemblyResolver.cs", StringComparison.Ordinal)
         && content.Contains("AppContext.BaseDirectory", StringComparison.Ordinal)
         && content.Contains("Interlocked.Exchange(ref _registered, 1)", StringComparison.Ordinal))
        || (relativePath.Equals("source/DevTools.Testing.Abstractions/Loading/DiscoveryAssemblyLoad.cs", StringComparison.Ordinal)
            && content.Contains("DiscoveryRefs.Read", StringComparison.Ordinal)
            && content.Contains("DiscoveryLoadContext", StringComparison.Ordinal));

    private static bool IsPlanAdapter(string relativePath, string content) =>
        relativePath.Equals("source/DevTools.Execution/Providers/CSharp/CSharpCompiler.cs", StringComparison.Ordinal)
        && content.Contains("ScriptIsolationPlan.Create", StringComparison.Ordinal);

    private static IEnumerable<string> EnumerateRepositoryInputs()
    {
        foreach (var relativePath in RootInputs)
        {
            var path = Path.Combine(RepositoryRoot, relativePath);
            if (File.Exists(path))
                yield return path;
        }

        foreach (var relativeRoot in InputRoots)
        {
            var root = Path.Combine(RepositoryRoot, relativeRoot);
            if (!Directory.Exists(root))
                continue;

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(IsActiveInput))
                yield return path;
        }
    }

    private static bool IsActiveInput(string path) =>
        InputExtensions.Contains(Path.GetExtension(path))
        && !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj" or ".git" or ".superpowers" or "TestResults");

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string RepositoryRoot => MetadataAssemblySessionTests.FindRepositoryRoot();
}
