"""Document management tools."""
from __future__ import annotations

from typing import Annotated

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.documents import CloseDocumentResult, OpenDocumentResult, SaveDocumentResult, SyncResult
from services.document_service import DocumentService


def register_document_tools(mcp: FastMCP) -> None:
    document_service = DocumentService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Open Document",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def open_document(
        file_path: Annotated[str, Field(description="Absolute path to the .rvt file to open")],
        detach: Annotated[bool, Field(description="Open detached from central (workshared models only)")] = False,
        audit: Annotated[bool, Field(description="Run audit on open to repair corrupted elements")] = False,
    ) -> OpenDocumentResult:
        """
        Open a Revit document by file path.

        Workflow:
        - Use after launch_revit for an empty session, or to switch documents.
        - For workshared files, use detach to open a local copy without central connection.
        - Use audit only when the file is suspected to be corrupted.
        """
        data = document_service.open_document(file_path=file_path, detach=detach, audit=audit)
        return OpenDocumentResult.model_validate(data)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Close Document",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def close_document(
        save: Annotated[bool, Field(description="Save the document before closing")] = False,
    ) -> CloseDocumentResult:
        """
        Close the active Revit document.

        Workflow:
        - Call when done with the current document.
        - Set save=True to persist changes before closing.
        - Ensure no other operations are in progress before closing.
        """
        data = document_service.close_document(save=save)
        return CloseDocumentResult.model_validate(data)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Save Document",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def save_document(
        file_path: Annotated[str | None, Field(description="Save to a new path; saves in place if omitted")] = None,
    ) -> SaveDocumentResult:
        """
        Save the active document in place or to a new path.

        Workflow:
        - Omit file_path to save in place (overwrites current file).
        - Provide file_path for Save As to a new location.
        - For workshared models, consider sync_with_central after saving.
        """
        data = document_service.save_document(file_path=file_path)
        return SaveDocumentResult.model_validate(data)

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Sync With Central",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def sync_with_central(
        comment: Annotated[str, Field(description="Sync comment to record in the central model history")] = "",
        compact: Annotated[bool, Field(description="Compact the central model during sync to reduce file size")] = False,
        relinquish_all: Annotated[bool, Field(description="Relinquish all owned worksets and elements after sync")] = True,
    ) -> SyncResult:
        """
        Synchronize the active workshared document with central.

        Workflow:
        - Use after making changes in a workshared (central) model.
        - Set relinquish_all=True to release elements for others to edit.
        - Use compact periodically to reduce central file size.
        """
        data = document_service.sync_with_central(
            comment=comment,
            compact=compact,
            relinquish_all=relinquish_all,
        )
        return SyncResult.model_validate(data)
