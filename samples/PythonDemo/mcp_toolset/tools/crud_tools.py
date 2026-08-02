"""Element CRUD tools."""
from __future__ import annotations
from typing import Annotated, Any

from mcp.server.mcpserver import MCPServer
from mcp.types import CallToolResult, TextContent, ToolAnnotations
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
from shared.responses import ToolError


def register_crud_tools(mcp: MCPServer) -> None:
    service = ElementService()

    @mcp.tool(annotations=ToolAnnotations(title="Write Parameters", destructiveHint=True), structured_output=True)
    async def revit_write_parameters(
        element_ids: Annotated[list[int], Field(description="Target element ids")],
        updates: Annotated[list[ParameterUpdate], Field(description="Parameter updates")],
    ) -> WriteParametersResult:
        """Set param values on elements."""
        return service.write_parameters(element_ids, updates)

    @mcp.tool(annotations=ToolAnnotations(title="Place Family", destructiveHint=True), structured_output=True)
    async def revit_place_family(
        family_name: Annotated[str, Field(description="Family name")],
        placements: Annotated[list[PlacementSpec], Field(description="Placement locations")],
        type_name: Annotated[str | None, Field(description="Family type name")] = None,
        properties: Annotated[dict[str, Any] | None, Field(description="Instance parameter overrides")] = None,
    ) -> PlaceFamilyResult:
        """Create family instance(s) at location(s)."""
        if not placements:
            raise ToolError("At least one placement is required")
        return service.place_family(family_name, type_name, placements, properties)

    @mcp.tool(annotations=ToolAnnotations(title="Move Elements", destructiveHint=True), structured_output=True)
    async def revit_move_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        vector: Annotated[list[float], Field(description="Translation [X,Y,Z] in feet", min_length=3, max_length=3)],
    ) -> MoveElementsResult:
        """Translate elements by vector."""
        return service.move_elements(element_ids, vector)

    @mcp.tool(annotations=ToolAnnotations(title="Rotate Elements", destructiveHint=True), structured_output=True)
    async def revit_rotate_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        axis_origin: Annotated[list[float], Field(description="Axis origin [X,Y,Z] in feet", min_length=3, max_length=3)],
        axis_direction: Annotated[list[float], Field(description="Axis direction [X,Y,Z]", min_length=3, max_length=3)],
        degrees: Annotated[float, Field(description="Rotation angle in degrees")],
    ) -> RotateElementsResult:
        """Rotate elements around axis."""
        return service.rotate_elements(element_ids, axis_origin, axis_direction, degrees)

    @mcp.tool(annotations=ToolAnnotations(title="Delete Elements", destructiveHint=True), structured_output=True)
    async def revit_delete_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        dry_run: Annotated[bool, Field(description="Preview deletions without applying")] = False,
    ) -> CallToolResult:
        """Delete elements with optional dry-run preview."""
        result = service.delete_elements(element_ids, dry_run)
        structured = result.model_dump(by_alias=True)
        if dry_run:
            count = len(result.dry_run_results or [])
            summary = "Delete preview: {} elements".format(count)
        else:
            summary = "Deleted {} elements".format(result.deleted_count)
        return CallToolResult(
            content=[TextContent(type="text", text=summary)],
            structured_content=structured,
        )

    @mcp.tool(annotations=ToolAnnotations(title="Clone Parameters", destructiveHint=True), structured_output=True)
    async def revit_clone_parameters(
        source_id: Annotated[int, Field(description="Source element id")],
        target_ids: Annotated[list[int], Field(description="Target element ids")],
        param_names: Annotated[list[str], Field(description="Parameter names to copy")],
    ) -> CloneParametersResult:
        """Copy param values source → targets."""
        return service.clone_parameters(source_id, target_ids, param_names)

    @mcp.tool(annotations=ToolAnnotations(title="Swap Type", destructiveHint=True), structured_output=True)
    async def revit_swap_type(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        new_type_id: Annotated[int, Field(description="New element type id")],
    ) -> SwapTypeResult:
        """Change element type."""
        return service.swap_type(element_ids, new_type_id)

    @mcp.tool(annotations=ToolAnnotations(title="Highlight Elements", destructiveHint=False), structured_output=True)
    async def revit_highlight_elements(
        element_ids: Annotated[list[int], Field(description="Element ids")],
    ) -> HighlightElementsResult:
        """Select elements in Revit UI for engineer visibility."""
        return service.highlight_elements(element_ids)
