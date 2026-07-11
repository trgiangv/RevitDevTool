"""Infrastructure and document management tools."""
from __future__ import annotations

from typing import Annotated

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from dto.infrastructure import (
    CloseDocumentResult,
    GenerateGridsResult,
    GenerateLevelsResult,
    GridAxisSpec,
    LevelSpec,
    SaveDocumentResult,
    StatusResult,
    SyncResult,
)
from services.document_service import DocumentService
from services.infrastructure_service import InfrastructureService


def register_infrastructure_tools(mcp: FastMCP) -> None:
    infra = InfrastructureService()
    docs = DocumentService()

    @mcp.tool(annotations=ToolAnnotations(title="Get Status", readOnlyHint=True), structured_output=True)
    async def revit_get_status() -> StatusResult:
        """Health + worksharing + selection info."""
        return infra.get_status()

    @mcp.tool(annotations=ToolAnnotations(title="Save Document", destructiveHint=True), structured_output=True)
    async def revit_save_document(
        file_path: Annotated[str | None, Field(alias="filePath", description="SaveAs path; omit for in-place")] = None,
    ) -> SaveDocumentResult:
        """Save or SaveAs."""
        return docs.save_document(file_path)

    @mcp.tool(annotations=ToolAnnotations(title="Close Document", destructiveHint=True), structured_output=True)
    async def revit_close_document(
        save: Annotated[bool, Field(description="Save before closing")] = False,
    ) -> CloseDocumentResult:
        """Close active document."""
        return docs.close_document(save)

    @mcp.tool(annotations=ToolAnnotations(title="Sync With Central", destructiveHint=True), structured_output=True)
    async def revit_sync_with_central(
        comment: Annotated[str, Field()] = "",
        compact: Annotated[bool, Field()] = False,
        relinquish_all: Annotated[bool, Field(alias="relinquishAll")] = False,
        save_local_before: Annotated[bool, Field(alias="saveLocalBefore")] = True,
    ) -> SyncResult:
        """Workshared sync."""
        if save_local_before:
            try:
                docs.save_document()
            except Exception:
                pass
        return docs.sync_with_central(comment=comment, compact=compact, relinquish_all=relinquish_all)

    @mcp.tool(annotations=ToolAnnotations(title="Generate Grids", destructiveHint=True), structured_output=True)
    async def revit_generate_grids(
        vertical: GridAxisSpec,
        horizontal: GridAxisSpec,
        origin: Annotated[list[float] | None, Field(description="[X,Y,Z] in feet")] = None,
    ) -> GenerateGridsResult:
        """Create grid system."""
        return infra.generate_grids(vertical, horizontal, origin)

    @mcp.tool(annotations=ToolAnnotations(title="Generate Levels", destructiveHint=True), structured_output=True)
    async def revit_generate_levels(
        levels: Annotated[list[LevelSpec], Field(description="Level specifications")],
    ) -> GenerateLevelsResult:
        """Create levels batch."""
        return infra.generate_levels(levels)
