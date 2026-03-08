"""View-related tools."""

from typing import Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations

from services.view_service import ViewService
from utils import try_log


def register_view_tools(mcp: FastMCP, view_service: ViewService) -> None:
    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Revit View",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def get_revit_view(view_name: str, ctx: Context | None = None) -> dict[str, Any]:
        """Export a named Revit view as a PNG payload."""
        _ = ctx
        return view_service.get_view_image(view_name)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Revit Views",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_revit_views(ctx: Context | None = None) -> dict[str, Any]:
        """List exportable Revit views grouped by view type."""
        _ = ctx
        return view_service.list_views()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Current View Info",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def get_current_view_info(ctx: Context | None = None) -> dict[str, Any]:
        """Get metadata for the current active view."""
        await try_log(ctx, "info", "Getting current view information...")
        return view_service.current_view_info()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Current View Elements",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def get_current_view_elements(
        limit: int = 5000,
        include_levels: bool = False,
        include_location: bool = False,
        ctx: Context | None = None,
    ) -> dict[str, Any]:
        """List elements visible in the current view, optionally including levels and locations."""
        await try_log(ctx, "info", "Getting elements in current view...")
        return view_service.current_view_elements(
            limit=limit,
            include_levels=include_levels,
            include_location=include_location,
        )
