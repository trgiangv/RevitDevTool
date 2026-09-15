"""Model-space drawing mutation tools."""

from typing import Annotated

from pydantic import Field

from dto.drawing import (
    DrawCircleResult,
    DrawLineResult,
    EraseEntitiesResult,
    HighlightEntitiesResult,
)
from services.drawing_service import DrawingService
from shared.mcp_registry import McpRegistry
from shared.tool_annotations import destructive_tool


def register_drawing_tools(mcp: McpRegistry) -> None:
    """Register AutoCAD draw / select / erase tools."""
    service = DrawingService()

    @mcp.tool(
        annotations=destructive_tool("Draw Line"),
        structured_output=True,
    )
    async def acad_draw_line(
        start: Annotated[list[float], Field(description="Start [x, y] or [x, y, z]")],
        end: Annotated[list[float], Field(description="End [x, y] or [x, y, z]")],
        layer: Annotated[
            str | None, Field(description="Existing layer name; omit for current")
        ] = None,
    ) -> DrawLineResult:
        """Create a LINE in model space."""
        return service.draw_line(start, end, layer)

    @mcp.tool(
        annotations=destructive_tool("Draw Circle"),
        structured_output=True,
    )
    async def acad_draw_circle(
        center: Annotated[list[float], Field(description="Center [x, y] or [x, y, z]")],
        radius: Annotated[float, Field(gt=0, description="Radius in drawing units")],
        layer: Annotated[
            str | None, Field(description="Existing layer name; omit for current")
        ] = None,
    ) -> DrawCircleResult:
        """Create a CIRCLE in model space."""
        return service.draw_circle(center, radius, layer)

    @mcp.tool(
        annotations=destructive_tool("Highlight Entities", destructive=False),
        structured_output=True,
    )
    async def acad_highlight_entities(
        handles: Annotated[list[str], Field(description="Entity handles (hex)")],
    ) -> HighlightEntitiesResult:
        """Set implied selection from handles for engineer visibility."""
        return service.highlight_entities(handles)

    @mcp.tool(
        annotations=destructive_tool("Erase Entities"),
        structured_output=True,
    )
    async def acad_erase_entities(
        handles: Annotated[list[str], Field(description="Entity handles (hex)")],
    ) -> EraseEntitiesResult:
        """Erase entities by handle."""
        return service.erase_entities(handles)
