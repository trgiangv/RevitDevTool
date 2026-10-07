using System.ComponentModel;
using DevTools.Daemon.Mcp.Code;
using DevTools.Daemon.Mcp.Processes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

/// <summary>
/// Model-facing program tool. Host capabilities stay off <c>tools/list</c>.
/// </summary>
public sealed class CodeModeTool(IProcessSessions sessions)
{
    public const string Name = "code_mode";

    private readonly CodeModeCache _cache = new();

    public static McpServerTool Create(IProcessSessions sessions) => McpServerTool.Create(
        new CodeModeTool(sessions).Run,
        new McpServerToolCreateOptions
        {
            Name = Name,
            Description = Description,
            Destructive = true,
            OpenWorld = true,
        });

    [Description("Run a C# program that searches and calls host tools.")]
    private async Task<CallToolResult> Run(
        [Description("Body of an async method. Not JSON and not a markdown fence.")] string code,
        [Description("When true, InvokeAsync throws unless ReadOnlyHint is true.")] bool readOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runtime = new CatalogRuntime(sessions.Catalog, sessions.GetByProcessId, readOnly);
            using var program = _cache.Load(code);
            var value = await program.RunAsync(runtime, cancellationToken).ConfigureAwait(false);
            return ProgramResult.ToCallToolResult(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = ex.Message }],
            };
        }
    }

    private const string Description =
        """
        Run a C# program that calls host tools. `code` is the body of an async method, not JSON and not a markdown fence. `return` is the only value sent back to you. No Revit or AutoCAD API, no files, no network, no extra assemblies.

        Globals:
        - await SearchAsync(query, limit: 8, processId: null, primitiveType: null) returns Name, Description, ProcessId, PrimitiveType. PrimitiveType defaults to tool. Values are tool, resource, resource_template.
        - await DescribeAsync(name, processId) returns the SDK Tool, including InputSchema and Annotations.ReadOnlyHint. Call it before InvokeAsync when the arguments are not obvious.
        - await InvokeAsync(name, arguments, processId) returns the SDK CallToolResult. StructuredContent is the JSON. Content holds TextContentBlock, ImageContentBlock, AudioContentBlock, EmbeddedResourceBlock, and ResourceLinkBlock. IsError is the host error flag. Pass processId when SearchAsync shows the same name on more than one process.
        - await ReadAsync(name, arguments, processId) returns the SDK ReadResourceResult. Contents are text or blob. Blob is base64 for non-text. It throws if name is a tool.
        - return a projected object for JSON. return the ImageContentBlock, AudioContentBlock, CallToolResult, or ReadResourceResult when the model must see that block. A projected object drops blocks you did not copy.
        - Filter before return. A return value over 1 MiB fails. That includes image base64.
        - list_machines, list_processes, launch_host, and read_file_info are separate tools. Call them directly.
        - Calls for one processId run one at a time. A later failure does not undo an earlier InvokeAsync.
        """;
}
