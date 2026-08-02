"""Convention for MCP Tasks execution mode on tools (Python MCP toolsets).

SEP-2663 does not define a per-tool wire field. The C# host maps tool metadata to SDK
``McpTaskExecutionMode`` (package ``ModelContextProtocol.Extensions.Tasks``) via
``McpTaskExecutionMeta.ParseMode``.

``META_KEY`` is the only host convention. ``Mode`` strings match C# ``nameof(McpTaskExecutionMode.*)``.

Example::

    @mcp.tool(meta=McpTaskExecutionMeta.OptionalMeta)
    async def revit_export_pdf() -> ExportPdfResult:
        ...
"""


class McpTaskExecutionMeta:
    """Meta key and mode strings aligned with SDK ``McpTaskExecutionMode`` names."""

    MetaKey = "tasks.executionMode"

    class Mode:
        Synchronous = "Synchronous"
        Optional = "Optional"
        Required = "Required"


McpTaskExecutionMeta.OptionalMeta = {McpTaskExecutionMeta.MetaKey: McpTaskExecutionMeta.Mode.Optional}
McpTaskExecutionMeta.RequiredMeta = {McpTaskExecutionMeta.MetaKey: McpTaskExecutionMeta.Mode.Required}
