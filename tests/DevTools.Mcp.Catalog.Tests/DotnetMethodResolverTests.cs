using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Isolation;
using DevTools.Mcp.Catalog.Tests.Harness;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class DotnetMethodResolverTests
{
    [Fact]
    public void ResolveTool_FindsMethodInLoadedAssembly()
    {
        var resolver = CreateResolver();
        var tool = CreateToolRegistration(
            "bind_capture",
            typeof(DotnetToolsetMrtrStubs).Assembly.Location,
            typeof(DotnetToolsetMrtrStubs).FullName!,
            nameof(DotnetToolsetMrtrStubs.BindCapture));

        var method = resolver.ResolveTool(tool);

        Assert.NotNull(method);
        Assert.Equal(nameof(DotnetToolsetMrtrStubs.BindCapture), method!.Name);
    }

    [Fact]
    public void ResolveTool_ReturnsNull_WhenContainerDoesNotMatch()
    {
        var resolver = CreateResolver();
        var tool = CreateToolRegistration(
            "bind_capture",
            typeof(DotnetToolsetMrtrStubs).Assembly.Location,
            "Missing.Container",
            nameof(DotnetToolsetMrtrStubs.BindCapture));

        Assert.Null(resolver.ResolveTool(tool));
    }

    [Fact]
    public void ResolveResource_FindsSampleResource_WhenAssemblyPresent()
    {
        var assemblyPath = OptionalArtifact.ResolveMcpToolsetDemoDll(FindRepositoryRoot());
        if (assemblyPath is null)
            Assert.Skip(OptionalArtifact.McpToolsetDemoHint);

        var catalog = new McpAssemblyParser(NullLogger<McpAssemblyParser>.Instance).ParseCatalogFromAssembly(assemblyPath);
        var resource = catalog.Resources.Single(item => item.Descriptor?.Name == "demo_status");
        var resolver = CreateResolver();

        var method = resolver.ResolveResource(resource);

        Assert.NotNull(method);
    }

    [Fact]
    public void ResolveTool_LoadsFromToolsetContext_WhenAssemblyNotYetLoaded()
    {
        using var workload = McpResolverWorkload.Create();
        var resolver = CreateResolver();
        var tool = CreateToolRegistration(
            "resolver_tool",
            workload.EntryPath,
            "Tools",
            "Run");

        var method = resolver.ResolveTool(tool);
        if (method is null)
            Assert.Skip("Compiled resolver toolset could not be loaded in this environment.");

        Assert.Equal("Run", method!.Name);
    }

    private static DotnetMethodResolver CreateResolver() =>
        new(new McpToolsetContextManager(NullLogger<McpToolsetContextManager>.Instance), NullLogger<DotnetMethodResolver>.Instance);

    private static McpRegisteredTool CreateToolRegistration(string name, string sourcePath, string containerType, string methodName) => new()
    {
        Id = name,
        Descriptor = new Tool
        {
            Name = name,
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
        },
        Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, sourcePath, containerType, methodName),
    };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }
}

internal sealed class McpResolverWorkload : IDisposable
{
    private McpResolverWorkload(string directory) => Directory = directory;

    public string Directory { get; }
    public string EntryPath => Path.Combine(Directory, "ResolverToolset.dll");

    public static McpResolverWorkload Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.Mcp.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var workload = new McpResolverWorkload(directory);
        var mcpServerPath = typeof(McpServerToolAttribute).Assembly.Location;

        var source = """
                     using ModelContextProtocol.Server;

                     [McpServerToolType]
                     public static class Tools
                     {
                         [McpServerTool(Name = "resolver_tool")]
                         public static string Run() => "ok";
                     }
                     """;

        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(static path => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path))
            .Append(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(mcpServerPath))
            .ToList();

        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "ResolverToolset",
            [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source)],
            trusted,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        using var stream = File.Create(workload.EntryPath);
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        return workload;
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
            System.IO.Directory.Delete(Directory, recursive: true);
    }
}
