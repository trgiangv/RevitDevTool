"""Element CRUD tools."""

from typing import Annotated, Any

from mcp.types import CallToolResult
from pydantic import Field

from dto.crud import (
    CloneParametersResult,
    HighlightElementsResult,
    MoveElementsResult,
    ParameterUpdate,
    PlaceFamilyResult,
    PlacementSpec,
    RotateElementsResult,
    SwapTypeResult,
    WriteParametersResult,
)
from services.element_service import ElementService
from shared.mcp_registry import McpRegistry
from shared.responses import ToolError
from shared.tool_annotations import destructive_tool
from shared.tool_results import structured_tool_result


def register_crud_tools(mcp: McpRegistry) -> None:
    """Register element create/read/update/delete MCP tools."""
    service = ElementService()
    _register_parameter_tools(mcp, service)
    _register_transform_tools(mcp, service)


def _register_parameter_tools(mcp: McpRegistry, service: ElementService) -> None:
    @mcp.tool(
        annotations=destructive_tool("Write Parameters"),
        structured_output=True,
    )
    async def revit_write_parameters(
        element_ids: Annotated[list[int], Field(description="Target element ids")],
        updates: Annotated[
            list[ParameterUpdate], Field(description="Parameter updates")
        ],
    ) -> WriteParametersResult:
        """Set param values on elements."""
        return service.write_parameters(element_ids, updates)

    @mcp.tool(
        annotations=destructive_tool("Clone Parameters"),
        structured_output=True,
    )
    async def revit_clone_parameters(
        source_id: Annotated[int, Field(description="Source element id")],
        target_ids: Annotated[list[int], Field(description="Target element ids")],
        param_names: Annotated[list[str], Field(description="Parameter names to copy")],
    ) -> CloneParametersResult:
        """Copy param values source → targets."""
        return service.clone_parameters(source_id, target_ids, param_names)

    @mcp.tool(
        annotations=destructive_tool("Swap Type"),
        structured_output=True,
    )
    async def revit_swap_type(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        new_type_id: Annotated[int, Field(description="New element type id")],
    ) -> SwapTypeResult:
        """Change element type."""
        return service.swap_type(element_ids, new_type_id)

    @mcp.tool(
        annotations=destructive_tool("Highlight Elements", destructive=False),
        structured_output=True,
    )
    async def revit_highlight_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
    ) -> HighlightElementsResult:
        """Select elements in Revit UI for engineer visibility."""
        return service.highlight_elements(element_ids)


def _register_transform_tools(mcp: McpRegistry, service: ElementService) -> None:
    @mcp.tool(
        annotations=destructive_tool("Place Family"),
        structured_output=True,
    )
    async def revit_place_family(
        family_name: Annotated[str, Field(description="Family name")],
        placements: Annotated[
            list[PlacementSpec], Field(description="Placement locations")
        ],
        type_name: Annotated[str | None, Field(description="Family type name")] = None,
        properties: Annotated[
            dict[str, Any] | None, Field(description="Instance parameter overrides")
        ] = None,
    ) -> PlaceFamilyResult:
        """Create family instance(s) at location(s)."""
        if not placements:
            raise ToolError("At least one placement is required")
        return service.place_family(family_name, type_name, placements, properties)

    @mcp.tool(
        annotations=destructive_tool("Move Elements"),
        structured_output=True,
    )
    async def revit_move_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        vector: Annotated[
            list[float],
            Field(
                description="Translation [X,Y,Z] in feet", min_length=3, max_length=3
            ),
        ],
    ) -> MoveElementsResult:
        """Translate elements by vector."""
        return service.move_elements(element_ids, vector)

    @mcp.tool(
        annotations=destructive_tool("Rotate Elements"),
        structured_output=True,
    )
    async def revit_rotate_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        axis_origin: Annotated[
            list[float],
            Field(
                description="Axis origin [X,Y,Z] in feet", min_length=3, max_length=3
            ),
        ],
        axis_direction: Annotated[
            list[float],
            Field(description="Axis direction [X,Y,Z]", min_length=3, max_length=3),
        ],
        degrees: Annotated[float, Field(description="Rotation angle in degrees")],
    ) -> RotateElementsResult:
        """Rotate elements around axis."""
        return service.rotate_elements(
            element_ids, axis_origin, axis_direction, degrees
        )

    @mcp.tool(
        annotations=destructive_tool("Delete Elements"),
        structured_output=True,
    )
    async def revit_delete_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        dry_run: Annotated[
            bool, Field(description="Preview deletions without applying")
        ] = False,
    ) -> CallToolResult:
        """Delete elements with optional dry-run preview."""
        result = service.delete_elements(element_ids, dry_run)
        if dry_run:
            count = len(result.dry_run_results or [])
            summary = "Delete preview: {} elements".format(count)
        else:
            summary = "Deleted {} elements".format(result.deleted_count)
        return structured_tool_result(summary, result)
