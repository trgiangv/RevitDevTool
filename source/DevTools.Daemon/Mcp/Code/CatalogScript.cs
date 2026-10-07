using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Code;

/// <summary>
/// Base class for a <c>code_mode</c> program. The model writes only the body of
/// <see cref="RunAsync"/>. Methods here are the program API, not MCP tools.
/// </summary>
public class CatalogScript
{
    private CatalogRuntime? _runtime;

    public CancellationToken CancellationToken { get; internal set; }

    public virtual Task<object?> RunAsync() => Task.FromResult<object?>(null);

    internal void Bind(CatalogRuntime runtime, CancellationToken cancellationToken)
    {
        _runtime = runtime;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// BM25F over the in-memory catalog. Default <paramref name="limit"/> is 8.
    /// Default <paramref name="primitiveType"/> is <c>tool</c>. An empty query returns nothing.
    /// </summary>
    public virtual Task<IReadOnlyList<MatchedPrimitive>> SearchAsync(
        string query,
        int? limit = null,
        int? processId = null,
        string? primitiveType = null)
    {
        var runtime = Runtime;
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<MatchedPrimitive>>([]);

        var kind = primitiveType is null ? CatalogType.Tool : ParsePrimitive(primitiveType);
        var matches = runtime.Catalog.Search(query, [kind], processId, limit ?? 8);
        IReadOnlyList<MatchedPrimitive> matched = matches
            .Select(match => new MatchedPrimitive(
                match.Item.Target,
                match.Item.Description,
                match.ProcessId,
                Wire(match.Item.Kind)))
            .ToArray();
        return Task.FromResult(matched);
    }

    public virtual Task<Tool> DescribeAsync(string name, int? processId = null)
    {
        var item = Resolve(name, processId, CatalogType.Tool);
        return Task.FromResult(item.Tool ?? throw new InvalidOperationException($"'{name}' has no tool declaration."));
    }

    public virtual async Task<CallToolResult> InvokeAsync(
        string name,
        object? arguments = null,
        int? processId = null)
    {
        var runtime = Runtime;
        var item = Resolve(name, processId, CatalogType.Tool);
        if (runtime.ReadOnly && item.Tool?.Annotations?.ReadOnlyHint != true)
            throw new InvalidOperationException($"'{name}' is not marked read-only.");

        var session = runtime.Session(item.ProcessId)
            ?? throw new InvalidOperationException($"Process {item.ProcessId} is not connected.");
        var outcome = await runtime.Gate(
            item.ProcessId,
            () => session.CallToolPassthroughAsync(
                new CallToolRequestParams
                {
                    Name = item.Target,
                    Arguments = ArgumentDictionary(arguments),
                },
                CancellationToken)).ConfigureAwait(false);

        return outcome as CallToolResult
            ?? throw new InvalidOperationException($"Host tools/call returned {outcome.GetType().Name}.");
    }

    public virtual async Task<ReadResourceResult> ReadAsync(
        string name,
        object? arguments = null,
        int? processId = null)
    {
        var runtime = Runtime;
        var matches = runtime.Catalog.FindMatches(name, processId);
        if (matches.Count > 0 && matches.All(item => item.Kind == CatalogType.Tool))
            throw new InvalidOperationException($"'{name}' is a tool. Use InvokeAsync.");

        var reads = matches.Where(item => item.Kind != CatalogType.Tool).ToArray();
        if (reads.Length == 0)
            throw new InvalidOperationException($"'{name}' was not found.");
        if (reads.Length > 1)
            throw new InvalidOperationException($"'{name}' matches more than one process. Pass processId.");

        var item = reads[0];
        var session = runtime.Session(item.ProcessId)
            ?? throw new InvalidOperationException($"Process {item.ProcessId} is not connected.");
        var args = ArgumentDictionary(arguments);
        return await runtime.Gate(item.ProcessId, () => item.Kind switch
        {
            CatalogType.Resource => session.ReadResourceAsync(item.Target, CancellationToken),
            CatalogType.ResourceTemplate => session.ReadResourceAsync(
                item.Target,
                args ?? new Dictionary<string, JsonElement>(),
                CancellationToken),
            _ => throw new InvalidOperationException($"'{name}' is a tool. Use InvokeAsync."),
        }).ConfigureAwait(false);
    }

    private CatalogRuntime Runtime =>
        _runtime ?? throw new InvalidOperationException("Catalog is not bound.");

    private CatalogItem Resolve(string name, int? processId, CatalogType kind)
    {
        var matches = Runtime.Catalog.FindMatches(name, processId, kind);
        if (matches.Count == 0)
            throw new InvalidOperationException($"'{name}' was not found.");
        if (matches.Count > 1)
            throw new InvalidOperationException($"'{name}' matches more than one process. Pass processId.");
        return matches[0];
    }

    private static CatalogType ParsePrimitive(string primitiveType) =>
        CatalogTypeCodec.TryParse(primitiveType, out var kind)
            ? kind
            : throw new ArgumentException(
                $"primitiveType must be tool, resource, or resource_template. Got '{primitiveType}'.",
                nameof(primitiveType));

    private static string Wire(CatalogType kind) => kind switch
    {
        CatalogType.Tool => "tool",
        CatalogType.Resource => "resource",
        CatalogType.ResourceTemplate => "resource_template",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static Dictionary<string, JsonElement>? ArgumentDictionary(object? arguments)
    {
        if (arguments is null)
            return null;
        if (arguments is Dictionary<string, JsonElement> ready)
            return ready;

        var element = JsonSerializer.SerializeToElement(arguments);
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("arguments must be an object.");

        return element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
    }
}
