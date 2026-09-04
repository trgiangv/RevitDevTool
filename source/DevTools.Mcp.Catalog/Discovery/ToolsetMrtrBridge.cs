using System.Text.Json;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Bridges ILRepacked <c>InputRequiredException</c> instances to host SDK types.
/// Host <c>catch (InputRequiredException)</c> cannot match foreign exception type identity.
/// </summary>
public static class ToolsetMrtrBridge
{
    public static bool IsIsolatedInputRequired(Exception exception) =>
        exception is not InputRequiredException &&
        string.Equals(exception.GetType().Name, nameof(InputRequiredException), StringComparison.Ordinal) &&
        exception.GetType().GetProperty("Result") is not null;

    public static McpInvocationResponse ToInputRequiredResponse(InputRequiredException exception) =>
        ToInputRequiredResponse(exception.Result);

    private static McpInvocationResponse ToInputRequiredResponse(InputRequiredResult result) =>
        new() { InputRequired = result };

    public static bool TryGetInputRequiredResult(McpInvocationResponse response, out InputRequiredResult? result)
    {
        if (response.InputRequired is not null)
        {
            result = response.InputRequired;
            return true;
        }

        result = null;
        if (response.Meta?.TryGetPropertyValue(McpTaskExecutionMeta.Invocation.InputRequired, out var node) != true || node is null)
            return false;

        result = node.Deserialize<InputRequiredResult>(ToolHelpers.ProtocolOptions);
        return result is not null;
    }

    public static InputRequiredException ToHostException(Exception foreign)
    {
        if (foreign is InputRequiredException host)
            return host;

        var foreignResult = foreign.GetType().GetProperty("Result")?.GetValue(foreign);
        if (foreignResult is null)
        {
            return new InputRequiredException(
                inputRequests: null,
                requestState: foreign.Message);
        }

        var foreignJson = SerializeRuntime(foreignResult);
        var hostResult = foreignJson.Deserialize<InputRequiredResult>(ToolHelpers.ProtocolOptions);
        if (hostResult is not null)
            return new InputRequiredException(hostResult);

        var requestState = foreignResult.GetType().GetProperty("RequestState")?.GetValue(foreignResult) as string;
        var mappedRequests = MapInputRequests(foreignResult.GetType().GetProperty("InputRequests")?.GetValue(foreignResult));

        if (mappedRequests is null && requestState is null)
            return new InputRequiredException(requestState: foreign.Message);

        return new InputRequiredException(
            inputRequests: mappedRequests,
            requestState: requestState);
    }

    private static IDictionary<string, InputRequest>? MapInputRequests(object? raw)
    {
        if (raw is null)
            return null;

        if (raw is IDictionary<string, InputRequest> hostTyped)
            return hostTyped.Count > 0 ? hostTyped : null;

        if (raw is not System.Collections.IDictionary dictionary)
            return null;

        var mapped = new Dictionary<string, InputRequest>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString();
            if (key is null || entry.Value is null)
                continue;

            var bridged = BridgeInputRequest(entry.Value);
            if (bridged is not null)
                mapped[key] = bridged;
        }

        return mapped.Count > 0 ? mapped : null;
    }

    private static InputRequest? BridgeInputRequest(object foreign)
    {
        if (foreign is InputRequest host)
            return host;

        var json = JsonSerializer.Serialize(foreign, foreign.GetType(), ToolHelpers.RuntimeJsonOptions);
        return JsonSerializer.Deserialize<InputRequest>(json, ToolHelpers.ProtocolOptions);
    }

    private static JsonElement SerializeRuntime(object value) =>
        // Runtime metadata is required only for the foreign exception payload;
        // protocol deserialization still uses the host SDK contract.
        JsonSerializer.SerializeToElement(value, value.GetType(), ToolHelpers.RuntimeJsonOptions);
}
