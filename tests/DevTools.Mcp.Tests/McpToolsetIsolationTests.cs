using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using DevTools.AssemblyIsolation;
using DevTools.Mcp.Catalog.Discovery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Tests;

public sealed class McpToolsetIsolationTests
{
    [Fact]
    public void Isolation_plan_binds_the_exact_parent_mcp_contract_assemblies()
    {
        using var workload = McpToolsetWorkload.Create("contract", "private");

        var plan = McpToolsetIsolationPlan.Create(workload.EntryPath);

        Assert.True(plan.ParentBindings.TryResolve(typeof(McpServer).Assembly.GetName(), out var server));
        Assert.True(plan.ParentBindings.TryResolve(typeof(CallToolResult).Assembly.GetName(), out var protocol));
        Assert.Same(typeof(McpServer).Assembly, server);
        Assert.Same(typeof(CallToolResult).Assembly, protocol);
        Assert.Equal(AssemblyIsolationLifecycle.Collectible, plan.Lifecycle);
        Assert.Equal(2, plan.ManagedSources.Count);
    }

    [Fact]
    public void Toolset_loads_its_private_microsoft_extensions_version_instead_of_the_parent_version()
    {
        using var workload = McpToolsetWorkload.Create("version", "private-v1", "parent-v2");
        _ = Assembly.Load(File.ReadAllBytes(workload.ParentDependencyPath));

        using var context = new McpToolsetContext(workload.EntryPath, NullLogger.Instance);
        var value = InvokeValue(context.LoadAssembly());

        Assert.Equal("private-v1", value);
    }

    [Fact]
    public void Toolset_load_does_not_run_an_unrequested_sibling_initializer()
    {
        using var workload = McpToolsetWorkload.Create("lazy", "private");

        using var context = new McpToolsetContext(workload.EntryPath, NullLogger.Instance);
        _ = context.LoadAssembly();

        Assert.False(File.Exists(workload.SiblingInitializerMarkerPath));
    }

    [Fact]
    public void Toolset_context_forwards_kernel_resolution_diagnostics_to_the_mcp_logger()
    {
        using var workload = McpToolsetWorkload.Create("diagnostics", "private");
        File.Delete(Path.Combine(workload.Directory, "Microsoft.Extensions.IsolationFixture.dll"));
        var logger = new RecordingLogger();

        using var context = new McpToolsetContext(workload.EntryPath, logger);
        var assembly = context.LoadAssembly();

        var failure = Assert.Throws<TargetInvocationException>(() => InvokeValue(assembly));
        Assert.IsType<FileNotFoundException>(failure.InnerException);
        Assert.Contains(logger.Messages, message => message.Contains("[managed-clr-fallback]", StringComparison.Ordinal));
    }

    [Fact]
    public void Toolset_context_manager_releases_collectible_context_after_cached_dispatch_references_are_cleared()
    {
        using var workload = McpToolsetWorkload.Create("unload", "private");
        var manager = new McpToolsetContextManager(NullLogger<McpToolsetContextManager>.Instance);
        var dispatcherCache = new List<MethodInfo>();

        var contextReference = LoadCacheAndClear(manager, workload.EntryPath, dispatcherCache);

        for (var attempt = 0; attempt < 10 && contextReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(contextReference.IsAlive, "The toolset ALC remained alive after cache clear and context disposal.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadCacheAndClear(
        McpToolsetContextManager manager,
        string entryPath,
        List<MethodInfo> dispatcherCache)
    {
        var assembly = manager.GetOrCreate(entryPath).LoadAssembly();
        dispatcherCache.Add(assembly.GetType("Fixture.Entry", throwOnError: true)!.GetMethod("Value")!);
        var contextReference = new WeakReference(AssemblyLoadContext.GetLoadContext(assembly)!);

        dispatcherCache.Clear();
        manager.Clear();

        return contextReference;
    }

    private static string InvokeValue(Assembly assembly) => (string)assembly
        .GetType("Fixture.Entry", throwOnError: true)!
        .GetMethod("Value", BindingFlags.Public | BindingFlags.Static)!
        .Invoke(null, null)!;

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}

internal sealed class McpToolsetWorkload : IDisposable
{
    private McpToolsetWorkload(string directory, string markerPath)
    {
        Directory = directory;
        SiblingInitializerMarkerPath = markerPath;
    }

    public string Directory { get; }
    public string EntryPath => Path.Combine(Directory, "Toolset.dll");
    public string ParentDependencyPath => Path.Combine(Directory, "parent", "Microsoft.Extensions.IsolationFixture.dll");
    public string SiblingInitializerMarkerPath { get; }

    public static McpToolsetWorkload Create(string suffix, string privateValue, string? parentValue = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Mcp.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var workload = new McpToolsetWorkload(directory, Path.Combine(directory, "sibling-initializer-ran.txt"));
        var privateDependency = Path.Combine(directory, "Microsoft.Extensions.IsolationFixture.dll");
        var parentDirectory = Path.GetDirectoryName(workload.ParentDependencyPath)!;
        System.IO.Directory.CreateDirectory(parentDirectory);

        Compile(privateDependency, "Microsoft.Extensions.IsolationFixture", privateValue, new Version(1, 0, 0, 0));
        if (parentValue is not null)
            Compile(workload.ParentDependencyPath, "Microsoft.Extensions.IsolationFixture", parentValue, new Version(2, 0, 0, 0));

        Compile(
            workload.EntryPath,
            $"McpToolsetFixture_{suffix}",
            "namespace Fixture { public static class Entry { public static string Value() => Microsoft.Extensions.IsolationFixture.Value.Text; } }",
            new Version(1, 0, 0, 0),
            [privateDependency]);

        Compile(
            Path.Combine(directory, "UnusedSibling.dll"),
            $"UnusedSibling_{suffix}",
            $"[assembly:System.Reflection.AssemblyVersion(\"1.0.0.0\")] public static class Sibling {{ [System.Runtime.CompilerServices.ModuleInitializer] public static void Initialize() => System.IO.File.WriteAllText(@\"{workload.SiblingInitializerMarkerPath}\", \"ran\"); }}",
            new Version(1, 0, 0, 0));

        return workload;
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
            System.IO.Directory.Delete(Directory, recursive: true);
    }

    private static void Compile(string path, string assemblyName, string value, Version version, IEnumerable<string>? references = null)
    {
        var source = value.StartsWith("[assembly:", StringComparison.Ordinal)
            ? value
            : value.Contains("namespace Fixture", StringComparison.Ordinal)
                ? $"[assembly:System.Reflection.AssemblyVersion(\"{version}\")] {value}"
            : $"[assembly:System.Reflection.AssemblyVersion(\"{version}\")] namespace Microsoft.Extensions.IsolationFixture {{ public static class Value {{ public static string Text => \"{value}\"; }} }}";
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(static trustedPath => MetadataReference.CreateFromFile(trustedPath)).ToList();
        if (references is not null)
            trusted.AddRange(references.Select(static referencePath => MetadataReference.CreateFromFile(referencePath)));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            trusted,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = File.Create(path);
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    }
}
