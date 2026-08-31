using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Execution;

/// <summary>Serializes <see cref="CallToolRequestParams"/> for the embedded Python bridge.</summary>
public static class PythonInvocationPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToJson(CallToolRequestParams? request)
    {
        if (request is null)
            return "{}";

        if (!HasMrtrFields(request))
            return ToLegacyArgumentsJson(request);

        return JsonSerializer.Serialize(BuildMrtrPayload(request), JsonOptions);
    }

    private static bool HasMrtrFields(CallToolRequestParams request) =>
        request.InputResponses is { Count: > 0 } ||
        !string.IsNullOrEmpty(request.RequestState);

    private static string ToLegacyArgumentsJson(CallToolRequestParams request)
    {
        if (request.Arguments is not { Count: > 0 })
            return "{}";

        return JsonSerializer.Serialize(request.Arguments, JsonOptions);
    }

    private static Dictionary<string, object?> BuildMrtrPayload(CallToolRequestParams request)
    {
        var payload = new Dictionary<string, object?>();
        if (request.Arguments is { Count: > 0 } arguments)
            payload[McpSpecKeys.Tools.Arguments] = arguments;

        if (request.InputResponses is { Count: > 0 } inputResponses)
            payload[McpSpecKeys.Tools.InputResponses] = inputResponses;

        if (!string.IsNullOrEmpty(request.RequestState))
            payload[McpSpecKeys.Tools.RequestState] = request.RequestState;

        return payload;
    }
}
