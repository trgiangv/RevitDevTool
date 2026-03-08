"""Document management tools."""

from typing import Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations

from services.document_service import DocumentService
from utils import try_log


def register_document_tools(mcp: FastMCP, document_service: DocumentService) -> None:
    @mcp.tool(
        annotations=ToolAnnotations(
            title="Open Document",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def open_document(
        ctx: Context,
        file_path: str,
        detach: bool = False,
        audit: bool = False,
    ) -> dict[str, Any]:
        """Open a Revit document by file path."""
        await try_log(ctx, "info", "Opening document '{}'".format(file_path))
        return document_service.open_document(file_path=file_path, detach=detach, audit=audit)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Close Document",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def close_document(ctx: Context, save: bool = False) -> dict[str, Any]:
        """Close the active Revit document."""
        await try_log(ctx, "info", "Closing active document")
        return document_service.close_document(save=save)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Save Document",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def save_document(ctx: Context, file_path: str | None = None) -> dict[str, Any]:
        """Save the active document in place or to a new path."""
        await try_log(ctx, "info", "Saving active document")
        return document_service.save_document(file_path=file_path)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Sync With Central",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def sync_with_central(
        ctx: Context,
        comment: str = "",
        compact: bool = False,
        relinquish_all: bool = True,
    ) -> dict[str, Any]:
        """Synchronize the active workshared document with central."""
        await try_log(ctx, "info", "Synchronizing active document with central")
        return document_service.sync_with_central(
            comment=comment,
            compact=compact,
            relinquish_all=relinquish_all,
        )
