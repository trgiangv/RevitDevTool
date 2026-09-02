"""Visualization and annotation tools."""

from typing import Annotated

from pydantic import Field

from dto.visualization import (
    ClearOverridesResult,
    ColorByParameterResult,
    OverrideColorsResult,
    PlaceTagsResult,
    TagPlacement,
)
from services.visualization_service import VisualizationService
from shared.mcp_registry import McpRegistry
from shared.tool_annotations import destructive_tool


def register_visualization_tools(mcp: McpRegistry) -> None:
    """Register visualization and annotation MCP tools."""
    service = VisualizationService()

    @mcp.tool(
        annotations=destructive_tool("Color by Parameter"),
        structured_output=True,
    )
    async def revit_color_by_parameter(
        category_name: Annotated[str, Field(description="Category display name")],
        parameter_name: Annotated[str, Field(description="Parameter name")],
        view_id: Annotated[int | None, Field(description="View element id")] = None,
        use_gradient: Annotated[
            bool, Field(description="Use gradient color scheme")
        ] = False,
        colors: Annotated[
            list[str] | None, Field(description="Hex colors #RRGGBB")
        ] = None,
    ) -> ColorByParameterResult:
        """Color splash by param value."""
        return service.color_by_parameter(
            category_name, parameter_name, view_id, use_gradient, colors
        )

    @mcp.tool(
        annotations=destructive_tool("Clear Overrides"),
        structured_output=True,
    )
    async def revit_clear_overrides(
        category_name: Annotated[str, Field(description="Category display name")],
        view_id: Annotated[int | None, Field(description="View element id")] = None,
    ) -> ClearOverridesResult:
        """Clear graphic overrides."""
        return service.clear_overrides(category_name, view_id)

    @mcp.tool(
        annotations=destructive_tool("Place Tags"),
        structured_output=True,
    )
    async def revit_place_tags(
        tagging_data: Annotated[
            list[TagPlacement], Field(description="Tag placements")
        ],
    ) -> PlaceTagsResult:
        """Auto-tag elements in view(s)."""
        return service.place_tags(tagging_data)

    @mcp.tool(
        annotations=destructive_tool("Override Colors"),
        structured_output=True,
    )
    async def revit_override_colors(
        element_ids: Annotated[list[int], Field(description="Element ids")],
        color: Annotated[
            list[int], Field(description="[R, G, B] 0-255", min_length=3, max_length=3)
        ],
    ) -> OverrideColorsResult:
        """Direct color override on elements."""
        return service.override_colors(element_ids, color)
