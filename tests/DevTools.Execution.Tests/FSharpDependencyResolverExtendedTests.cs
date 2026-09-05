using DevTools.Execution.Abstractions;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[Collection(nameof(NugetRestoreCollection))]
public sealed class FSharpDependencyResolverExtendedTests
{
    [Fact]
    public async Task ResolveAsync_WithNugetPackage_AddsPackageDlls()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-nuget-resolve");
        var scriptPath = Path.Combine(directory, "nuget_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            "#r \"nuget: Newtonsoft.Json, 13.0.3\"",
            TestContext.Current.CancellationToken);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(
            NullLogger<FSharpDependencyResolver>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var resolution = await resolver.ResolveAsync(
                scriptPath,
                graph,
                ExecutionTestHelpers.CreateScriptBridge(),
                ct: TestContext.Current.CancellationToken);

            Assert.NotEmpty(resolution.References);
            Assert.Contains(resolution.References, path => Path.GetFileName(path).Equals("Newtonsoft.Json.dll", StringComparison.OrdinalIgnoreCase));
            resolution.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WithHostReference_AddsResolvedDll()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-host-ref");
        var hostDllDir = Path.Combine(directory, "Revit 2025");
        Directory.CreateDirectory(hostDllDir);
        var dependencyPath = Path.Combine(hostDllDir, "RevitAPI.dll");
        await File.WriteAllBytesAsync(dependencyPath, [0x4D, 0x5A], TestContext.Current.CancellationToken);

        var scriptPath = Path.Combine(directory, "entry_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            $"#r @\"{dependencyPath.Replace('\\', '/')}\"\nlet x = 1",
            TestContext.Current.CancellationToken);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(
            NullLogger<FSharpDependencyResolver>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var resolution = await resolver.ResolveAsync(
                scriptPath,
                graph,
                ExecutionTestHelpers.CreateScriptBridge(),
                ct: TestContext.Current.CancellationToken);

            Assert.NotEmpty(resolution.References);
            Assert.Contains(
                resolution.References,
                path => Path.GetFullPath(path).Equals(Path.GetFullPath(dependencyPath), StringComparison.OrdinalIgnoreCase));
            resolution.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WithFileReferenceOutsideHostYear_AddsResolvedDll()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-file-ref");
        var dependencyPath = Path.Combine(directory, "helper.dll");
        await File.WriteAllBytesAsync(dependencyPath, [0x4D, 0x5A], TestContext.Current.CancellationToken);

        var scriptPath = Path.Combine(directory, "entry_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            $"#r @\"{dependencyPath.Replace('\\', '/')}\"\nlet x = 1",
            TestContext.Current.CancellationToken);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(
            NullLogger<FSharpDependencyResolver>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var resolution = await resolver.ResolveAsync(
                scriptPath,
                graph,
                ExecutionTestHelpers.CreateScriptBridge(),
                ct: TestContext.Current.CancellationToken);

            Assert.Contains(
                resolution.References,
                path => Path.GetFullPath(path).Equals(Path.GetFullPath(dependencyPath), StringComparison.OrdinalIgnoreCase));
            resolution.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_RewrittenGraph_EmitsLineDirectiveToOriginalScript()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-line-directive");
        var dependencyPath = Path.Combine(directory, "helper.dll");
        await File.WriteAllBytesAsync(dependencyPath, [0x4D, 0x5A], TestContext.Current.CancellationToken);

        var scriptPath = Path.Combine(directory, "entry_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            $"#r @\"{dependencyPath.Replace('\\', '/')}\"\nlet x = 1",
            TestContext.Current.CancellationToken);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(
            NullLogger<FSharpDependencyResolver>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var resolution = await resolver.ResolveAsync(
                scriptPath,
                graph,
                ExecutionTestHelpers.CreateScriptBridge(),
                ct: TestContext.Current.CancellationToken);

            Assert.False(
                string.Equals(
                    Path.GetFullPath(scriptPath),
                    Path.GetFullPath(resolution.ScriptPath),
                    StringComparison.OrdinalIgnoreCase));

            var rewritten = await File.ReadAllLinesAsync(resolution.ScriptPath, TestContext.Current.CancellationToken);
            Assert.StartsWith("#line 1 \"", rewritten[0], StringComparison.Ordinal);
            Assert.Contains(scriptPath.Replace('\\', '/'), rewritten[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("let x = 1", rewritten);

            resolution.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_RewritesHostYear_ViaBridge()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-host-rewrite");
        var oldDir = Path.Combine(directory, "Revit 2024");
        var newDir = Path.Combine(directory, "Revit 2025");
        Directory.CreateDirectory(oldDir);
        Directory.CreateDirectory(newDir);
        var oldDll = Path.Combine(oldDir, "RevitAPI.dll");
        var newDll = Path.Combine(newDir, "RevitAPI.dll");
        await File.WriteAllBytesAsync(oldDll, [0x4D, 0x5A], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(newDll, [0x4D, 0x5A], TestContext.Current.CancellationToken);

        var scriptPath = Path.Combine(directory, "entry_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            $"#r @\"{oldDll.Replace('\\', '/')}\"\nlet x = 1",
            TestContext.Current.CancellationToken);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(
            NullLogger<FSharpDependencyResolver>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var resolution = await resolver.ResolveAsync(
                scriptPath,
                graph,
                new YearRewriteBridge("Revit 2024", "Revit 2025"),
                ct: TestContext.Current.CancellationToken);

            Assert.Contains(
                resolution.References,
                path => Path.GetFullPath(path).Equals(Path.GetFullPath(newDll), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                resolution.References,
                path => Path.GetFullPath(path).Equals(Path.GetFullPath(oldDll), StringComparison.OrdinalIgnoreCase));
            resolution.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WithConflictingNugetVersions_Throws()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-nuget-conflict");
        var first = Path.Combine(directory, "first.fsx");
        var second = Path.Combine(directory, "second.fsx");
        var entry = Path.Combine(directory, "entry_script.fsx");
        await File.WriteAllTextAsync(first, "#r \"nuget: Newtonsoft.Json, 13.0.1\"", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(second, "#r \"nuget: Newtonsoft.Json, 13.0.3\"", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(entry, $"#load @\"{first.Replace('\\', '/')}\"\n#load @\"{second.Replace('\\', '/')}\"", TestContext.Current.CancellationToken);

        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(entry, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(
            NullLogger<FSharpDependencyResolver>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => resolver.ResolveAsync(entry, graph, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.Current.CancellationToken));

            Assert.Contains("version conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_ReportsProgressForPackageResolution()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-progress");
        var scriptPath = Path.Combine(directory, "progress_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "#r \"nuget: Newtonsoft.Json, 13.0.3\"", TestContext.Current.CancellationToken);
        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(
            NullLogger<FSharpDependencyResolver>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));
        var messages = new List<string>();

        try
        {
            var resolution = await resolver.ResolveAsync(
                scriptPath,
                graph,
                ExecutionTestHelpers.CreateScriptBridge(),
                progress: new Progress<string>(messages.Add),
                ct: TestContext.Current.CancellationToken);

            Assert.Contains(messages, message => message.Contains("NuGet", StringComparison.OrdinalIgnoreCase));
            resolution.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class YearRewriteBridge(string fromToken, string toToken) : ICompiledScriptBridge
    {
        public IEnumerable<System.Reflection.Assembly> GetParentBindings() => [];
        public Type? TryFindCommandType(System.Reflection.Assembly assembly) => null;
        public string RewriteHostReference(string reference) =>
            reference.Replace(fromToken, toToken, StringComparison.OrdinalIgnoreCase);
    }
}
