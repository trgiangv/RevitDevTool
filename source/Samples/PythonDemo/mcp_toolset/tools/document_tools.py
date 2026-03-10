"""Document management tools."""

from typing import Annotated, Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

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
        file_path: Annotated[str, Field(description="Absolute path to the .rvt file to open")],
        detach: Annotated[bool, Field(description="Open detached from central (workshared models only)")] = False,
        audit: Annotated[bool, Field(description="Run audit on open to repair corrupted elements")] = False,
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
    async def close_document(
        ctx: Context,
        save: Annotated[bool, Field(description="Save the document before closing")] = False,
    ) -> dict[str, Any]:
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
    async def save_document(
        ctx: Context,
        file_path: Annotated[str | None, Field(description="Save to a new path; saves in place if omitted")] = None,
    ) -> dict[str, Any]:
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
        comment: Annotated[str, Field(description="Sync comment to record in the central model history")] = "",
        compact: Annotated[bool, Field(description="Compact the central model during sync to reduce file size")] = False,
        relinquish_all: Annotated[bool, Field(description="Relinquish all owned worksets and elements after sync")] = True,
    ) -> dict[str, Any]:
        """Synchronize the active workshared document with central."""
        await try_log(ctx, "info", "Synchronizing active document with central")
        return document_service.sync_with_central(
            comment=comment,
            compact=compact,
            relinquish_all=relinquish_all,
        )
