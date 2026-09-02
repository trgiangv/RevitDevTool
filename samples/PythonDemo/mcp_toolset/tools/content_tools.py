"""Multimodal MCP content tool registrations."""

import base64
from typing import Annotated

from mcp.types import (
    CallToolResult,
    EmbeddedResource,
    ImageContent,
    ResourceLink,
    TextContent,
    TextResourceContents,
)
from pydantic import Field

from services.content_service import ContentService
from shared.mcp_registry import McpRegistry
from shared.tool_annotations import read_only_tool


def register_content_tools(mcp: McpRegistry) -> None:
    """Register view capture, schedule preview, and model digest tools."""
    service = ContentService()

    @mcp.tool(annotations=read_only_tool("Capture View (inline image)"))
    async def revit_capture_view(
        resolution: Annotated[int, Field(description="Image DPI (default 150)")] = 150,
    ) -> CallToolResult:
        """Capture active view as inline PNG for vision verification."""
        capture = service.capture_view(resolution)
        encoded = base64.b64encode(capture.data).decode("ascii")
        return CallToolResult(
            content=[ImageContent(type="image", data=encoded, mime_type="image/png")],
        )

    @mcp.tool(annotations=read_only_tool("Preview Schedule (embedded CSV)"))
    async def revit_preview_schedule(
        schedule_id: Annotated[int, Field(description="Schedule element id")],
        max_rows: Annotated[
            int, Field(description="Max embedded rows (default 30)")
        ] = 30,
    ) -> CallToolResult:
        """Return schedule preview as embedded CSV without writing files."""
        preview = service.preview_schedule(schedule_id, max_rows)
        uri = "revit://schedule/{}/preview".format(schedule_id)
        return CallToolResult(
            content=[
                TextContent(
                    type="text",
                    text="Schedule '{}' preview: {} of {} rows embedded as CSV.".format(
                        preview.schedule_name,
                        preview.embedded_rows,
                        preview.total_rows,
                    ),
                ),
                EmbeddedResource(
                    type="resource",
                    resource=TextResourceContents(
                        uri=uri,
                        mime_type="text/csv",
                        text=preview.csv_text,
                    ),
                ),
            ],
            structured_content=preview.model_dump(by_alias=True),
        )

    @mcp.tool(
        annotations=read_only_tool("Model Digest (resource link)"),
        structured_output=True,
    )
    async def revit_model_digest() -> CallToolResult:
        """Compact digest with structured counts and link to revit://model/views."""
        digest = service.model_digest()
        return CallToolResult(
            content=[
                TextContent(
                    type="text",
                    text=(
                        "Project '{title}': {views} views, {levels} levels, {warnings} warnings. "
                        "Use the linked resource for full view/sheet metadata."
                    ).format(
                        title=digest.project_title,
                        views=digest.view_count,
                        levels=digest.level_count,
                        warnings=digest.warning_count,
                    ),
                ),
                ResourceLink(
                    type="resource_link",
                    uri="revit://model/views",
                    name="revit_model_views",
                    title="Model views",
                    description="Full view and sheet metadata for documentation workflows.",
                    mime_type="application/json",
                ),
            ],
            structured_content=digest.model_dump(by_alias=True),
        )
