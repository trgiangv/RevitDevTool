using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using DevTools.Daemon.Mcp.Contracts;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

/// <summary>Fixed external tool that invokes opaque catalog locators against the current host session.</summary>
public sealed class InvokeTool(IProcessSessions sessions)
{
    public static McpServerTool Create(IProcessSessions sessions) => McpServerTool.Create(
        new InvokeTool(sessions).Invoke,
        new McpServerToolCreateOptions
        {
            Name = McpSpecKeys.Tool.Invoke,
            Description = "Invoke an id from search_dynamic, or batch-read resources. Re-search once before retrying a stale id.",
            Destructive = true,
            OpenWorld = true,
            SerializerOptions = McpToolJson.Options
        });

    [Description("Invoke one id from search_dynamic, or batch-read resource ids.")]
    private async Task<CallToolResult> Invoke(
        RequestContext<CallToolRequestParams> context,
        string? id = null,
        Dictionary<string, JsonElement>? arguments = null,
        ResourceReadRequest[]? reads = null,
        CancellationToken cancellationToken = default)
    {
        var request = new InvokeRequest(
            id,
            arguments is null ? null : JsonSerializer.SerializeToElement(arguments, McpServerJsonContext.Default.DictionaryStringJsonElement),
            reads);

        var problems = InvokeValidator.Validate(request);
        if (problems.Count > 0)
            return ToolResults.Error(
                "validation_error",
                string.Join(" ", problems.Select(problem => $"{problem.Name}: {problem.Message}")));

        if (request.Reads is { Count: > 0 })
            return await InvokeReadsAsync(request.Reads, cancellationToken).ConfigureAwait(false);

        var host = await InvokeSingleAsync(context, request.Id!, request.Arguments, cancellationToken).ConfigureAwait(false);
        if (host.InputRequired is not null)
            throw new InputRequiredException(ForwardInputRequired(request.Id!, request.Arguments, host.InputRequired));

        return ToCallToolResult(host.Response!);
    }

    private static InputRequiredResult ForwardInputRequired(
        string id,
        JsonElement? arguments,
        InputRequiredResult hostResult)
    {
        var state = new InvokeState(id, arguments, hostResult.RequestState);
        return new InputRequiredResult
        {
            InputRequests = hostResult.InputRequests,
            RequestState = state.Serialize(),
        };
    }

    private static CallToolResult ToCallToolResult(InvokeResponse response)
    {
        if (!response.Ok)
            return ToolResults.Result(response, McpServerJsonContext.Default.InvokeResponse);

        return ToHostCallToolResult(response.Result);
    }

    private static CallToolResult ToHostCallToolResult(object? result)
    {
        if (result is CallToolResult toolResult)
            return toolResult;

        if (result is ReadResourceResult resourceResult)
            return new CallToolResult
            {
                Content = resourceResult.Contents
                    .Select(static resource => new EmbeddedResourceBlock { Resource = resource })
                    .Cast<ContentBlock>()
                    .ToList()
            };

        return ToolResults.Result(new InvokeResponse(true, true, result), McpServerJsonContext.Default.InvokeResponse);
    }

    private async Task<CallToolResult> InvokeReadsAsync(IReadOnlyList<ResourceReadRequest> reads, CancellationToken ct)
    {
        var results = new List<ResourceReadResult>();
        var budget = InvokeValidator.DefaultResultBudgetBytes;
        var used = Utf8Size("{\"ok\":true,\"executionStarted\":true,\"results\":[]}");
        foreach (var (read, index) in reads.Select((value, index) => (value, index)))
        {
            var item = await InvokeSingleAsync(null, read.Id!, ToElement(read.Arguments), ct).ConfigureAwait(false);
            var resourceResult = new ResourceReadResult(index, item.Response!.Ok, item.Response.Result, item.Response.Error);
            var itemBytes = PackedUtf8Size(resourceResult);
            if (itemBytes > InvokeValidator.HardResultBudgetBytes || itemBytes > budget)
            {
                results.Add(new ResourceReadResult(index, false, null,
                    new InvocationError("result_too_large", "The complete item exceeds the result budget.")));
                continue;
            }
            if (used + itemBytes > budget)
                break;
            results.Add(resourceResult);
            used += itemBytes;
        }
        return ToolResults.Result(new InvokeResponse(true, true, Results: results), McpServerJsonContext.Default.InvokeResponse);
    }

    private async Task<HostResult> InvokeSingleAsync(
        RequestContext<CallToolRequestParams>? context,
        string id,
        JsonElement? arguments,
        CancellationToken ct)
    {
        var precheck = CatalogResolver.Resolve(sessions, id);
        if (precheck.Error is not null)
            return HostResult.FromResponse(precheck.Error);

        var state = InvokeState.TryParse(context?.Params.RequestState);
        var invocationArguments = ToArguments(state?.Arguments) ?? ToArguments(arguments);
        try
        {
            return await InvokeResolvedAsync(precheck, context, state, invocationArguments, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return HostResult.FromResponse(new InvokeResponse(false, true, Error: new InvocationError("invocation_canceled", "Invocation was canceled or timed out.")));
        }
        catch (Exception ex)
        {
            return HostResult.FromResponse(new InvokeResponse(false, true, Error: new InvocationError("invocation_failed", ex.Message)));
        }
    }

    private static async Task<HostResult> InvokeResolvedAsync(
        CatalogLookup precheck,
        RequestContext<CallToolRequestParams>? context,
        InvokeState? state,
        Dictionary<string, JsonElement>? invocationArguments,
        CancellationToken ct)
    {
        if (precheck.Id!.Kind is CatalogType.Tool)
            return await InvokeToolAsync(precheck.Session!, precheck.Id.Target, context, state, invocationArguments, ct).ConfigureAwait(false);

        var result = await ReadCatalogItemAsync(precheck.Session!, precheck.Id, invocationArguments, ct).ConfigureAwait(false);
        return HostResult.FromResponse(new InvokeResponse(true, true, result));
    }

    private static async Task<HostResult> InvokeToolAsync(
        IProcessSession session,
        string toolName,
        RequestContext<CallToolRequestParams>? context,
        InvokeState? state,
        Dictionary<string, JsonElement>? invocationArguments,
        CancellationToken ct)
    {
        var hostParams = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = invocationArguments,
            InputResponses = context?.Params.InputResponses,
            RequestState = state?.HostRequestState,
        };
        var outcome = await session.CallToolPassthroughAsync(hostParams, ct).ConfigureAwait(false);
        if (outcome is InputRequiredResult inputRequired)
            return HostResult.FromInputRequired(inputRequired);
        if (outcome is not CallToolResult toolResult)
            throw new InvalidOperationException($"Host tools/call returned {outcome.GetType().Name}.");

        return HostResult.FromResponse(new InvokeResponse(true, true, toolResult));
    }

    private static async Task<object> ReadCatalogItemAsync(
        IProcessSession session,
        CatalogId locator,
        Dictionary<string, JsonElement>? invocationArguments,
        CancellationToken ct)
    {
        var result = locator.Kind switch
        {
            CatalogType.Resource => session.ReadResourceAsync(locator.Target, ct),
            CatalogType.ResourceTemplate => session.ReadResourceAsync(locator.Target, invocationArguments ?? new Dictionary<string, JsonElement>(), ct),
            _ => throw new ArgumentOutOfRangeException("MCP feature is not supported: " + locator.Kind)
        };
        return await result.ConfigureAwait(false);
    }

    private static Dictionary<string, JsonElement>? ToArguments(JsonElement? arguments) => arguments is { ValueKind: JsonValueKind.Object } value
        ? value.EnumerateObject().ToDictionary(property => property.Name, property => property.Value) : null;

    private static JsonElement? ToElement(Dictionary<string, JsonElement>? arguments) =>
        arguments is null ? null : JsonSerializer.SerializeToElement(arguments, McpServerJsonContext.Default.DictionaryStringJsonElement);

    private const int JsonOverhead = 48;
    private const int UnknownPayloadBytes = 256;

    private static int Utf8Size(string text) => Encoding.UTF8.GetByteCount(text);

    private static int PackedUtf8Size(ResourceReadResult result)
    {
        var errorBytes = result.Error is null
            ? 0
            : JsonSerializer.SerializeToUtf8Bytes(result.Error, McpServerJsonContext.Default.InvocationError).Length;
        var payloadBytes = result.Result switch
        {
            ReadResourceResult resource => JsonSerializer.SerializeToUtf8Bytes(resource, ToolHelpers.ProtocolOptions).Length,
            JsonElement element => Utf8Size(element.GetRawText()),
            string text => Utf8Size(text),
            null => 0,
            _ => UnknownPayloadBytes
        };
        return errorBytes + payloadBytes + JsonOverhead;
    }

    /// <summary>Host hop: a finished <see cref="InvokeResponse"/>, or an input request to forward.</summary>
    private sealed record HostResult(InvokeResponse? Response, InputRequiredResult? InputRequired)
    {
        public static HostResult FromResponse(InvokeResponse response) => new(response, null);

        public static HostResult FromInputRequired(InputRequiredResult inputRequired) => new(null, inputRequired);
    }
}
