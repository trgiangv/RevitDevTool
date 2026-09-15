"""Read-only drawing intelligence tools."""

from typing import Annotated

from mcp.types import CallToolResult
from pydantic import Field

from dto.query import GetSelectionResult, ListLayersResult, StatusResult
from services.query_service import QueryService
from shared.mcp_registry import McpRegistry
from shared.tool_annotations import read_only_tool
from shared.tool_results import structured_tool_result


def register_query_tools(mcp: McpRegistry) -> None:
    """Register read-only AutoCAD query tools."""
    service = QueryService()

    @mcp.tool(
        annotations=read_only_tool("Get Status"),
        structured_output=True,
    )
    async def acad_get_status() -> CallToolResult:
        """Health, drawing name, entity/layer counts, and implied selection size."""
        result: StatusResult = service.get_status()
        return structured_tool_result(result.summary_text(), result)

    @mcp.tool(
        annotations=read_only_tool("List Layers"),
        structured_output=True,
    )
    async def acad_list_layers() -> ListLayersResult:
        """Layers with off/frozen/locked state and color."""
        return service.list_layers()

    @mcp.tool(
        annotations=read_only_tool("Find Entities"),
        structured_output=True,
    )
    async def acad_find_entities(
        type_name: Annotated[
            str | None,
            Field(description="Entity CLR type name, e.g. Line, Circle, Polyline"),
        ] = None,
        layer: Annotated[
            str | None, Field(description="Layer name filter (case-insensitive)")
        ] = None,
        max_results: Annotated[int, Field(ge=1, le=5000)] = 200,
        offset: Annotated[int, Field(ge=0, description="Skip this many matches")] = 0,
    ) -> CallToolResult:
        """Model-space entity search by type and/or layer."""
        result = service.find_entities(
            type_name=type_name,
            layer=layer,
            max_results=max_results,
            offset=offset,
        )
        return structured_tool_result(
            "Found {} entities (total {}, truncated={})".format(
                len(result.entities),
                result.count,
                str(result.truncated).lower(),
            ),
            result,
        )

    @mcp.tool(
        annotations=read_only_tool("Get Selection"),
        structured_output=True,
    )
    async def acad_get_selection() -> GetSelectionResult:
        """Currently implied/selected entities in the editor."""
        return service.get_selection()
