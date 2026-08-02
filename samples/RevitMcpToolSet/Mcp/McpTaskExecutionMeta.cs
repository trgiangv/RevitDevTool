using ModelContextProtocol.Extensions.Tasks;
using System.Text.Json.Nodes;

namespace RevitMcpToolSet.Mcp;

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
/// <see cref="MetaKey"/> is the only host convention. Mode strings use
/// <c>nameof(McpTaskExecutionMode)</c> members — same pattern as
/// <c>DevTools.Mcp.Core.McpTaskExecutionMeta</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// [McpServerTool(Name = "revit_export_pdf")]
/// [McpMeta(McpTaskExecutionMeta.MetaKey, McpTaskExecutionMeta.Mode.Optional)]
/// public static object ExportPdf(...) =&gt; ...;
/// </code>
/// </example>
public static class McpTaskExecutionMeta
{
    /// <summary>
    /// <c>Tool.Meta</c> key read by the host <c>ExecutionModeSelector</c> (not an MCP wire field).
    /// </summary>
    public const string MetaKey = "tasks.executionMode";

    /// <summary>
    /// <c>[McpMeta]</c> values derived from SDK <see cref="McpTaskExecutionMode"/> member names.
    /// </summary>
    public static class Mode
    {
        public const string Synchronous = nameof(McpTaskExecutionMode.Synchronous);
        public const string Optional = nameof(McpTaskExecutionMode.Optional);
        public const string Required = nameof(McpTaskExecutionMode.Required);
    }

    /// <summary>Reads <see cref="MetaKey"/> from tool metadata and maps to <see cref="McpTaskExecutionMode"/>.</summary>
    public static McpTaskExecutionMode ParseMode(JsonObject? meta) =>
        ParseMode(meta, McpTaskExecutionMode.Synchronous);

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
}
