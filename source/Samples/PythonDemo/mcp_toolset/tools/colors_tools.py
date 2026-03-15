"""Color tools."""
from __future__ import annotations

from typing import Annotated

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.colors import CategoryParametersResult, ClearColorsResult, ColorSplashResult
from services.colors_service import ColorsService


def register_colors_tools(mcp: FastMCP) -> None:
    colors_service = ColorsService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Color Splash",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def color_splash(
        category_name: Annotated[str, Field(description="Revit category name to colorize, e.g. 'Walls', 'Doors'")],
        parameter_name: Annotated[str, Field(description="Parameter name to group elements by, e.g. 'Type Name', 'Level'")],
        use_gradient: Annotated[bool, Field(description="Use a gradient color scale instead of distinct colors")] = False,
        custom_colors: Annotated[
            list[str] | None,
            Field(description="Optional list of hex color codes to use, e.g. ['#FF0000', '#00FF00']"),
        ] = None,
    ) -> ColorSplashResult:
        """
        Apply category color overrides based on parameter values.

        Workflow:
        - Call list_category_parameters first to get valid parameter names for the category.
        - Elements are colored by their parameter value.
        - Use use_gradient for continuous scales (e.g. elevation); use distinct colors for categorical data.
        - Use custom_colors for specific palettes when you need control over the color mapping.
        """
        return colors_service.color_splash(
            category_name=category_name,
            parameter_name=parameter_name,
            use_gradient=use_gradient,
            custom_colors=custom_colors,
        )

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Clear Colors",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def clear_colors(
        category_name: Annotated[str, Field(description="Revit category name to clear color overrides for, e.g. 'Walls'")],
    ) -> ClearColorsResult:
        """
        Clear color overrides for a category in the active view.

        Workflow:
        - Use after color_splash to reset visual overrides for a category.
        - Requires the same view where colors were applied.
        - Call for each category that was colorized to fully reset the view.
        """
        return colors_service.clear_colors(category_name=category_name)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Category Parameters",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_category_parameters(
        category_name: Annotated[str, Field(description="Revit category name to list parameters for, e.g. 'Walls'")],
    ) -> CategoryParametersResult:
        """
        List parameters available for a category so agents can choose color grouping keys safely.

        Workflow:
        - Call before color_splash to discover valid parameter names for grouping elements by color.
        - Use sample_value in results to understand parameter content.
        - Parameters with has_value=True on sample elements are good candidates for color_splash.
        """
        return colors_service.list_category_parameters(category_name=category_name)
