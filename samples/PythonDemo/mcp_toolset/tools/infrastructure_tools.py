"""Infrastructure and document management tools."""
from __future__ import annotations

from typing import Annotated

from mcp.server.mcpserver import MCPServer
from mcp.types import CallToolResult, TextContent, ToolAnnotations
from pydantic import Field

from dto.infrastructure import (
    CloseDocumentResult,
    GenerateGridsResult,
    GenerateLevelsResult,
    GridAxisSpec,
    LevelSpec,
    SaveDocumentResult,
    SyncResult,
)
from services.document_service import DocumentService
from services.infrastructure_service import InfrastructureService
from shared.mcp_task_execution_meta import McpTaskExecutionMeta


def register_infrastructure_tools(mcp: MCPServer) -> None:
    infra = InfrastructureService()
    docs = DocumentService()

    @mcp.tool(annotations=ToolAnnotations(title="Get Status", readOnlyHint=True), structured_output=True)
    async def revit_get_status() -> CallToolResult:
        """Health + worksharing + selection info."""
        result = infra.get_status()
        structured = result.model_dump(by_alias=True)
        if not result.healthy and not result.document_title:
            summary = "No active document"
        elif result.healthy:
            try:
                from RevitDevTool.Core import RevitContext

                doc = RevitContext.ActiveDocument
                warnings = doc.GetWarnings() if doc is not None else None
                warning_count = len(warnings) if warnings else 0
            except Exception:
                warning_count = 0
            summary = "Model healthy, {} warnings".format(warning_count)
        else:
            summary = "Model unhealthy"
        return CallToolResult(
            content=[TextContent(type="text", text=summary)],
            structured_content=structured,
        )

    @mcp.tool(annotations=ToolAnnotations(title="Save Document", destructiveHint=True), structured_output=True)
    async def revit_save_document(
        file_path: Annotated[str | None, Field(description="SaveAs path; omit for in-place")] = None,
    ) -> SaveDocumentResult:
        """Save or SaveAs."""
        return docs.save_document(file_path)

    @mcp.tool(annotations=ToolAnnotations(title="Close Document", destructiveHint=True), structured_output=True)
    async def revit_close_document(
        save: Annotated[bool, Field(description="Save before closing")] = False,
    ) -> CloseDocumentResult:
        """Close active document."""
        return docs.close_document(save)

    @mcp.tool(
        annotations=ToolAnnotations(title="Sync With Central", destructiveHint=True),
        structured_output=True,
        meta=McpTaskExecutionMeta.OptionalMeta,
    )
    async def revit_sync_with_central(
        comment: Annotated[str, Field()] = "",
        compact: Annotated[bool, Field()] = False,
        relinquish_all: Annotated[bool, Field(description="Relinquish all borrowed worksets")] = False,
        save_local_before: Annotated[bool, Field(description="Save local file before sync")] = True,
    ) -> SyncResult:
        """Workshared sync."""
        return docs.sync_with_central(
            comment=comment,
            compact=compact,
            relinquish_all=relinquish_all,
            save_local_before=save_local_before,
        )

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
