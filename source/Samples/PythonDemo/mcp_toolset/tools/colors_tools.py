"""Color tools."""

from typing import Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations

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
        category_name: str,
        parameter_name: str,
        use_gradient: bool = False,
        custom_colors: list[str] | None = None,
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
    async def clear_colors(category_name: str, ctx: Context | None = None) -> dict[str, Any]:
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
    async def list_category_parameters(category_name: str, ctx: Context | None = None) -> dict[str, Any]:
        """List parameters available for a category so agents can choose color grouping keys safely."""
        await try_log(ctx, "info", "Getting available parameters for {} category".format(category_name))
        return colors_service.list_category_parameters(category_name=category_name)
