"""View-related tools."""
from __future__ import annotations

from typing import Annotated

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.views import ViewElementsResult, ViewImageResult, ViewInfoResult, ViewListResult
from services.view_service import ViewService


def register_view_tools(mcp: FastMCP) -> None:
    view_service = ViewService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Revit View",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def get_revit_view(
        view_name: Annotated[str, Field(description="Exact name of the Revit view to export as PNG")],
    ) -> ViewImageResult:
        """
        Export a named Revit view as a PNG payload.

        Workflow:
        - Call list_revit_views first to get exact view names.
        - Use the returned view name with this tool to retrieve a base64-encoded image.
        - The image can be displayed or analyzed by downstream tools.
        """
        data = view_service.get_view_image(view_name)
        return ViewImageResult.model_validate(data)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Revit Views",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_revit_views() -> ViewListResult:
        """
        List exportable Revit views grouped by view type.

        Workflow:
        - Call before get_revit_view to discover available views.
        - Views are grouped by type: floor plans, elevations, sections, 3D views, etc.
        - Use exact names from the list when calling get_revit_view.
        """
        data = view_service.list_views()
        return ViewListResult.model_validate(data)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Get Current View Info",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def get_current_view_info() -> ViewInfoResult:
        """
        Get metadata for the current active view.

        Workflow:
        - Use when the user has a view open and you need view type, scale, detail level, or other properties.
        - Helps determine if the current view is suitable for element operations or exports.
        """
        data = view_service.current_view_info()
        return ViewInfoResult.model_validate(data)

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
        limit: Annotated[
            int,
            Field(
                description=(
                    "Maximum number of elements to return (1–10000). "
                    "Use lower values (e.g. 500) for quick overviews and faster responses; "
                    "increase up to 10000 for detailed analysis. Higher limits increase payload size and latency."
                ),
                ge=1,
                le=10000,
            ),
        ] = 5000,
        include_levels: Annotated[
            bool,
            Field(
                description=(
                    "Include level assignment for each element. "
                    "Enable when filtering or grouping by level, or when level context matters. "
                    "Disable to reduce payload size when level is not needed."
                ),
            ),
        ] = False,
        include_location: Annotated[
            bool,
            Field(
                description=(
                    "Include XYZ location point (or curve start/end) for each element. "
                    "Enable for spatial analysis, placement reference, or distance calculations. "
                    "Disable to reduce payload size when location is not needed."
                ),
            ),
        ] = False,
    ) -> ViewElementsResult:
        """
        List elements visible in the current view, optionally including levels and locations.

        Workflow:
        - Open the target view in Revit first, then call this tool.
        - Use limit to control response size; start with 500 for quick scans.
        - Enable include_levels or include_location only when needed to reduce payload.
        - Check truncated flag in the result to see if more elements exist beyond the limit.
        """
        data = view_service.current_view_elements(
            limit=limit,
            include_levels=include_levels,
            include_location=include_location,
        )
        return ViewElementsResult.model_validate(data)
