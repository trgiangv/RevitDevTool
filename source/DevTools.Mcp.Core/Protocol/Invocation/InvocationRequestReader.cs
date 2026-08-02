using System.Text.Json;
using System.Text.Json.Nodes;
using ToolsKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Tools;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Reads <c>tools/call</c> wire params into <see cref="McpInvocationRequest"/>.</summary>
public static class InvocationRequestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static McpInvocationRequest FromWire(JsonObject? parameters) =>
        new()
        {
            Arguments = ReadArguments(parameters),
            InputResponses = ReadInputResponses(parameters),
            RequestState = ReadRequestState(parameters),
            ProgressToken = ReadProgressToken(parameters),
            Meta = ReadMeta(parameters),
        };

    private static JsonElement? ReadArguments(JsonObject? parameters)
    {
        if (parameters?[ToolsKeys.Arguments] is not { } argumentsNode)
            return null;

        return JsonSerializer.SerializeToElement(argumentsNode, JsonOptions);
    }

    private static IReadOnlyDictionary<string, JsonElement>? ReadInputResponses(JsonObject? parameters)
    {
        if (parameters?[ToolsKeys.InputResponses] is not JsonObject inputResponsesObject)
            return null;

        var responses = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in inputResponsesObject)
        {
            responses[property.Key] = property.Value is null
                ? default
                : JsonSerializer.SerializeToElement(property.Value, JsonOptions);
        }

        return responses;
    }

    private static JsonElement? ReadRequestState(JsonObject? parameters)
    {
        if (parameters?[ToolsKeys.RequestState] is not { } requestStateNode)
            return null;

        return JsonSerializer.SerializeToElement(requestStateNode, JsonOptions);
    }

    private static long? ReadProgressToken(JsonObject? parameters)
    {
        if (parameters?[ToolsKeys.ProgressToken] is JsonValue progressValue &&
            progressValue.TryGetValue(out long parsedProgress))
        {
            return parsedProgress;
        }

        return null;
    }

    private static JsonObject? ReadMeta(JsonObject? parameters) =>
        parameters?[ToolsKeys.Meta] switch
        {
            JsonObject metaObject => metaObject.DeepClone().AsObject(),
            _ => null,
        };
}
