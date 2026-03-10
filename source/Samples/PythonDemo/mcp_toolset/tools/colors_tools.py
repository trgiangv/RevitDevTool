"""Color tools."""

from typing import Annotated, Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from services.colors_service import ColorsService
from utils import try_log


def register_colors_tools(mcp: FastMCP, colors_service: ColorsService) -> None:
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
        custom_colors: Annotated[list[str] | None, Field(description="Optional list of hex color codes to use, e.g. ['#FF0000', '#00FF00']")] = None,
        ctx: Context | None = None,
    ) -> dict[str, Any]:
        """Apply category color overrides based on parameter values."""
        await try_log(ctx, "info", "Color splashing {} elements by {}".format(category_name, parameter_name))
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
        ctx: Context | None = None,
    ) -> dict[str, Any]:
        """Clear color overrides for a category in the active view."""
        await try_log(ctx, "info", "Clearing color overrides for {} elements".format(category_name))
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
        ctx: Context | None = None,
    ) -> dict[str, Any]:
        """List parameters available for a category so agents can choose color grouping keys safely."""
        await try_log(ctx, "info", "Getting available parameters for {} category".format(category_name))
        return colors_service.list_category_parameters(category_name=category_name)
