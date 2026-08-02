using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using ToolsKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Tools;

namespace DevTools.Mcp.Adapter.Execution;

/// <summary>Serializes <see cref="McpInvocationRequest"/> for the embedded Python bridge.</summary>
public static class PythonInvocationPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToJson(McpInvocationRequest? request)
    {
        if (request is null)
            return "{}";

        if (!HasMrtrFields(request))
            return ToLegacyArgumentsJson(request);

        return JsonSerializer.Serialize(BuildMrtrPayload(request), JsonOptions);
    }

    private static bool HasMrtrFields(McpInvocationRequest request) =>
        request.InputResponses is { Count: > 0 } ||
        request.RequestState is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined };

    private static string ToLegacyArgumentsJson(McpInvocationRequest request)
    {
        if (request.Arguments is not { } arguments ||
            arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "{}";
        }

        return arguments.ValueKind == JsonValueKind.Object
            ? arguments.GetRawText()
            : JsonSerializer.Serialize(arguments, JsonOptions);
    }

    private static Dictionary<string, object?> BuildMrtrPayload(McpInvocationRequest request)
    {
        var payload = new Dictionary<string, object?>();
        if (request.Arguments is { } args &&
            args.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            payload[ToolsKeys.Arguments] = args.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(args.GetRawText(), JsonOptions)
                : args;
        }

        if (request.InputResponses is { Count: > 0 } inputResponses)
            payload[ToolsKeys.InputResponses] = inputResponses;

        if (request.RequestState is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } requestState)
            payload[ToolsKeys.RequestState] = requestState;

        return payload;
    }
}
