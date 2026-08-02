"""Multimodal MCP content tool registrations."""
from __future__ import annotations

import base64
from typing import Annotated

from mcp.server.mcpserver import MCPServer
from mcp.types import (
    CallToolResult,
    EmbeddedResource,
    ImageContent,
    ResourceLink,
    TextContent,
    TextResourceContents,
    ToolAnnotations,
)
from pydantic import Field

from services.content_service import ContentService


def register_content_tools(mcp: MCPServer) -> None:
    service = ContentService()

    @mcp.tool(annotations=ToolAnnotations(title="Capture View (inline image)", readOnlyHint=True))
    async def revit_capture_view(
        resolution: Annotated[int, Field(description="Image DPI (default 150)")] = 150,
    ) -> CallToolResult:
        """Capture active view as inline PNG for vision verification."""
        data, view_name, view_id, image_path = service.capture_view(resolution)
        encoded = base64.b64encode(data).decode("ascii")
        return CallToolResult(
            content=[ImageContent(type="image", data=encoded, mime_type="image/png")],
        )

    @mcp.tool(annotations=ToolAnnotations(title="Preview Schedule (embedded CSV)", readOnlyHint=True))
    async def revit_preview_schedule(
        schedule_id: Annotated[int, Field(description="Schedule element id")],
        max_rows: Annotated[int, Field(description="Max embedded rows (default 30)")] = 30,
    ) -> CallToolResult:
        """Return schedule preview as embedded CSV without writing files."""
        name, csv_text, embedded_rows, total_rows, column_count = service.preview_schedule(
            schedule_id, max_rows
        )
        uri = "revit://schedule/{}/preview".format(schedule_id)
        return CallToolResult(
            content=[
                TextContent(
                    type="text",
                    text="Schedule '{}' preview: {} of {} rows embedded as CSV.".format(
                        name, embedded_rows, total_rows
                    ),
                ),
                EmbeddedResource(
                    type="resource",
                    resource=TextResourceContents(
                        uri=uri,
                        mime_type="text/csv",
                        text=csv_text,
                    ),
                ),
            ],
            structured_content={
                "scheduleId": schedule_id,
                "scheduleName": name,
                "embeddedRows": embedded_rows,
                "totalRows": total_rows,
                "columns": column_count,
            },
        )

    @mcp.tool(
        annotations=ToolAnnotations(title="Model Digest (resource link)", readOnlyHint=True),
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
                        title=digest["projectTitle"],
                        views=digest["viewCount"],
                        levels=digest["levelCount"],
                        warnings=digest["warningCount"],
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
            structured_content=digest,
        )
