using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp.Core;

/// <summary>
/// Bridges SDK <see cref="McpTaskExecutionMode"/> to per-tool <c>[McpMeta]</c> declarations.
/// </summary>
/// <remarks>
/// <para>
/// <b>SEP-2663</b> does not define a per-tool wire field for task behavior. Server policy is
/// implemented via <c>McpTasksOptions.ExecutionModeSelector</c>, which returns
/// <see cref="McpTaskExecutionMode"/>. The SDK does not expose that enum on
/// <c>[McpServerTool]</c>; declare it with <c>[McpMeta(MetaKey, Mode.Optional)]</c> instead.
/// </para>
/// <para>
/// <see cref="MetaKey"/> is the only host convention: it links tool metadata to
/// <see cref="ParseMode(JsonObject?, McpTaskExecutionMode)"/>. Mode strings use <c>nameof(McpTaskExecutionMode)</c> members so they
/// stay aligned with the SDK enum without manual literals.
/// </para>
/// <para>
/// Copy this class into custom toolsets when you do not reference this assembly — add package
/// <c>ModelContextProtocol.Extensions.Tasks</c> and keep <see cref="MetaKey"/> aligned.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// [McpServerTool(Name = "slow_report")]
/// [McpMeta(McpTaskExecutionMeta.MetaKey, McpTaskExecutionMeta.Mode.Optional)]
/// public static Task&lt;CallToolResult&gt; SlowReport() =&gt; ...;
/// </code>
/// </example>
public static class McpTaskExecutionMeta
{
    /// <summary>
    /// <c>Tool.Meta</c> key read by the host <c>ExecutionModeSelector</c> (not an MCP wire field).
    /// </summary>
    public const string MetaKey = "tasks.executionMode";

    /// <summary><see cref="McpInvocationResponse.Meta"/> keys for host-internal pass-through (not MCP wire).</summary>
    public static class Invocation
    {
        /// <summary>Serialized <c>InputRequiredResult</c> for MRTR host pass-through.</summary>
        public const string InputRequired = "devtools.inputRequired";
    }

    /// <summary>
    /// <c>[McpMeta]</c> values derived from SDK <see cref="McpTaskExecutionMode"/> member names.
    /// </summary>
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public static class Mode
    {
        public const string Synchronous = nameof(McpTaskExecutionMode.Synchronous);
        public const string Optional = nameof(McpTaskExecutionMode.Optional);
        public const string Required = nameof(McpTaskExecutionMode.Required);
    }

    /// <summary>Reads <see cref="MetaKey"/> from tool metadata and maps to <see cref="McpTaskExecutionMode"/>.</summary>
    public static McpTaskExecutionMode ParseMode(JsonObject? meta) =>
        ParseMode(meta, McpTaskExecutionMode.Synchronous);

    /// <summary>Reads <see cref="MetaKey"/> from tool metadata and maps to <see cref="McpTaskExecutionMode"/>.</summary>
    public static McpTaskExecutionMode ParseMode(JsonObject? meta, McpTaskExecutionMode defaultMode)
    {
        if (meta is null || !meta.TryGetPropertyValue(MetaKey, out var node))
            return defaultMode;

        var value = node switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => text,
            _ => node?.ToString()
        };

        if (string.IsNullOrWhiteSpace(value))
            return defaultMode;

        return Enum.TryParse<McpTaskExecutionMode>(value, ignoreCase: true, out var mode)
            ? mode
            : defaultMode;
    }

    /// <summary>Selects task mode for a daemon tool call from tool metadata.</summary>
    public static McpTaskExecutionMode SelectForRequest(RequestContext<CallToolRequestParams> request)
    {
        if (request.MatchedPrimitive is McpServerTool matched)
            return ParseMode(matched.ProtocolTool.Meta);

        var name = request.Params.Name;
        if (string.IsNullOrWhiteSpace(name))
            return McpTaskExecutionMode.Synchronous;

        var tools = request.Server.ServerOptions.ToolCollection;
        if (tools is not null && tools.TryGetPrimitive(name!, out var tool))
            return ParseMode(tool.ProtocolTool.Meta);

        return McpTaskExecutionMode.Synchronous;
    }
}
