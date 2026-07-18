using System.Collections.Concurrent;
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Registry;

public sealed record McpCatalogDiagnostic(
    string Code,
    string Kind,
    string Key,
    string Provider,
    string Message);

public sealed record McpPrimitiveSnapshot(
    IReadOnlyList<McpServerTool> Tools,
    IReadOnlyList<McpServerPrompt> Prompts,
    IReadOnlyList<McpServerResource> Resources,
    IReadOnlyList<McpCatalogDiagnostic> Diagnostics)
{
    public static McpPrimitiveSnapshot Empty { get; } = new([], [], [], []);
}

public sealed record McpCatalogLoadResult(McpRegistryCatalog Catalog, McpPrimitiveSnapshot Snapshot);

/// <summary>Builds SDK primitives without making the shared MCP library depend on host execution code.</summary>
public interface IMcpServerPrimitiveAdapter
{
    ExecutionMode SourceKind { get; }
    McpServerTool? CreateTool(McpRegisteredTool registration);
    McpServerPrompt? CreatePrompt(McpRegisteredPrompt registration);
    McpServerResource? CreateResource(McpRegisteredResource registration);
}

/// <summary>Provides SDK primitives already bound to built-in host services.</summary>
public interface IMcpServerPrimitiveProvider
{
    McpServerTool? CreateTool(McpRegisteredTool registration);
    McpServerPrompt? CreatePrompt(McpRegisteredPrompt registration);
    McpServerResource? CreateResource(McpRegisteredResource registration);
}

public sealed class DotnetMcpServerPrimitiveAdapter(
    DotnetMethodResolver methodResolver,
    IServiceProvider serviceProvider,
    IMcpHostExecution hostExecution) : IMcpServerPrimitiveAdapter
{
    private readonly ConcurrentDictionary<string, McpServerTool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerPrompt> _prompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerResource> _resources = new(StringComparer.OrdinalIgnoreCase);

    public ExecutionMode SourceKind => ExecutionMode.Dotnet;

    public McpServerTool? CreateTool(McpRegisteredTool registration) => Wrap(DotnetMcpServerFactory.GetOrCreate(
        _tools, registration.Id, registration, methodResolver.ResolveTool, serviceProvider,
        (method, target) => McpServerTool.Create(method, target)));

    public McpServerPrompt? CreatePrompt(McpRegisteredPrompt registration) => Wrap(DotnetMcpServerFactory.GetOrCreate(
        _prompts, registration.Id, registration, methodResolver.ResolvePrompt, serviceProvider,
        (method, target) => McpServerPrompt.Create(method, target)));

    public McpServerResource? CreateResource(McpRegisteredResource registration) => Wrap(DotnetMcpServerFactory.GetOrCreate(
        _resources, registration.Id, registration, methodResolver.ResolveResource, serviceProvider,
        (method, target) => McpServerResource.Create(method, target)));

    private McpServerTool? Wrap(McpServerTool? primitive) => primitive is null ? null : McpHostExecutionPrimitives.Wrap(primitive, hostExecution);
    private McpServerPrompt? Wrap(McpServerPrompt? primitive) => primitive is null ? null : McpHostExecutionPrimitives.Wrap(primitive, hostExecution);
    private McpServerResource? Wrap(McpServerResource? primitive) => primitive is null ? null : McpHostExecutionPrimitives.Wrap(primitive, hostExecution);
}
