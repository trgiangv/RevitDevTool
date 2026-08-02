"""Visualization and annotation tools."""
from __future__ import annotations

from typing import Annotated

from mcp.server.mcpserver import MCPServer
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.visualization import (
    ClearOverridesResult,
    ColorByParameterResult,
    OverrideColorsResult,
    PlaceTagsResult,
    TagPlacement,
)
from services.visualization_service import VisualizationService


def register_visualization_tools(mcp: MCPServer) -> None:
    service = VisualizationService()

    @mcp.tool(annotations=ToolAnnotations(title="Color by Parameter", destructiveHint=True), structured_output=True)
    async def revit_color_by_parameter(
        category_name: Annotated[str, Field(description="Category display name")],
        parameter_name: Annotated[str, Field(description="Parameter name")],
        view_id: Annotated[int | None, Field(description="View element id")] = None,
        use_gradient: Annotated[bool, Field(description="Use gradient color scheme")] = False,
        colors: Annotated[list[str] | None, Field(description="Hex colors #RRGGBB")] = None,
    ) -> ColorByParameterResult:
        """Color splash by param value."""
        return service.color_by_parameter(category_name, parameter_name, view_id, use_gradient, colors)

    @mcp.tool(annotations=ToolAnnotations(title="Clear Overrides", destructiveHint=True), structured_output=True)
    async def revit_clear_overrides(
        category_name: Annotated[str, Field(description="Category display name")],
        view_id: Annotated[int | None, Field(description="View element id")] = None,
    ) -> ClearOverridesResult:
        """Clear graphic overrides."""
        return service.clear_overrides(category_name, view_id)

    @mcp.tool(annotations=ToolAnnotations(title="Place Tags", destructiveHint=True), structured_output=True)
    async def revit_place_tags(
        tagging_data: Annotated[list[TagPlacement], Field(description="Tag placements")],
    ) -> PlaceTagsResult:
        """Auto-tag elements in view(s)."""
        return service.place_tags(tagging_data)

    @mcp.tool(annotations=ToolAnnotations(title="Override Colors", destructiveHint=True), structured_output=True)
    async def revit_override_colors(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        color: Annotated[list[int], Field(description="[R, G, B] 0-255", min_length=3, max_length=3)],
    ) -> OverrideColorsResult:
        """Direct color override on elements."""
        return service.override_colors(element_ids, color)
