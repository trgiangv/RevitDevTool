"""Query and model intelligence tools."""
from __future__ import annotations
from typing import Annotated

from mcp.server.mcpserver import MCPServer
from mcp.types import CallToolResult, TextContent, ToolAnnotations
from pydantic import Field

from dto.filters import FilterSpec
from dto.query import (
    ListCategoryParametersResult,
    ListLinksResult,
    ListRoomsResult,
    ListTypesResult,
    ModelSummaryResult,
)
from services.query_service import QueryService


def register_query_tools(mcp: MCPServer) -> None:
    service = QueryService()

    @mcp.tool(annotations=ToolAnnotations(title="Get Model Summary", readOnlyHint=True), structured_output=True)
    async def revit_get_model_summary() -> ModelSummaryResult:
        """Project overview: info, categories+counts, warnings, levels, phases, worksets, links."""
        return service.get_model_summary()

    @mcp.tool(annotations=ToolAnnotations(title="Find Elements", readOnlyHint=True), structured_output=True)
    async def revit_find_elements(
        filters: Annotated[FilterSpec | None, Field(description="Composable filter specification")] = None,
        selected_only: Annotated[bool, Field(description="Limit results to the current Revit selection")] = False,
        max_results: Annotated[int, Field(ge=1, le=10000)] = 500,
        offset: Annotated[int, Field(ge=0, description="Pagination offset — skip this many matches")] = 0,
        include_types: Annotated[bool, Field(description="Include element types")] = False,
        include_instances: Annotated[bool, Field(description="Include element instances")] = True,
        fields: Annotated[list[str] | None, Field(description="Fields to return")] = None,
    ) -> CallToolResult:
        """Structured element search with composable FilterSpec."""
        result = service.find_elements(
            filters,
            selected_only=selected_only,
            include_types=include_types,
            include_instances=include_instances,
            max_results=max_results,
            offset=offset,
            fields=fields,
        )
        structured = result.model_dump(by_alias=True)
        return CallToolResult(
            content=[
                TextContent(
                    type="text",
                    text="Found {} elements (total {}, truncated={})".format(
                        len(result.elements), result.count, str(result.truncated).lower()
                    ),
                )
            ],
            structured_content=structured,
        )

    @mcp.tool(annotations=ToolAnnotations(title="Read Parameters", readOnlyHint=True), structured_output=True)
    async def revit_read_parameters(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        param_names: Annotated[list[str] | None, Field(description="Optional parameter name filter")] = None,
    ) -> CallToolResult:
        """Get all params of element(s) with metadata."""
        result = service.read_parameters(element_ids, param_names)
        structured = result.model_dump(by_alias=True)
        return CallToolResult(
            content=[
                TextContent(
                    type="text",
                    text="Parameters for {} elements".format(len(result.elements)),
                )
            ],
            structured_content=structured,
        )

    @mcp.tool(annotations=ToolAnnotations(title="List Types", readOnlyHint=True), structured_output=True)
    async def revit_list_types(
        kind: Annotated[str, Field(description="family, mep_system, view_template, or title_block")],
        category: Annotated[str | None, Field(description="Category filter when kind=family")] = None,
    ) -> ListTypesResult:
        """Available types: family, MEP system, view template, title block."""
        return service.list_types(kind, category)

    @mcp.tool(annotations=ToolAnnotations(title="List Category Parameters", readOnlyHint=True), structured_output=True)
    async def revit_list_category_parameters(
        category_name: Annotated[str, Field(description="Category display name")],
    ) -> ListCategoryParametersResult:
        """Schedulable parameter names for a category."""
        return service.list_category_parameters(category_name)

    @mcp.tool(annotations=ToolAnnotations(title="List Rooms", readOnlyHint=True), structured_output=True)
    async def revit_list_rooms() -> ListRoomsResult:
        """Rooms with name, number, area, level, department, location."""
        return service.list_rooms()

    @mcp.tool(annotations=ToolAnnotations(title="List Links", readOnlyHint=True), structured_output=True)
    async def revit_list_links() -> ListLinksResult:
        """Revit links and CAD imports with load status."""
        return service.list_links()
