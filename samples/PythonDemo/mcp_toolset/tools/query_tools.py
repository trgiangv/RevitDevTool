"""Query and model intelligence tools."""

from typing import Annotated

from mcp.types import CallToolResult
from pydantic import Field

from dto.filters import FilterSpec
from dto.query import (
    FindElementsResult,
    ListCategoryParametersResult,
    ListLinksResult,
    ListRoomsResult,
    ListTypesResult,
    ModelSummaryResult,
)
from services.query_service import QueryService
from shared.mcp_registry import McpRegistry
from shared.tool_annotations import read_only_tool
from shared.tool_results import structured_tool_result


def register_query_tools(mcp: McpRegistry) -> None:
    """Register read-only model intelligence MCP tools."""
    service = QueryService()

    @mcp.tool(
        annotations=read_only_tool("Get Model Summary"),
        structured_output=True,
    )
    async def revit_get_model_summary() -> ModelSummaryResult:
        """Project overview: info, categories+counts, warnings, levels, phases, worksets, links."""
        return service.get_model_summary()

    @mcp.tool(
        annotations=read_only_tool("Find Elements"),
        structured_output=True,
    )
    async def revit_find_elements(
        filters: Annotated[
            FilterSpec | None, Field(description="Composable filter specification")
        ] = None,
        selected_only: Annotated[
            bool, Field(description="Limit results to the current Revit selection")
        ] = False,
        max_results: Annotated[int, Field(ge=1, le=10000)] = 500,
        offset: Annotated[
            int, Field(ge=0, description="Pagination offset — skip this many matches")
        ] = 0,
        include_types: Annotated[
            bool, Field(description="Include element types")
        ] = False,
        include_instances: Annotated[
            bool, Field(description="Include element instances")
        ] = True,
        fields: Annotated[
            list[str] | None, Field(description="Fields to return")
        ] = None,
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
        return _find_elements_result(result)

    @mcp.tool(
        annotations=read_only_tool("Read Parameters"),
        structured_output=True,
    )
    async def revit_read_parameters(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        param_names: Annotated[
            list[str] | None, Field(description="Optional parameter name filter")
        ] = None,
    ) -> CallToolResult:
        """Get all params of element(s) with metadata."""
        result = service.read_parameters(element_ids, param_names)
        return structured_tool_result(
            "Parameters for {} elements".format(len(result.elements)),
            result,
        )

    @mcp.tool(
        annotations=read_only_tool("List Types"),
        structured_output=True,
    )
    async def revit_list_types(
        kind: Annotated[
            str, Field(description="family, mep_system, view_template, or title_block")
        ],
        category: Annotated[
            str | None, Field(description="Category filter when kind=family")
        ] = None,
    ) -> ListTypesResult:
        """Available types: family, MEP system, view template, title block."""
        return service.list_types(kind, category)

    @mcp.tool(
        annotations=read_only_tool("List Category Parameters"),
        structured_output=True,
    )
    async def revit_list_category_parameters(
        category_name: Annotated[str, Field(description="Category display name")],
    ) -> ListCategoryParametersResult:
        """Schedulable parameter names for a category."""
        return service.list_category_parameters(category_name)

    @mcp.tool(
        annotations=read_only_tool("List Rooms"),
        structured_output=True,
    )
    async def revit_list_rooms() -> ListRoomsResult:
        """Rooms with name, number, area, level, department, location."""
        return service.list_rooms()

    @mcp.tool(
        annotations=read_only_tool("List Links"),
        structured_output=True,
    )
    async def revit_list_links() -> ListLinksResult:
        """Revit links and CAD imports with load status."""
        return service.list_links()


def _find_elements_result(result: FindElementsResult) -> CallToolResult:
    return structured_tool_result(
        "Found {} elements (total {}, truncated={})".format(
            len(result.elements),
            result.count,
            str(result.truncated).lower(),
        ),
        result,
    )
