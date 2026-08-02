using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Bridges ILRepacked <c>InputRequiredException</c> instances to host SDK types.
/// Host <c>catch (InputRequiredException)</c> cannot match foreign exception type identity.
/// </summary>
public static class ToolsetMrtrBridge
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    public static bool IsForeignInputRequired(Exception exception) =>
        exception is not InputRequiredException &&
        string.Equals(exception.GetType().Name, nameof(InputRequiredException), StringComparison.Ordinal);

    public static McpInvocationResponse ToInputRequiredResponse(InputRequiredException exception) =>
        ToInputRequiredResponse(exception.Result);

    private static McpInvocationResponse ToInputRequiredResponse(InputRequiredResult result)
    {
        var payload = JsonSerializer.SerializeToNode(result, JsonOptions) as JsonObject
                      ?? throw new InvalidOperationException("Failed to serialize InputRequiredResult.");
        return new McpInvocationResponse
        {
            Meta = new JsonObject { [McpTaskExecutionMeta.Invocation.InputRequired] = payload }
        };
    }

    public static bool TryGetInputRequiredResult(McpInvocationResponse response, out InputRequiredResult? result)
    {
        result = null;
        if (response.Meta?.TryGetPropertyValue(McpTaskExecutionMeta.Invocation.InputRequired, out var node) != true || node is null)
            return false;

        result = node.Deserialize<InputRequiredResult>(JsonOptions);
        return result is not null;
    }

    public static InputRequiredException ToHostException(Exception foreign)
    {
        if (foreign is InputRequiredException host)
            return host;

        var foreignResult = foreign.GetType()
            .GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(foreign);
        if (foreignResult is null)
        {
            return new InputRequiredException(
                inputRequests: null,
                requestState: foreign.Message);
        }

        var requestState = ReadProperty(foreignResult, "RequestState") as string;
        var mappedRequests = MapInputRequests(ReadProperty(foreignResult, "InputRequests"));

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

        var json = JsonSerializer.Serialize(BagObject(foreign), CamelCaseOptions);
        return JsonSerializer.Deserialize<InputRequest>(json, JsonOptions);
    }

    private static Dictionary<string, object?> BagObject(object value)
    {
        var nested = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!IsReadableProperty(prop))
                continue;

            var child = prop.GetValue(value);
            if (child is null)
                continue;

            nested[ToCamel(prop.Name)] = ToBagValue(child);
        }

        return nested;
    }

    private static object? ToBagValue(object value)
    {
        if (IsDirectBagValue(value))
            return value;
        if (value.GetType().IsEnum)
            return value.ToString();
        if (value is System.Collections.IDictionary dict)
            return BagDictionary(dict);
        if (value is System.Collections.IEnumerable enumerable and not string)
            return BagEnumerable(enumerable);
        return BagObject(value);
    }

    private static Dictionary<string, object?> BagDictionary(System.Collections.IDictionary dict)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in dict)
        {
            if (entry.Key?.ToString() is not { } key || entry.Value is null)
                continue;

            map[key] = IsDirectBagValue(entry.Value) ? entry.Value : BagObject(entry.Value);
        }

        return map;
    }

    private static List<object?> BagEnumerable(System.Collections.IEnumerable enumerable)
    {
        var list = new List<object?>();
        foreach (var item in enumerable)
        {
            if (item is null)
                continue;

            list.Add(IsDirectBagValue(item) ? item : BagObject(item));
        }

        return list;
    }

    private static bool IsReadableProperty(PropertyInfo prop) =>
        prop.CanRead && prop.GetIndexParameters().Length == 0;

    private static bool IsDirectBagValue(object value) =>
        value is string or bool or int or long or double or float or decimal or JsonElement;

    private static string ToCamel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static object? ReadProperty(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?.GetValue(target);
}
