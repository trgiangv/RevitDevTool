using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Core.Utils;
using DevTools.Mcp.Server.Contracts;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tools;

/// <summary>Fixed external tool that invokes opaque catalog locators against the current host session.</summary>
public sealed class InvokeDynamicTool(IHostBroker broker)
{
    public static McpServerTool Create(IHostBroker broker) => McpServerTool.Create(
        new InvokeDynamicTool(broker).Invoke,
        new McpServerToolCreateOptions
        {
            Name = "invoke_dynamic",
            Description = "Invoke a capabilityId from search_dynamic, or batch read resource capabilityIds. Re-search once before retrying a stale locator.",
            Destructive = true,
            OpenWorld = true,
            SerializerOptions = McpToolJson.Options
        });

    [Description("Invoke one capability ID, or batch read resource capability IDs.")]
    private async Task<CallToolResult> Invoke(
        RequestContext<CallToolRequestParams> context,
        string? capabilityId = null,
        Dictionary<string, JsonElement>? arguments = null,
        ResourceReadRequest[]? reads = null,
        CancellationToken cancellationToken = default)
    {
        var request = new InvokeCapabilityRequest(
            capabilityId,
            arguments is null ? null : JsonSerializer.SerializeToElement(arguments, McpServerJsonContext.Default.DictionaryStringJsonElement),
            reads);

        var problems = InvokeCapabilityValidator.Validate(request);
        if (problems.Count > 0)
            return DynamicToolResults.Error(
                "validation_error",
                string.Join(" ", problems.Select(problem => $"{problem.Name}: {problem.Message}")));

        if (request.Reads is { Count: > 0 })
            return await InvokeReadsAsync(request.Reads, cancellationToken).ConfigureAwait(false);

        var response = await InvokeSingleAsync(context, request.CapabilityId!, request.Arguments, cancellationToken).ConfigureAwait(false);
        if (response.InputRequired is not null)
            throw new InputRequiredException(ForwardInputRequired(request.CapabilityId!, request.Arguments, response.InputRequired));

        return ToCallToolResult(response.Response!);
    }

    private static InputRequiredResult ForwardInputRequired(
        string capabilityId,
        JsonElement? arguments,
        InputRequiredResult hostResult)
    {
        var state = new InvokeDynamicMrtrState(capabilityId, arguments, hostResult.RequestState);
        return new InputRequiredResult
        {
            InputRequests = hostResult.InputRequests,
            RequestState = state.Serialize(),
        };
    }

    private static CallToolResult ToCallToolResult(InvokeCapabilityResponse response)
    {
        if (!response.Ok)
            return DynamicToolResults.Result(response, McpServerJsonContext.Default.InvokeCapabilityResponse);

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

        return DynamicToolResults.Result(new InvokeCapabilityResponse(true, true, result), McpServerJsonContext.Default.InvokeCapabilityResponse);
    }

    private async Task<CallToolResult> InvokeReadsAsync(IReadOnlyList<ResourceReadRequest> reads, CancellationToken ct)
    {
        var results = new List<ResourceReadResult>();
        var budget = InvokeDynamicLimits.DefaultResultBudgetBytes;
        var used = Utf8Size("{\"ok\":true,\"executionStarted\":true,\"results\":[]}");
        foreach (var (read, index) in reads.Select((value, index) => (value, index)))
        {
            var item = await InvokeSingleAsync(null, read.CapabilityId!, ToElement(read.Arguments), ct).ConfigureAwait(false);
            var resourceResult = new ResourceReadResult(index, item.Response!.Ok, item.Response.Result, item.Response.Error);
            var itemBytes = PackedUtf8Size(resourceResult);
            if (itemBytes > InvokeDynamicLimits.HardResultBudgetBytes || itemBytes > budget)
            {
                results.Add(new ResourceReadResult(index, false, null,
                    new DynamicInvocationError("result_too_large", "The complete item exceeds the result budget.")));
                continue;
            }
            if (used + itemBytes > budget)
                break;
            results.Add(resourceResult);
            used += itemBytes;
        }
        return DynamicToolResults.Result(new InvokeCapabilityResponse(true, true, Results: results), McpServerJsonContext.Default.InvokeCapabilityResponse);
    }

    private async Task<InvokeSingleOutcome> InvokeSingleAsync(
        RequestContext<CallToolRequestParams>? context,
        string capabilityId,
        JsonElement? arguments,
        CancellationToken ct)
    {
        var precheck = PrepareInvocation(capabilityId);
        if (precheck.ErrorResponse is not null)
            return InvokeSingleOutcome.FromResponse(precheck.ErrorResponse);

        var mrtrState = InvokeDynamicMrtrState.TryParse(context?.Params.RequestState);
        var invocationArguments = ToArguments(mrtrState?.Arguments) ?? ToArguments(arguments);
        try
        {
            return await InvokeResolvedAsync(precheck, context, mrtrState, invocationArguments, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return InvokeSingleOutcome.FromResponse(new InvokeCapabilityResponse(false, true, Error: new DynamicInvocationError("invocation_canceled", "Invocation was canceled or timed out.")));
        }
        catch (Exception ex)
        {
            return InvokeSingleOutcome.FromResponse(new InvokeCapabilityResponse(false, true, Error: new DynamicInvocationError("invocation_failed", ex.Message)));
        }
    }

    private (DynamicCapabilityId Decoded, IHostSession Session, InvokeCapabilityResponse? ErrorResponse) PrepareInvocation(string capabilityId)
    {
        if (!DynamicCapabilityId.TryDecode(capabilityId, out var locator) || locator is null)
            return (null!, null!, Error("validation_error", "capabilityId is malformed."));

        var entry = broker.Catalog.List().FirstOrDefault(item => item.Key.MachineId.Equals(locator.MachineId, StringComparison.OrdinalIgnoreCase) && item.Key.ProcessId == locator.HostInstanceId);
        var session = broker.GetByHostKey(new HostKey(locator.MachineId, locator.HostInstanceId));
        if (entry is null || session is null || !session.IsConnected)
            return (locator, null!, Stale("host_disconnected", "The host session is no longer connected."));

        var staleResponse = ValidateCatalog(locator, entry);
        return staleResponse is null
            ? (locator, session, null)
            : (locator, session, staleResponse);
    }

    private InvokeCapabilityResponse? ValidateCatalog(DynamicCapabilityId locator, HostCatalogEntry entry)
    {
        var resolution = broker.Catalog.Resolve(locator.Kind, locator.Target, locator.MachineId, locator.HostInstanceId);
        if (resolution.State != HostCatalogResolutionState.Found || resolution.Hit is null)
            return Stale("capability_removed", "The capability is no longer advertised by this host.");

        var currentFingerprint = DynamicCapabilityId.FingerprintFor(resolution.Hit);
        if (!string.Equals(locator.CatalogVersion, DynamicCapabilityId.CatalogVersionFor(entry), StringComparison.Ordinal))
        {
            var reason = string.Equals(locator.Fingerprint, currentFingerprint, StringComparison.Ordinal)
                ? "host_catalog_changed"
                : "capability_changed";
            return Stale(reason, "The host catalog changed; search again before invoking.");
        }

        return string.Equals(locator.Fingerprint, currentFingerprint, StringComparison.Ordinal)
            ? null
            : Stale("capability_changed", "The capability changed; search again before invoking.");
    }

    private static async Task<InvokeSingleOutcome> InvokeResolvedAsync(
        (DynamicCapabilityId Decoded, IHostSession Session, InvokeCapabilityResponse? ErrorResponse) precheck,
        RequestContext<CallToolRequestParams>? context,
        InvokeDynamicMrtrState? mrtrState,
        Dictionary<string, JsonElement>? invocationArguments,
        CancellationToken ct)
    {
        if (precheck.Decoded.Kind is HostCatalogKind.Tool)
            return await InvokeToolAsync(precheck.Session, precheck.Decoded.Target, context, mrtrState, invocationArguments, ct).ConfigureAwait(false);

        var result = await ReadCatalogItemAsync(precheck.Session, precheck.Decoded, invocationArguments, ct).ConfigureAwait(false);
        return InvokeSingleOutcome.FromResponse(new InvokeCapabilityResponse(true, true, result));
    }

    private static async Task<InvokeSingleOutcome> InvokeToolAsync(
        IHostSession session,
        string toolName,
        RequestContext<CallToolRequestParams>? context,
        InvokeDynamicMrtrState? mrtrState,
        Dictionary<string, JsonElement>? invocationArguments,
        CancellationToken ct)
    {
        var hostParams = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = invocationArguments,
            InputResponses = context?.Params.InputResponses,
            RequestState = mrtrState?.HostRequestState,
        };
        var outcome = await session.CallToolPassthroughAsync(hostParams, ct).ConfigureAwait(false);
        if (outcome.IsInputRequired)
            return InvokeSingleOutcome.FromInputRequired(outcome.InputRequired!);

        return InvokeSingleOutcome.FromResponse(new InvokeCapabilityResponse(true, true, outcome.ToolResult));
    }

    private static async Task<object> ReadCatalogItemAsync(
        IHostSession session,
        DynamicCapabilityId locator,
        Dictionary<string, JsonElement>? invocationArguments,
        CancellationToken ct)
    {
        var result = locator.Kind switch
        {
            HostCatalogKind.Resource => session.ReadResourceAsync(locator.Target, ct),
            HostCatalogKind.ResourceTemplate => session.ReadResourceAsync(locator.Target, invocationArguments ?? new Dictionary<string, JsonElement>(), ct),
            _ => throw new ArgumentOutOfRangeException("MCP feature is not supported: " + locator.Kind)
        };
        return await result.ConfigureAwait(false);
    }

    private static Dictionary<string, JsonElement>? ToArguments(JsonElement? arguments) => arguments is { ValueKind: JsonValueKind.Object } value
        ? value.EnumerateObject().ToDictionary(property => property.Name, property => property.Value) : null;

    private static JsonElement? ToElement(Dictionary<string, JsonElement>? arguments) =>
        arguments is null ? null : JsonSerializer.SerializeToElement(arguments, McpServerJsonContext.Default.DictionaryStringJsonElement);

    private static InvokeCapabilityResponse Stale(string reason, string message) => new(false, false, Error: new DynamicInvocationError("stale_capability", message, true, reason, "research_then_reinvoke"));
    private static InvokeCapabilityResponse Error(string type, string message) => new(false, false, Error: new DynamicInvocationError(type, message));
    private const int JsonOverhead = 48;
    private const int UnknownPayloadBytes = 256;

    private static int Utf8Size(string text) => Encoding.UTF8.GetByteCount(text);

    private static int PackedUtf8Size(ResourceReadResult result)
    {
        var errorBytes = result.Error is null
            ? 0
            : JsonSerializer.SerializeToUtf8Bytes(result.Error, McpServerJsonContext.Default.DynamicInvocationError).Length;
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
}
